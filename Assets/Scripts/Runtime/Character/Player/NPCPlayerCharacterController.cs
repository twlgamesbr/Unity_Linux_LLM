using NPCSystem.Auth;
using NPCSystem.Character.Animation;
using NPCSystem.Character.NPC;
using NPCSystem.Character.Player;
using NPCSystem.Dialogue.Core;
using NPCSystem.Dialogue.Persistence;
using NPCSystem.Dialogue.RAG;
using NPCSystem.Dialogue.Session;
using NPCSystem.Dialogue.UI;
using NPCSystem.Initialization;
using NPCSystem.Items;
using NPCSystem.LocalAI;
using NPCSystem.Monitoring;
using NPCSystem.Monitoring.Datadog;
using NPCSystem.Network.Bridges;
using NPCSystem.Network.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace NPCSystem.Character.Player
{
    /// <summary>
    /// Modern multiplayer character controller orchestrator.
    /// Replaces the monolithic NPCNetworkPlayerController with modular subsystems:
    /// - NPCMultiplayerInputActions: clean InputSystem wrapper with cached actions
    /// - NPCCharacterMotor: physics-based movement (FixedUpdate)
    /// - NPCCharacterAnimatorBridge: animator driving
    /// - NPCThirdPersonCameraController: orbit camera (separate component)
    /// - NPCNetworkItemInteractor: interaction routing (existing, preserved)
    ///
    /// Network-aware: only the owning client reads input and drives camera.
    /// Non-owners receive transform replication via NPCOwnerNetworkTransform.
    /// </summary>
    [DefaultExecutionOrder(-350)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(NPCOwnerNetworkTransform))]
    [RequireComponent(typeof(NPCMultiplayerInputActions))]
    [RequireComponent(typeof(NPCCharacterMotor))]
    [RequireComponent(typeof(NPCCharacterAnimatorBridge))]
    [RequireComponent(typeof(NPCNetworkAnimatorState))]
    public sealed class NPCPlayerCharacterController : NetworkBehaviour
    {
        [Header("References")]
        [FormerlySerializedAs("inputHandler")]
        [SerializeField]
        private NPCMultiplayerInputActions _inputHandler;

        [FormerlySerializedAs("motor")]
        [SerializeField]
        private NPCCharacterMotor _motor;

        [FormerlySerializedAs("animBridge")]
        [SerializeField]
        private NPCCharacterAnimatorBridge _animBridge;

        [FormerlySerializedAs("networkAnimState")]
        [SerializeField]
        private NPCNetworkAnimatorState _networkAnimState;

        [FormerlySerializedAs("cameraController")]
        [SerializeField]
        private NPCThirdPersonCameraController _cameraController;

        [FormerlySerializedAs("interactor")]
        [SerializeField]
        private NPCNetworkItemInteractor _interactor;

        [Header("Input Asset")]
        [FormerlySerializedAs("inputActions")]
        [SerializeField]
        private InputActionAsset _inputActions;

        [Header("Owner Settings")]
        [FormerlySerializedAs("LockCursorForOwner")]
        [SerializeField]
        private bool _lockCursorForOwner = true;

        // ─── Singleton-style access for the owning client ───
        public static NPCPlayerCharacterController LocalInstance { get; private set; }

        public static bool PendingUIModeRequest { get; private set; }

        [Header("Runtime Status")]
        public bool IsInUIMode { get; private set; }

        public static void RequestUIActive()
        {
            PendingUIModeRequest = true;
            LocalInstance?.SetUIActive(true);
        }

        public static void ClearUIActiveRequest()
        {
            PendingUIModeRequest = false;
        }

        bool _eventsSubscribed;

        // ─── Lifecycle ───

        void Reset() => ResolveReferences();

        void Awake()
        {
            ResolveReferences();
            SubscribeInputEvents();
        }

        public override void OnDestroy()
        {
            UnsubscribeInputEvents();
            base.OnDestroy();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsOwner)
            {
                LocalInstance = this;
                if (PendingUIModeRequest)
                {
                    SetUIActive(true);
                }
                else if (IsInUIMode)
                {
                    SetUIActive(true);
                }
                else
                {
                    EnableOwnerInput();
                }

                if (_cameraController != null)
                    _cameraController.StartFollowing();
            }
            else
            {
                DisableAllInput();
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner && LocalInstance == this)
                LocalInstance = null;
            DisableAllInput();
            if (IsOwner && _cameraController != null)
                _cameraController.StopFollowing();
            base.OnNetworkDespawn();
        }

        void OnEnable()
        {
            if (!Application.isPlaying || (IsSpawned && IsOwner))
            {
                if (IsInUIMode)
                {
                    SetUIActive(true);
                }
                else
                {
                    EnableOwnerInput();
                }
            }
        }

        void OnDisable()
        {
            DisableAllInput();
        }

        void Update()
        {
            if (!IsOwner || _motor == null)
                return;

            _motor.MoveInput = _inputHandler != null ? _inputHandler.MoveInput : Vector2.zero;
            _motor.SprintInput = _inputHandler != null && _inputHandler.SprintHeld;
        }

        // ─── Input Event Wiring ───

        void SubscribeInputEvents()
        {
            if (_eventsSubscribed || _inputHandler == null)
                return;

            _inputHandler.OnJump += HandleJump;
            _inputHandler.OnInteract += HandleInteract;
            _inputHandler.OnPrevious += HandleGiveToPlayer;
            _inputHandler.OnNext += HandleGiveToNpc;
            _inputHandler.OnCrouch += HandleCrouch;
            _inputHandler.OnToggleUI += HandleToggleUI;

            _eventsSubscribed = true;
        }

        void UnsubscribeInputEvents()
        {
            if (!_eventsSubscribed || _inputHandler == null)
                return;

            _inputHandler.OnJump -= HandleJump;
            _inputHandler.OnInteract -= HandleInteract;
            _inputHandler.OnPrevious -= HandleGiveToPlayer;
            _inputHandler.OnNext -= HandleGiveToNpc;
            _inputHandler.OnCrouch -= HandleCrouch;
            _inputHandler.OnToggleUI -= HandleToggleUI;

            _eventsSubscribed = false;
        }

        void HandleJump()
        {
            if (!IsOwner || _motor == null)
                return;
            _motor.RequestJump();
            _networkAnimState?.NotifyJumpTriggered();
        }

        void HandleInteract()
        {
            if (!IsOwner || _interactor == null)
                return;
            _interactor.RequestPickupNearestItemServerRpc();
        }

        void HandleGiveToPlayer()
        {
            if (!IsOwner || _interactor == null)
                return;
            _interactor.RequestGiveHeldItemToNearestPlayerServerRpc();
        }

        void HandleGiveToNpc()
        {
            if (!IsOwner || _interactor == null)
                return;
            _interactor.RequestGiveHeldItemToNearestNpcServerRpc();
        }

        void HandleCrouch()
        {
            // Reserved for crouch toggle — extend when crouch mechanic is added
        }

        void HandleToggleUI()
        {
            if (!IsOwner)
                return;
            SetUIActive(!IsInUIMode);
        }

        // ─── Input Management ───

        /// <summary>Public entry point for external systems to toggle UI mode.</summary>
        public void ToggleUIMode()
        {
            if (!IsOwner)
                return;
            SetUIActive(!IsInUIMode);
        }

        void EnableOwnerInput()
        {
            if (_inputHandler != null)
            {
                if (_inputHandler.InputActions == null && _inputActions != null)
                    _inputHandler.InputActions = _inputActions;

                _inputHandler.EnableActions();
            }

            if (_lockCursorForOwner)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        void DisableAllInput()
        {
            if (_inputHandler != null)
                _inputHandler.DisableAll();

            if (_lockCursorForOwner && IsOwner)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        public void SetUIActive(bool active)
        {
            if (_inputHandler == null)
                return;

            if (active)
            {
                _inputHandler.DisableActions();
                _inputHandler.EnableUIActions();
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                ClearUIActiveRequest();
            }
            else
            {
                _inputHandler.DisableUIActions();
                _inputHandler.EnableActions();
                if (_lockCursorForOwner)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }

            IsInUIMode = active;

            // ── Datadog: player toggled between Gameplay ↔ UI/Dialogue mode ──
            DatadogMetricsService.Increment(
                "input.mode_switch",
                tags: new[] { $"mode:{(active ? "ui" : "gameplay")}" }
            );
        }

        void ResolveReferences()
        {
            if (_inputHandler == null)
                _inputHandler = GetComponent<NPCMultiplayerInputActions>();
            if (_motor == null)
                _motor = GetComponent<NPCCharacterMotor>();
            if (_animBridge == null)
                _animBridge = GetComponent<NPCCharacterAnimatorBridge>();
            if (_networkAnimState == null)
                _networkAnimState = GetComponent<NPCNetworkAnimatorState>();
            if (_cameraController == null)
                _cameraController = GetComponent<NPCThirdPersonCameraController>();
            if (_interactor == null)
                _interactor = GetComponent<NPCNetworkItemInteractor>();
        }

        // ─── Public API ───

        public NPCCharacterMotor Motor => _motor;
        public NPCMultiplayerInputActions InputHandler => _inputHandler;
        public NPCThirdPersonCameraController CameraController => _cameraController;
    }
}
