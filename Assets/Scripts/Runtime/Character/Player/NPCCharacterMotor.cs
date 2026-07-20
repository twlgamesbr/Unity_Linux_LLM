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
using UnityEngine;
using UnityEngine.Serialization;

namespace NPCSystem.Character.Player
{
    /// <summary>
    /// Physics-based character movement using CharacterController.
    /// Runs in FixedUpdate. Receives input from an external source (e.g. NPCMultiplayerInputActions).
    /// No input reading, no network logic \u2014 pure movement.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class NPCCharacterMotor : MonoBehaviour
    {
        CharacterController _controller;

        [Header("Movement")]
        [FormerlySerializedAs("walkSpeed")]
        [SerializeField]
        private float _walkSpeed = 3.5f;

        [FormerlySerializedAs("sprintSpeed")]
        [SerializeField]
        private float _sprintSpeed = 6.0f;

        [FormerlySerializedAs("rotationSpeed")]
        [SerializeField]
        private float _rotationSpeed = 720f;

        [FormerlySerializedAs("acceleration")]
        [SerializeField]
        private float _acceleration = 12f;

        [FormerlySerializedAs("deceleration")]
        [SerializeField]
        private float _deceleration = 10f;

        [Header("Jump / Gravity")]
        [FormerlySerializedAs("jumpHeight")]
        [SerializeField]
        private float _jumpHeight = 1.25f;

        [FormerlySerializedAs("gravity")]
        [SerializeField]
        private float _gravity = -24f;

        [FormerlySerializedAs("groundedStickVelocity")]
        [SerializeField]
        private float _groundedStickVelocity = -2f;

        [Header("Ground Check")]
        [FormerlySerializedAs("groundCheckDistance")]
        [SerializeField]
        private float _groundCheckDistance = 0.15f;

        [FormerlySerializedAs("groundLayers")]
        [SerializeField]
        private LayerMask _groundLayers = ~0;

        // \u2500\u2500\u2500 Runtime state \u2500\u2500\u2500
        Vector2 _moveInput;
        bool _sprintInput;
        bool _jumpRequested;
        Vector3 _currentVelocity;
        float _currentSpeed;
        float _verticalVelocity;
        bool _grounded;
        bool _wasGrounded;

        // \u2500\u2500\u2500 Public API \u2500\u2500\u2500

        /// <summary>Normalized movement input (-1..1 on each axis).</summary>
        public Vector2 MoveInput
        {
            get => _moveInput;
            set => _moveInput = Vector2.ClampMagnitude(value, 1f);
        }

        /// <summary>Whether sprint is held.</summary>
        public bool SprintInput
        {
            get => _sprintInput;
            set => _sprintInput = value;
        }

        /// <summary>Call once per press to request a jump.</summary>
        public void RequestJump() => _jumpRequested = true;

        /// <summary>Current horizontal velocity magnitude.</summary>
        public float CurrentSpeed => new Vector3(_currentVelocity.x, 0, _currentVelocity.z).magnitude;

        /// <summary>Normalized 0-1 speed ratio relative to the target speed.</summary>
        public float SpeedRatio => CurrentSpeed / TargetSpeed;

        /// <summary>Whether the character is grounded this frame.</summary>
        public bool Grounded => _grounded;

        /// <summary>Whether the character is sprinting (input + grounded).</summary>
        public bool IsSprinting => _sprintInput && _grounded && _moveInput.sqrMagnitude > 0.01f;

        /// <summary>Whether the character just left the ground this frame.</summary>
        public bool JustLeftGround => _wasGrounded && !_grounded;

        public float TargetSpeed => _sprintInput && _grounded ? _sprintSpeed : _walkSpeed;
        public CharacterController CharacterController => _controller;

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _currentVelocity = Vector3.zero;
        }

        void FixedUpdate()
        {
            _wasGrounded = _grounded;
            _grounded = CheckGrounded();

            HandleGravity();
            HandleJump();
            ApplyMovement(Time.fixedDeltaTime);
        }

        void HandleGravity()
        {
            if (_grounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = _groundedStickVelocity;
            }
            _verticalVelocity += _gravity * Time.fixedDeltaTime;
        }

        void HandleJump()
        {
            if (!_jumpRequested)
                return;
            _jumpRequested = false;

            if (!_grounded)
                return;

            _verticalVelocity = Mathf.Sqrt(_jumpHeight * -2f * _gravity);
            _grounded = false;
        }

        void ApplyMovement(float deltaTime)
        {
            // Build target horizontal velocity from input
            Vector3 inputDirection = new Vector3(_moveInput.x, 0f, _moveInput.y);
            if (inputDirection.sqrMagnitude > 0.01f)
            {
                inputDirection = CameraRelativeDirection(inputDirection);

                // Rotate character towards movement direction
                Quaternion targetRotation = Quaternion.LookRotation(inputDirection, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRotation,
                    _rotationSpeed * deltaTime
                );

                _currentSpeed = Mathf.MoveTowards(_currentSpeed, TargetSpeed, _acceleration * deltaTime);
                _currentVelocity = transform.forward * _currentSpeed;
            }
            else
            {
                _currentSpeed = Mathf.MoveTowards(_currentSpeed, 0f, _deceleration * deltaTime);
                _currentVelocity = transform.forward * _currentSpeed;
            }

            Vector3 finalMotion = _currentVelocity + Vector3.up * _verticalVelocity;
            _controller.Move(finalMotion * deltaTime);
        }

        bool CheckGrounded()
        {
            if (_controller.isGrounded)
                return true;

            // Additional raycast ground check for when the controller's built-in isGrounded is unreliable
            Vector3 origin = transform.position + Vector3.up * 0.1f;
            return Physics.Raycast(
                origin,
                Vector3.down,
                _groundCheckDistance,
                _groundLayers,
                QueryTriggerInteraction.Ignore
            );
        }

        Vector3 CameraRelativeDirection(Vector3 inputLocal)
        {
            Camera cam = Camera.main;
            if (cam == null)
                return inputLocal;

            Vector3 forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.forward;

            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            return (forward * inputLocal.z + right * inputLocal.x).normalized;
        }

        void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying)
                return;
            Gizmos.color = _grounded ? Color.green : Color.red;
            Gizmos.DrawRay(transform.position + Vector3.up * 0.1f, Vector3.down * _groundCheckDistance);
        }
    }
}
