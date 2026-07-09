using UnityEngine;

namespace NPCSystem
{
    /// <summary>
    /// Third-person orbit camera controller.
    /// Separated from movement logic entirely.
    /// Only active for the owning player.
    /// </summary>
    public sealed class NPCThirdPersonCameraController : MonoBehaviour
    {
        [Header("Follow")]
        [SerializeField]
        Transform followTarget;

        [SerializeField]
        Vector3 cameraOffset = new Vector3(0f, 4.5f, -6f);

        [SerializeField]
        float followSharpness = 12f;

        [SerializeField]
        Vector3 lookTargetOffset = new Vector3(0f, 1.25f, 0f);

        [Header("Look Sensitivity")]
        [SerializeField]
        float yawSensitivity = 0.12f;

        [SerializeField]
        float pitchSensitivity = 0.08f;

        [SerializeField]
        float minPitch = -30f;

        [SerializeField]
        float maxPitch = 60f;

        [SerializeField]
        bool invertY = false;

        [Header("Input Source")]
        [SerializeField]
        NPCMultiplayerInputActions inputSource;

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
                if (followTarget != null)
                {
                    Vector3 dir = _cam.transform.position - followTarget.position;
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
            if (followTarget == null)
                followTarget = transform;
            if (inputSource == null)
                inputSource = GetComponent<NPCMultiplayerInputActions>();

            _yaw = followTarget != null ? followTarget.eulerAngles.y : transform.eulerAngles.y;
        }

        void LateUpdate()
        {
            if (!_active || _cam == null || followTarget == null)
                return;

            // Read look input
            Vector2 lookInput = Vector2.zero;
            if (inputSource != null)
                lookInput = inputSource.LookInput;

            _yaw += lookInput.x * yawSensitivity;
            _pitch += lookInput.y * pitchSensitivity * (invertY ? 1f : -1f);
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);

            // Calculate camera position
            Quaternion cameraRotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 targetPosition = followTarget.position + cameraRotation * cameraOffset;

            // Smooth follow
            float t = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            _cam.transform.position = Vector3.Lerp(_cam.transform.position, targetPosition, t);

            // Look at target
            _cam.transform.LookAt(followTarget.position + lookTargetOffset);
        }

        public void SetInputSource(NPCMultiplayerInputActions source)
        {
            inputSource = source;
        }
    }
}
