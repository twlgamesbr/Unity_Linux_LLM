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

namespace NPCSystem.Character.Animation
{
    /// <summary>
    /// Third-person orbit camera controller.
    /// Separated from movement logic entirely.
    /// Only active for the owning player.
    /// </summary>
    public sealed class NPCThirdPersonCameraController : MonoBehaviour
    {
        [Header("Follow")]
        [FormerlySerializedAs("followTarget")]
        [SerializeField]
        private Transform _followTarget;

        [FormerlySerializedAs("cameraOffset")]
        [SerializeField]
        private Vector3 _cameraOffset = new Vector3(0f, 4.5f, -6f);

        [FormerlySerializedAs("followSharpness")]
        [SerializeField]
        private float _followSharpness = 12f;

        [FormerlySerializedAs("lookTargetOffset")]
        [SerializeField]
        private Vector3 _lookTargetOffset = new Vector3(0f, 1.25f, 0f);

        [Header("Look Sensitivity")]
        [FormerlySerializedAs("yawSensitivity")]
        [SerializeField]
        private float _yawSensitivity = 0.12f;

        [FormerlySerializedAs("pitchSensitivity")]
        [SerializeField]
        private float _pitchSensitivity = 0.08f;

        [FormerlySerializedAs("minPitch")]
        [SerializeField]
        private float _minPitch = -30f;

        [FormerlySerializedAs("maxPitch")]
        [SerializeField]
        private float _maxPitch = 60f;

        [FormerlySerializedAs("invertY")]
        [SerializeField]
        private bool _invertY = false;

        [Header("Input Source")]
        [FormerlySerializedAs("inputSource")]
        [SerializeField]
        private NPCMultiplayerInputActions _inputSource;

        // \u2500\u2500\u2500 Runtime \u2500\u2500\u2500
        Camera _cam;
        float _yaw;
        float _pitch;
        bool _active;

        /// <summary>Call to enable camera following (owner only).</summary>
        public void StartFollowing()
        {
            _active = true;
            _cam ??= Camera.main;
            if (_cam != null)
            {
                // Initialize yaw/pitch from current camera position relative to target
                if (_followTarget != null)
                {
                    Vector3 dir = _cam.transform.position - _followTarget.position;
                    _yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                    _pitch = Mathf.Asin(dir.y / dir.magnitude) * Mathf.Rad2Deg;
                }
            }
        }

        public void StopFollowing()
        {
            _active = false;
        }

        void Awake()
        {
            _cam = Camera.main;
            if (_followTarget == null)
                _followTarget = transform;
            if (_inputSource == null)
                _inputSource = GetComponent<NPCMultiplayerInputActions>();

            _yaw = _followTarget != null ? _followTarget.eulerAngles.y : transform.eulerAngles.y;
        }

        void LateUpdate()
        {
            if (!_active || _cam == null || _followTarget == null)
                return;

            // Read look input
            Vector2 lookInput = Vector2.zero;
            if (_inputSource != null)
                lookInput = _inputSource.LookInput;

            _yaw += lookInput.x * _yawSensitivity;
            _pitch += lookInput.y * _pitchSensitivity * (_invertY ? 1f : -1f);
            _pitch = Mathf.Clamp(_pitch, _minPitch, _maxPitch);

            // Calculate camera position
            Quaternion cameraRotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 targetPosition = _followTarget.position + cameraRotation * _cameraOffset;

            // Smooth follow
            float t = 1f - Mathf.Exp(-_followSharpness * Time.deltaTime);
            _cam.transform.position = Vector3.Lerp(_cam.transform.position, targetPosition, t);

            // Look at target
            _cam.transform.LookAt(_followTarget.position + _lookTargetOffset);
        }

        public void SetInputSource(NPCMultiplayerInputActions source)
        {
            _inputSource = source;
        }
    }
}
