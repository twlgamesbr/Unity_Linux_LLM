using System.Collections.Generic;
using NPCSystem.Auth;
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
using NPCSystem.Network.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace NPCSystem.Character.Player
{
    [DefaultExecutionOrder(-350)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(CharacterController))]
    public class NPCNetworkPlayerController : NetworkBehaviour
    {
        static readonly int MoveXHash = Animator.StringToHash("MoveX");
        static readonly int MoveYHash = Animator.StringToHash("MoveY");
        static readonly int SpeedHash = Animator.StringToHash("Speed");
        static readonly int GroundedHash = Animator.StringToHash("Grounded");
        static readonly int SprintingHash = Animator.StringToHash("Sprinting");
        static readonly int JumpHash = Animator.StringToHash("Jump");

        enum AnimatorState
        {
            Idle,
            Walking,
            Sprinting,
        }

        public CharacterController characterController;

        public Animator animator;

        public Transform cameraFollowTarget;

        public PlayerInput playerInput;

        public InputActionAsset inputActions;

        [FormerlySerializedAs("ActionMapName")]
        public string ActionMapName = "Player";

        [FormerlySerializedAs("MoveActionName")]
        public string MoveActionName = "Move";

        [FormerlySerializedAs("LookActionName")]
        public string LookActionName = "Look";

        [FormerlySerializedAs("JumpActionName")]
        public string JumpActionName = "Jump";

        [FormerlySerializedAs("SprintActionName")]
        public string SprintActionName = "Sprint";

        [FormerlySerializedAs("WalkSpeed")]
        public float WalkSpeed = 3.5f;

        [FormerlySerializedAs("SprintSpeed")]
        public float SprintSpeed = 6.0f;

        [FormerlySerializedAs("RotationSpeed")]
        public float RotationSpeed = 720f;

        [FormerlySerializedAs("JumpHeight")]
        public float JumpHeight = 1.25f;

        [FormerlySerializedAs("Gravity")]
        public float Gravity = -24f;

        [FormerlySerializedAs("GroundedStickVelocity")]
        public float GroundedStickVelocity = -2f;

        [FormerlySerializedAs("DriveMainCameraForOwner")]
        public bool DriveMainCameraForOwner = true;

        public Vector3 cameraOffset = new Vector3(0f, 4.5f, -6f);

        [FormerlySerializedAs("CameraFollowSharpness")]
        public float CameraFollowSharpness = 12f;

        [FormerlySerializedAs("LookYawSensitivity")]
        public float LookYawSensitivity = 0.12f;

        [FormerlySerializedAs("LockCursorForOwner")]
        public bool LockCursorForOwner = false;

        [FormerlySerializedAs("UsePlayerInputCopy")]
        public bool UsePlayerInputCopy = true;

        [FormerlySerializedAs("AllowKeyboardFallback")]
        public bool AllowKeyboardFallback = true;

        [FormerlySerializedAs("LogSpawnDiagnostics")]
        public bool LogSpawnDiagnostics = true;

        Vector2 _moveInput;
        Vector2 _lookInput;
        float _verticalVelocity;
        float _yaw;
#if !UNITY_SERVER
        InputAction _moveAction;
        InputAction _lookAction;
        InputAction _jumpAction;
        InputAction _sprintAction;
#endif
        Camera _mainCamera;
#if !UNITY_SERVER
        bool _inputEnabled;
#endif

        public bool HasInputAuthority => !Application.isPlaying || IsOwner;

        void Reset()
        {
            ResolveReferences();
        }

        void Awake()
        {
            ResolveReferences();
            _yaw = transform.eulerAngles.y;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            ResolveReferences();

            LogAuthorityEvent(NPCFlowStatus.Start, "Network player controller spawned.");

            if (IsOwner)
            {
#if !UNITY_SERVER
                EnableInput();
#endif
                if (LogSpawnDiagnostics)
                {
#if !UNITY_SERVER
                    string resolvedMoveAction = _moveAction != null ? _moveAction.name : "missing";
#else
                    string resolvedMoveAction = "missing";
#endif
                    LogAuthorityEvent(
                        NPCFlowStatus.Success,
                        "Owner input enabled for spawned player controller.",
                        new Dictionary<string, object>
                        {
                            ["localClientId"] =
                                NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0ul,
                            ["actionMap"] = ActionMapName,
                            ["moveAction"] = resolvedMoveAction,
                        }
                    );
                }

                if (LockCursorForOwner)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
            else
            {
#if !UNITY_SERVER
                if (playerInput != null)
                {
                    playerInput.DeactivateInput();
                }

                DisableInput();
#endif
                LogAuthorityEvent(NPCFlowStatus.Success, "Spawned non-owner controller; local input disabled.");
            }
        }

        public override void OnNetworkDespawn()
        {
#if !UNITY_SERVER
            DisableInput();
#endif
            LogAuthorityEvent(NPCFlowStatus.Warning, "Network player controller despawned.");
            base.OnNetworkDespawn();
        }

        void OnEnable()
        {
            if (!Application.isPlaying || (IsSpawned && IsOwner))
            {
#if !UNITY_SERVER
                EnableInput();
#endif
            }
        }

        void OnDisable()
        {
#if !UNITY_SERVER
            DisableInput();
#endif
        }

        void Update()
        {
            if (!HasInputAuthority)
            {
#if !UNITY_SERVER
                UpdateAnimator(Vector2.zero, false, IsGrounded());
#else
                // Server: no input-driven animation; skip
#endif
                return;
            }

#if !UNITY_SERVER
            ReadInput();
#endif
            MoveCharacter(Time.deltaTime);
            UpdateOwnerCamera(Time.deltaTime);
        }

        public void ResolveReferences()
        {
            if (characterController == null)
            {
                characterController = GetComponent<CharacterController>();
            }

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>(true);
            }

#if !UNITY_SERVER
            if (playerInput == null)
            {
                playerInput = GetComponent<PlayerInput>();
            }
#endif

            if (cameraFollowTarget == null)
            {
                cameraFollowTarget = transform;
            }
        }

        void LogAuthorityEvent(NPCFlowStatus status, string message, Dictionary<string, object> data = null)
        {
            data ??= new Dictionary<string, object>();
            data["ownerClientId"] = OwnerClientId;
            data["isOwner"] = IsOwner;
            data["isServer"] = IsServer;
            data["isSpawned"] = IsSpawned;
            NPCFlowLogger
                .FindOrCreate()
                ?.Log(
                    NPCFlowStage.OwnershipAuthority,
                    status,
                    status == NPCFlowStatus.Warning ? NPCFlowLogLevel.Warning : NPCFlowLogLevel.Info,
                    message,
                    source: nameof(NPCNetworkPlayerController),
                    data: data
                );
        }

#if !UNITY_SERVER
        void EnableInput()
        {
            if (UsePlayerInputCopy && playerInput != null)
            {
                if (!string.IsNullOrWhiteSpace(ActionMapName))
                {
                    playerInput.defaultActionMap = ActionMapName;
                }

                playerInput.ActivateInput();
                if (!string.IsNullOrWhiteSpace(ActionMapName))
                {
                    playerInput.SwitchCurrentActionMap(ActionMapName);
                }

                if (playerInput.actions != null)
                {
                    inputActions = playerInput.actions;
                }
            }

            ResolveInputActions();
            _moveAction?.Enable();
            _lookAction?.Enable();
            _jumpAction?.Enable();
            _sprintAction?.Enable();
            _inputEnabled = true;
        }

        void DisableInput()
        {
            if (!_inputEnabled)
                return;
            _moveAction?.Disable();
            _lookAction?.Disable();
            _jumpAction?.Disable();
            _sprintAction?.Disable();
            _inputEnabled = false;
        }

        void ResolveInputActions()
        {
            if (inputActions == null)
                return;
            InputActionMap map = inputActions.FindActionMap(ActionMapName, false);
            _moveAction = map?.FindAction(MoveActionName, false);
            _lookAction = map?.FindAction(LookActionName, false);
            _jumpAction = map?.FindAction(JumpActionName, false);
            _sprintAction = map?.FindAction(SprintActionName, false);
        }

        void ReadInput()
        {
            _moveInput =
                _moveAction != null ? Vector2.ClampMagnitude(_moveAction.ReadValue<Vector2>(), 1f) : Vector2.zero;
            _lookInput = _lookAction != null ? _lookAction.ReadValue<Vector2>() : Vector2.zero;

            if (AllowKeyboardFallback && _moveInput.sqrMagnitude < 0.0001f)
            {
                Vector2 fallbackMove = Vector2.zero;
                Keyboard keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                        fallbackMove.y += 1f;
                    if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                        fallbackMove.y -= 1f;
                    if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                        fallbackMove.x += 1f;
                    if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                        fallbackMove.x -= 1f;
                }

                Gamepad gamepad = Gamepad.current;
                if (gamepad != null)
                {
                    fallbackMove += gamepad.leftStick.ReadValue();
                }

                _moveInput = Vector2.ClampMagnitude(fallbackMove, 1f);
            }
        }
#endif // !UNITY_SERVER

        void MoveCharacter(float deltaTime)
        {
            if (characterController == null)
                return;

            bool grounded = IsGrounded();
            if (grounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = GroundedStickVelocity;
            }

#if !UNITY_SERVER
            if (grounded && _jumpAction != null && _jumpAction.WasPressedThisFrame())
            {
                _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
                if (animator != null)
                {
                    animator.SetTrigger(JumpHash);
                }
            }

            bool sprinting =
                (_sprintAction != null && _sprintAction.IsPressed())
                || (
                    AllowKeyboardFallback
                    && Keyboard.current != null
                    && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed)
                );
#else
            bool sprinting = false;
#endif
            float targetSpeed = sprinting ? SprintSpeed : WalkSpeed;
            Vector3 planarMove = new Vector3(_moveInput.x, 0f, _moveInput.y);
            planarMove = Vector3.ClampMagnitude(planarMove, 1f);

            if (planarMove.sqrMagnitude > 0.0001f)
            {
                Vector3 cameraForward = CameraPlanarForward();
                Vector3 cameraRight = Vector3.Cross(Vector3.up, cameraForward).normalized;
                Vector3 worldMove = (cameraForward * planarMove.z + cameraRight * planarMove.x).normalized;
                Quaternion targetRotation = Quaternion.LookRotation(worldMove, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRotation,
                    RotationSpeed * deltaTime
                );
                planarMove = worldMove;
            }

            _verticalVelocity += Gravity * deltaTime;
            Vector3 velocity = planarMove * targetSpeed + Vector3.up * _verticalVelocity;
            characterController.Move(velocity * deltaTime);
#if !UNITY_SERVER
            UpdateAnimator(_moveInput, sprinting, characterController.isGrounded);
#endif
        }

        bool IsGrounded()
        {
            return characterController != null && characterController.isGrounded;
        }

        Vector3 CameraPlanarForward()
        {
            Camera cam = _mainCamera != null ? _mainCamera : Camera.main;
            if (cam == null)
                return Vector3.forward;
            Vector3 forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
            return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        }

        void UpdateOwnerCamera(float deltaTime)
        {
            if (!DriveMainCameraForOwner)
                return;
            _mainCamera ??= Camera.main;
            if (_mainCamera == null || cameraFollowTarget == null)
                return;

            _yaw += _lookInput.x * LookYawSensitivity;
            Quaternion cameraYaw = Quaternion.Euler(0f, _yaw, 0f);
            Vector3 targetPosition = cameraFollowTarget.position + cameraYaw * cameraOffset;
            float t = 1f - Mathf.Exp(-CameraFollowSharpness * deltaTime);
            _mainCamera.transform.position = Vector3.Lerp(_mainCamera.transform.position, targetPosition, t);
            _mainCamera.transform.LookAt(cameraFollowTarget.position + Vector3.up * 1.25f);
        }

#if !UNITY_SERVER
        void UpdateAnimator(Vector2 move, bool sprinting, bool grounded)
        {
            if (animator == null)
                return;
            float speed01 = Mathf.Clamp01(move.magnitude) * (sprinting ? 1f : 0.6f);
            animator.SetFloat(MoveXHash, move.x);
            animator.SetFloat(MoveYHash, move.y);
            animator.SetFloat(SpeedHash, speed01);
            animator.SetBool(GroundedHash, grounded);
            animator.SetBool(SprintingHash, sprinting);
        }
#endif
    }
}
