using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
#if !UNITY_SERVER
using UnityEngine.InputSystem;
#endif

namespace NPCSystem
{
    [DefaultExecutionOrder(-340)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NPCPlayerInventory))]
    public sealed class NPCNetworkItemInteractor : NetworkBehaviour
    {
        [Header("References")]
        public NPCPlayerInventory inventory;

        [Header("Actions")]
        public string interactActionName = "Interact";
        public string giveToPlayerActionName = "Previous";
        public string giveToNpcActionName = "Next";

        [Header("Range")]
        public float pickupRange = 3f;
        public float transferRange = 4f;

#if !UNITY_SERVER
        InputAction _interactAction;
        InputAction _giveToPlayerAction;
        InputAction _giveToNpcAction;
        NPCNetworkPlayerController _playerController;
#endif

        void Awake()
        {
            inventory = inventory != null ? inventory : GetComponent<NPCPlayerInventory>();
#if !UNITY_SERVER
            _playerController = GetComponent<NPCNetworkPlayerController>();
#endif
        }

        void Update()
        {
            if (!IsOwner)
            {
                return;
            }

#if !UNITY_SERVER
            ResolveActions();

            if (_interactAction != null && _interactAction.WasPressedThisFrame())
            {
                RequestPickupNearestItemServerRpc();
            }

            if (_giveToPlayerAction != null && _giveToPlayerAction.WasPressedThisFrame())
            {
                RequestGiveHeldItemToNearestPlayerServerRpc();
            }

            if (_giveToNpcAction != null && _giveToNpcAction.WasPressedThisFrame())
            {
                RequestGiveHeldItemToNearestNpcServerRpc();
            }
#endif
        }

        [Rpc(SendTo.Server)]
        void RequestPickupNearestItemServerRpc(RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            NPCTransferableItem item = FindNearestPickupCandidate(clientId);
            if (item == null)
            {
                LogInteractorEvent(clientId, NPCFlowStatus.Skipped, "No pickup candidate in range.");
                return;
            }

            TransferItemToPlayer(item, clientId);
        }

        [Rpc(SendTo.Server)]
        void RequestGiveHeldItemToNearestPlayerServerRpc(RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            NPCTransferableItem item = FindHeldItem(clientId);
            NPCPlayerNetworkAvatar targetPlayer = FindNearestOtherPlayer(clientId);
            if (item == null || targetPlayer == null)
            {
                LogInteractorEvent(clientId, NPCFlowStatus.Skipped, "Give-to-player skipped because no held item or target player was found.");
                return;
            }

            TransferItemToPlayer(item, targetPlayer.OwnerClientId);
        }

        [Rpc(SendTo.Server)]
        void RequestGiveHeldItemToNearestNpcServerRpc(RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            NPCTransferableItem item = FindHeldItem(clientId);
            NPCServerCharacter npc = FindNearestNpc(clientId);
            if (item == null || npc == null)
            {
                LogInteractorEvent(clientId, NPCFlowStatus.Skipped, "Give-to-NPC skipped because no held item or NPC was found.");
                return;
            }

            RemoveItemFromPlayer(item, clientId);
            item.AssignToNpc(npc.Slug);
        }

        void TransferItemToPlayer(NPCTransferableItem item, ulong targetClientId)
        {
            ulong previousOwnerClientId = item.OwnerClientId;
            bool hadPlayerOwner = item.IsHeldByPlayer;

            if (hadPlayerOwner)
            {
                RemoveItemFromPlayer(item, previousOwnerClientId);
            }

            item.AssignToPlayer(targetClientId);
            AddItemToPlayer(item, targetClientId);
        }

        void AddItemToPlayer(NPCTransferableItem item, ulong clientId)
        {
            NPCPlayerInventory targetInventory = FindInventory(clientId);
            targetInventory?.ServerTryAddItem(item.ItemId);

            NPCNetworkSessionManager sessionManager = FindAnyObjectByType<NPCNetworkSessionManager>(FindObjectsInactive.Include);
            sessionManager?.AddInventoryItem(clientId, item.ItemId);

            NPCDialogueNetworkBridge bridge = FindAnyObjectByType<NPCDialogueNetworkBridge>(FindObjectsInactive.Include);
            bridge?.RefreshNotebookStateForClient(clientId);

            LogInteractorEvent(clientId, NPCFlowStatus.Success, $"Recorded item '{item.ItemId}' in player inventory.");
        }

        void RemoveItemFromPlayer(NPCTransferableItem item, ulong clientId)
        {
            NPCPlayerInventory sourceInventory = FindInventory(clientId);
            sourceInventory?.ServerTryRemoveItem(item.ItemId);

            NPCNetworkSessionManager sessionManager = FindAnyObjectByType<NPCNetworkSessionManager>(FindObjectsInactive.Include);
            sessionManager?.RemoveInventoryItem(clientId, item.ItemId);

            NPCDialogueNetworkBridge bridge = FindAnyObjectByType<NPCDialogueNetworkBridge>(FindObjectsInactive.Include);
            bridge?.RefreshNotebookStateForClient(clientId);
        }

        NPCTransferableItem FindNearestPickupCandidate(ulong clientId)
        {
            NPCPlayerNetworkAvatar avatar = FindPlayerAvatar(clientId);
            if (avatar == null)
            {
                return null;
            }

            NPCTransferableItem nearestItem = null;
            float nearestDistance = pickupRange;
            NPCTransferableItem[] items = FindObjectsByType<NPCTransferableItem>(FindObjectsInactive.Include);
            foreach (NPCTransferableItem item in items)
            {
                if (item == null || !item.IsSpawned || item.IsHeldByPlayer)
                {
                    continue;
                }

                float distance = Vector3.Distance(avatar.transform.position, item.transform.position);
                if (distance <= nearestDistance)
                {
                    nearestDistance = distance;
                    nearestItem = item;
                }
            }

            return nearestItem;
        }

        NPCTransferableItem FindHeldItem(ulong clientId)
        {
            NPCTransferableItem[] items = FindObjectsByType<NPCTransferableItem>(FindObjectsInactive.Include);
            foreach (NPCTransferableItem item in items)
            {
                if (item != null && item.IsSpawned && item.IsHeldByPlayer && item.OwnerClientId == clientId)
                {
                    return item;
                }
            }

            return null;
        }

        NPCPlayerNetworkAvatar FindNearestOtherPlayer(ulong clientId)
        {
            NPCPlayerNetworkAvatar sourceAvatar = FindPlayerAvatar(clientId);
            if (sourceAvatar == null)
            {
                return null;
            }

            NPCPlayerNetworkAvatar nearestPlayer = null;
            float nearestDistance = transferRange;
            NPCPlayerNetworkAvatar[] avatars = FindObjectsByType<NPCPlayerNetworkAvatar>(FindObjectsInactive.Include);
            foreach (NPCPlayerNetworkAvatar avatar in avatars)
            {
                if (avatar == null || avatar.OwnerClientId == clientId || !avatar.IsSpawned)
                {
                    continue;
                }

                float distance = Vector3.Distance(sourceAvatar.transform.position, avatar.transform.position);
                if (distance <= nearestDistance)
                {
                    nearestDistance = distance;
                    nearestPlayer = avatar;
                }
            }

            return nearestPlayer;
        }

        NPCServerCharacter FindNearestNpc(ulong clientId)
        {
            NPCPlayerNetworkAvatar sourceAvatar = FindPlayerAvatar(clientId);
            if (sourceAvatar == null)
            {
                return null;
            }

            NPCServerCharacter nearestNpc = null;
            float nearestDistance = transferRange;
            NPCServerCharacter[] npcs = FindObjectsByType<NPCServerCharacter>(FindObjectsInactive.Include);
            foreach (NPCServerCharacter npc in npcs)
            {
                if (npc == null || !npc.IsSpawned)
                {
                    continue;
                }

                float distance = Vector3.Distance(sourceAvatar.transform.position, npc.transform.position);
                if (distance <= nearestDistance)
                {
                    nearestDistance = distance;
                    nearestNpc = npc;
                }
            }

            return nearestNpc;
        }

        NPCPlayerInventory FindInventory(ulong clientId)
        {
            NPCPlayerNetworkAvatar avatar = FindPlayerAvatar(clientId);
            return avatar != null ? avatar.GetComponent<NPCPlayerInventory>() : null;
        }

        NPCPlayerNetworkAvatar FindPlayerAvatar(ulong clientId)
        {
            NPCPlayerNetworkAvatar[] avatars = FindObjectsByType<NPCPlayerNetworkAvatar>(FindObjectsInactive.Include);
            foreach (NPCPlayerNetworkAvatar avatar in avatars)
            {
                if (avatar != null && avatar.IsSpawned && avatar.OwnerClientId == clientId)
                {
                    return avatar;
                }
            }

            return null;
        }

#if !UNITY_SERVER
        void ResolveActions()
        {
            if (_playerController == null || _playerController.inputActions == null)
            {
                return;
            }

            InputActionMap map = _playerController.inputActions.FindActionMap(_playerController.actionMapName, false);
            _interactAction ??= map?.FindAction(interactActionName, false);
            _giveToPlayerAction ??= map?.FindAction(giveToPlayerActionName, false);
            _giveToNpcAction ??= map?.FindAction(giveToNpcActionName, false);
        }
#endif

        void LogInteractorEvent(ulong clientId, NPCFlowStatus status, string message)
        {
            NPCFlowLogger.FindOrCreate()?.Log(NPCFlowStage.OwnershipAuthority, status, NPCFlowLogLevel.Info,
                message,
                source: nameof(NPCNetworkItemInteractor),
                data: new Dictionary<string, object>
                {
                    ["clientId"] = clientId
                });
        }
    }
}
