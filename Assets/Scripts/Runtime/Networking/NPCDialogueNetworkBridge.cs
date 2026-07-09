using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace NPCSystem
{
    [DefaultExecutionOrder(-900)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public partial class NPCDialogueNetworkBridge : NetworkBehaviour
    {
        [FormerlySerializedAs("DialogueManager")]
        public NPCDialogueManager DialogueManager;
        [FormerlySerializedAs("SessionManager")]
        public NPCNetworkSessionManager SessionManager;

        [FormerlySerializedAs("onNPCChanged")]
        public UnityEvent<string> OnNpcChanged = new UnityEvent<string>();

        [FormerlySerializedAs("onResponseStart")]
        public UnityEvent<string> OnResponseStart = new UnityEvent<string>();

        [FormerlySerializedAs("onResponseUpdated")]
        public UnityEvent<string> OnResponseUpdated = new UnityEvent<string>();

        [FormerlySerializedAs("onResponseComplete")]
        public UnityEvent<string, string> OnResponseComplete = new UnityEvent<string, string>();

        [FormerlySerializedAs("onError")]
        public UnityEvent<string> OnError = new UnityEvent<string>();

        [FormerlySerializedAs("onNotebookStateChanged")]
        public UnityEvent<NPCNotebookStateMessage> OnNotebookStateChanged =
            new UnityEvent<NPCNotebookStateMessage>();

        ulong? _activeClientId;
        string _activeRequestId = string.Empty;
        string _localSelectedNpcSlug = string.Empty;
        bool _eventsBound;
        bool _disconnectCallbackRegistered;
        readonly Queue<PendingDialogueRequest> _pendingRequests =
            new Queue<PendingDialogueRequest>();

        [SerializeField]
        string lastRoutingStatus = "Idle";

        class PendingDialogueRequest
        {
            public ulong clientId;
            public NPCDialogueRequestMessage request;
        }

        // ── Lifecycle ──────────────────────────────────────────────────

        void Awake()
        {
            ResolveReferences();
            BindManagerEvents();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            RegisterDisconnectCallback();
        }

        public override void OnNetworkDespawn()
        {
            UnregisterDisconnectCallback();
            base.OnNetworkDespawn();
        }

        public override void OnDestroy()
        {
            UnregisterDisconnectCallback();
            UnbindManagerEvents();
            base.OnDestroy();
        }

        public async Task InitializeAsync()
        {
            ResolveReferences();
            if (DialogueManager != null)
            {
                if (DialogueManager.IsInitialized)
                {
                    CaptureBaselineState();
                    UpdateNotebookStateLocal();
                }
                else
                {
                    await DialogueManager.InitializeAsync();
                    CaptureBaselineState();
                    UpdateNotebookStateLocal();
                }
            }
        }

        // ── Public API ──────────────────────────────────────────────────

        public async Task RequestNpcSelectionAsync(string npcSlug)
        {
            ResolveReferences();

            var selection = new NPCDialogueSelectionMessage { npcSlug = npcSlug };
            selection.SanitizeInPlace();

            if (string.IsNullOrWhiteSpace(selection.npcSlug))
            {
                RaiseErrorLocal("NPC slug is required.");
                return;
            }

            _localSelectedNpcSlug = selection.npcSlug;

            if (
                !Application.isPlaying
                || NetworkManager == null
                || !NetworkManager.IsListening
                || IsServer
            )
            {
                if (SessionManager != null && NetworkManager != null)
                {
                    SessionManager.SetSelectedNpcSlug(
                        NetworkManager.LocalClientId,
                        selection.npcSlug
                    );
                }

                if (DialogueManager != null)
                {
                    await DialogueManager.SwitchToNPCAsync(selection.npcSlug);
                    UpdateNotebookStateLocal();
                }
                return;
            }

            RequestNpcSelectionServerRpc(selection);
        }

        public void SubmitPlayerMessage(string playerMessage)
        {
            ResolveReferences();

            var request = new NPCDialogueRequestMessage
            {
                requestId = Guid.NewGuid().ToString("N"),
                npcSlug =
                    currentProfile != null ? currentProfile.GetNpcSlug() : _localSelectedNpcSlug,
                playerMessage = playerMessage,
            };
            request.SanitizeInPlace();

            if (string.IsNullOrWhiteSpace(request.playerMessage))
            {
                RaiseErrorLocal("Player message is required.");
                return;
            }

            if (
                !Application.isPlaying
                || NetworkManager == null
                || !NetworkManager.IsListening
                || IsServer
            )
            {
                DialogueManager?.SendMessage(request.playerMessage);
                return;
            }

            SubmitDialogueServerRpc(request);
        }

        public new void SendMessage(string playerMessage)
        {
            SubmitPlayerMessage(playerMessage);
        }

        public void CancelActiveRequest()
        {
            ResolveReferences();

            if (
                !Application.isPlaying
                || NetworkManager == null
                || !NetworkManager.IsListening
                || IsServer
            )
            {
                DialogueManager?.CancelRequests();
                return;
            }

            CancelActiveRequestServerRpc();
        }

        // ── Reference Resolution ────────────────────────────────────────

        void ResolveReferences()
        {
            if (DialogueManager == null)
            {
                DialogueManager = FindAnyObjectByType<NPCDialogueManager>(
                    FindObjectsInactive.Include
                );
            }

            if (SessionManager == null)
            {
                SessionManager = GetComponent<NPCNetworkSessionManager>();
                if (SessionManager == null)
                {
                    SessionManager = FindAnyObjectByType<NPCNetworkSessionManager>(
                        FindObjectsInactive.Include
                    );
                }
            }
        }

        void BindManagerEvents()
        {
            if (_eventsBound || DialogueManager == null)
                return;

            DialogueManager.OnNpcChanged.AddListener(HandleManagerNpcChanged);
            DialogueManager.OnResponseStart.AddListener(HandleManagerResponseStart);
            DialogueManager.OnResponseUpdated.AddListener(HandleManagerResponseUpdated);
            DialogueManager.OnResponseComplete.AddListener(HandleManagerResponseComplete);
            DialogueManager.OnError.AddListener(HandleManagerError);
            _eventsBound = true;
        }

        void UnbindManagerEvents()
        {
            if (!_eventsBound || DialogueManager == null)
                return;

            DialogueManager.OnNpcChanged.RemoveListener(HandleManagerNpcChanged);
            DialogueManager.OnResponseStart.RemoveListener(HandleManagerResponseStart);
            DialogueManager.OnResponseUpdated.RemoveListener(HandleManagerResponseUpdated);
            DialogueManager.OnResponseComplete.RemoveListener(HandleManagerResponseComplete);
            DialogueManager.OnError.RemoveListener(HandleManagerError);
            _eventsBound = false;
        }

        void RegisterDisconnectCallback()
        {
            if (_disconnectCallbackRegistered || NetworkManager == null)
                return;
            NetworkManager.OnClientDisconnectCallback += HandleClientDisconnected;
            _disconnectCallbackRegistered = true;
        }

        void UnregisterDisconnectCallback()
        {
            if (!_disconnectCallbackRegistered || NetworkManager == null)
                return;
            NetworkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
            _disconnectCallbackRegistered = false;
        }

        void HandleClientDisconnected(ulong clientId)
        {
            LogClientSessionEvent(
                clientId,
                NPCFlowStatus.Warning,
                $"Client {clientId} disconnected. Clearing queued work and session state."
            );
            RemoveQueuedRequestsForClient(clientId);
            SessionManager?.ClearClientSession(clientId);
            if (_activeClientId.HasValue && _activeClientId.Value == clientId)
            {
                DialogueManager?.CancelRequests();
                _activeClientId = null;
                _activeRequestId = string.Empty;
                TryProcessNextQueuedRequest();
            }
        }

        // ── Server RPCs ─────────────────────────────────────────────────

        [Rpc(SendTo.Server)]
        void RequestNpcSelectionServerRpc(
            NPCDialogueSelectionMessage selection,
            RpcParams rpcParams = default
        )
        {
            _ = HandleNpcSelectionServerAsync(selection, rpcParams.Receive.SenderClientId);
        }

        async Task HandleNpcSelectionServerAsync(
            NPCDialogueSelectionMessage selection,
            ulong senderClientId
        )
        {
            ResolveReferences();
            selection.SanitizeInPlace();

            if (SessionManager == null || DialogueManager == null)
            {
                SendErrorToClient(senderClientId, "Dialogue network bridge is not configured.");
                return;
            }

            if (string.IsNullOrWhiteSpace(selection.npcSlug))
            {
                SendErrorToClient(senderClientId, "NPC slug is required.");
                return;
            }

            EnsureClientSessionSeeded(senderClientId);
            SessionManager.SetSelectedNpcSlug(senderClientId, selection.npcSlug);
            LogRoutingEvent(
                senderClientId,
                string.Empty,
                NPCFlowStatus.Start,
                $"Client selected NPC '{selection.npcSlug}'."
            );

            if (_activeClientId.HasValue)
            {
                NPCProfile selectedProfile = FindProfileBySlug(selection.npcSlug);
                SendNpcChangedToClient(
                    senderClientId,
                    selection.npcSlug,
                    selectedProfile != null ? selectedProfile.GetDisplayName() : selection.npcSlug
                );
                SendNotebookStateToClient(
                    senderClientId,
                    BuildNotebookStateMessageForClient(senderClientId)
                );
                return;
            }

            ApplySessionStateToManager(senderClientId);
            await DialogueManager.SwitchToNPCAsync(selection.npcSlug);
            SendNpcChangedToClient(
                senderClientId,
                selection.npcSlug,
                DialogueManager.currentProfile != null
                    ? DialogueManager.currentProfile.GetDisplayName()
                    : selection.npcSlug
            );
            SendNotebookStateToClient(senderClientId, BuildNotebookStateMessage());
        }

        [Rpc(SendTo.Server)]
        void SubmitDialogueServerRpc(
            NPCDialogueRequestMessage request,
            RpcParams rpcParams = default
        )
        {
            _ = HandleSubmitDialogueServerAsync(request, rpcParams.Receive.SenderClientId);
        }

        async Task HandleSubmitDialogueServerAsync(
            NPCDialogueRequestMessage request,
            ulong senderClientId
        )
        {
            ResolveReferences();
            request.SanitizeInPlace();

            if (SessionManager == null || DialogueManager == null)
            {
                SendErrorToClient(senderClientId, "Dialogue network bridge is not configured.");
                return;
            }

            EnsureClientSessionSeeded(senderClientId);

            if (
                !SessionManager.TryGetSelectedNpcSlug(senderClientId, out string selectedNpcSlug)
                || string.IsNullOrWhiteSpace(selectedNpcSlug)
            )
            {
                SendErrorToClient(senderClientId, "No NPC selected.");
                return;
            }

            request.npcSlug = selectedNpcSlug;
            LogRoutingEvent(
                senderClientId,
                request.requestId,
                NPCFlowStatus.Start,
                $"Received dialogue request for NPC '{request.npcSlug}'.",
                new Dictionary<string, object>
                {
                    ["messageLength"] = request.playerMessage?.Length ?? 0,
                }
            );

            if (_activeClientId.HasValue || DialogueManager.IsResponding)
            {
                EnqueueDialogueRequest(senderClientId, request);
                return;
            }

            await BeginDialogueRequestAsync(senderClientId, request);
        }

        [Rpc(SendTo.Server)]
        void CancelActiveRequestServerRpc(RpcParams rpcParams = default)
        {
            if (
                _activeClientId.HasValue
                && _activeClientId.Value == rpcParams.Receive.SenderClientId
            )
            {
                DialogueManager?.CancelRequests();
                _activeClientId = null;
                _activeRequestId = string.Empty;
                TryProcessNextQueuedRequest();
                return;
            }

            RemoveQueuedRequestsForClient(rpcParams.Receive.SenderClientId);
        }

        // ── Client RPC Senders ──────────────────────────────────────────

        void SendNpcChangedToClient(ulong clientId, string npcSlug, string displayName)
        {
            var payload = new NPCDialogueResponseMessage
            {
                requestId = string.Empty,
                npcSlug = npcSlug,
                displayName = displayName,
                content = string.Empty,
            };
            payload.SanitizeInPlace();
            ReceiveNpcChangedClientRpc(payload, RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }

        void SendResponseStartToClient(ulong clientId, NPCDialogueResponseMessage payload)
        {
            payload.SanitizeInPlace();
            ReceiveResponseStartClientRpc(payload, RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }

        void SendResponseUpdatedToClient(ulong clientId, NPCDialogueResponseMessage payload)
        {
            payload.SanitizeInPlace();
            ReceiveResponseUpdatedClientRpc(payload, RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }

        void SendResponseCompleteToClient(ulong clientId, NPCDialogueResponseMessage payload)
        {
            payload.SanitizeInPlace();
            ReceiveResponseCompleteClientRpc(
                payload,
                RpcTarget.Single(clientId, RpcTargetUse.Temp)
            );
        }

        void SendErrorToClient(ulong clientId, string error)
        {
            string normalizedError = string.IsNullOrWhiteSpace(error)
                ? "Unknown dialogue error."
                : error.Trim();
            NPCFlowLogger
                .FindOrCreate()
                ?.Log(
                    NPCFlowStage.DialogueRouting,
                    NPCFlowStatus.Error,
                    NPCFlowLogLevel.Error,
                    $"Sending dialogue error to client {clientId}: {normalizedError}",
                    source: nameof(NPCDialogueNetworkBridge),
                    requestId: _activeRequestId,
                    data: new Dictionary<string, object>
                    {
                        ["clientId"] = clientId,
                        ["error"] = normalizedError,
                    }
                );
            ReceiveErrorClientRpc(normalizedError, RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }

        void SendNotebookStateToClient(ulong clientId, NPCNotebookStateMessage payload)
        {
            payload.SanitizeInPlace();
            ReceiveNotebookStateClientRpc(payload, RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }

        // ── Client RPC Receivers ────────────────────────────────────────

        [Rpc(SendTo.SpecifiedInParams)]
        void ReceiveNpcChangedClientRpc(
            NPCDialogueResponseMessage payload,
            RpcParams rpcParams = default
        )
        {
            _localSelectedNpcSlug = payload.npcSlug;
            OnNpcChanged?.Invoke(payload.displayName);
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void ReceiveResponseStartClientRpc(
            NPCDialogueResponseMessage payload,
            RpcParams rpcParams = default
        )
        {
            OnResponseStart?.Invoke(payload.content);
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void ReceiveResponseUpdatedClientRpc(
            NPCDialogueResponseMessage payload,
            RpcParams rpcParams = default
        )
        {
            OnResponseUpdated?.Invoke(payload.content);
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void ReceiveResponseCompleteClientRpc(
            NPCDialogueResponseMessage payload,
            RpcParams rpcParams = default
        )
        {
            _localSelectedNpcSlug = payload.npcSlug;
            OnResponseComplete?.Invoke(payload.displayName, payload.content);
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void ReceiveErrorClientRpc(string error, RpcParams rpcParams = default)
        {
            OnError?.Invoke(error);
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void ReceiveNotebookStateClientRpc(
            NPCNotebookStateMessage payload,
            RpcParams rpcParams = default
        )
        {
            _currentNotebookState = payload;
            _localSelectedNpcSlug = string.IsNullOrWhiteSpace(payload.npcSlug)
                ? _localSelectedNpcSlug
                : payload.npcSlug;
            OnNotebookStateChanged?.Invoke(payload);
        }

        // ── Logging ─────────────────────────────────────────────────────

        void LogRoutingEvent(
            ulong clientId,
            string requestId,
            NPCFlowStatus status,
            string message,
            Dictionary<string, object> data = null
        )
        {
            lastRoutingStatus = message;
            data ??= new Dictionary<string, object>();
            data["clientId"] = clientId;
            data["activeClientId"] = _activeClientId.HasValue ? _activeClientId.Value : 0ul;
            data["pendingRequestCount"] = _pendingRequests.Count;
            NPCFlowLogger
                .FindOrCreate()
                ?.Log(
                    NPCFlowStage.DialogueRouting,
                    status,
                    status == NPCFlowStatus.Error ? NPCFlowLogLevel.Error
                        : status == NPCFlowStatus.Warning ? NPCFlowLogLevel.Warning
                        : NPCFlowLogLevel.Info,
                    message,
                    source: nameof(NPCDialogueNetworkBridge),
                    requestId: requestId,
                    npcSlug: DialogueManager != null && DialogueManager.currentProfile != null
                        ? DialogueManager.currentProfile.GetNpcSlug()
                        : _localSelectedNpcSlug,
                    data: data
                );
        }

        void LogClientSessionEvent(ulong clientId, NPCFlowStatus status, string message)
        {
            lastRoutingStatus = message;
            NPCFlowLogger
                .FindOrCreate()
                ?.Log(
                    NPCFlowStage.ClientSession,
                    status,
                    status == NPCFlowStatus.Warning
                        ? NPCFlowLogLevel.Warning
                        : NPCFlowLogLevel.Info,
                    message,
                    source: nameof(NPCDialogueNetworkBridge),
                    data: new Dictionary<string, object>
                    {
                        ["clientId"] = clientId,
                        ["playerName"] =
                            SessionManager != null
                                ? SessionManager.GetPlayerDisplayName(clientId)
                                : string.Empty,
                    }
                );
        }
    }
}
