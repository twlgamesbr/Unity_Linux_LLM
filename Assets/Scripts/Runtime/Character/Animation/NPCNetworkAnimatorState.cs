using Unity.Netcode;
using UnityEngine;


using NPCSystem.Monitoring;
using NPCSystem.Dialogue.Core;
using NPCSystem.Network.Core;
using NPCSystem.Character.Player;
using NPCSystem.Auth;
using NPCSystem.Items;
using NPCSystem.LocalAI;
using NPCSystem.Initialization;
using NPCSystem.Character.NPC;
using NPCSystem.Dialogue.Session;
using NPCSystem.Dialogue.UI;
using NPCSystem.Dialogue.RAG;
using NPCSystem.Dialogue.Persistence;
namespace NPCSystem.Character.Animation
{
    /// <summary>
    /// Server-authoritative animation state replication for player characters.
    ///
    /// Flow:
    ///   1. Owner client captures <see cref="AnimatorSnapshot"/> from local motor
    ///      and sends it to the server via <see cref="SubmitAnimStateServerRpc"/>.
    ///   2. Server receives the RPC and writes to <see cref="ReplicatedState"/>.
    ///   3. All clients (including owner) receive <see cref="NetworkVariable{T}.OnValueChanged"/>
    ///      and apply the snapshot to their local <see cref="NPCCharacterAnimatorBridge"/>.
    ///
    /// This guarantees ALL clients see identical animation state. WebGL clients
    /// only apply pre-computed float/bool values — no local Animator computation.
    ///
    /// The owner client also applies the snapshot directly (no round-trip delay)
    /// and the NetworkVariable provides eventual consistency for other clients.
    /// </summary>
    [RequireComponent(typeof(NPCPlayerCharacterController))]
    public sealed class NPCNetworkAnimatorState : NetworkBehaviour
    {
        // ── Network Variable ──
        /// <summary>
        /// The latest authoritative animation snapshot.
        /// Server writes, everyone reads.
        /// </summary>
        public readonly NetworkVariable<AnimatorSnapshot> ReplicatedState = new(
            new AnimatorSnapshot(),
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        [Header("Replication")]
        [SerializeField]
        [Tooltip("Minimum interval between animation state submissions (seconds).")]
        private float _sendIntervalSec = 0.05f; // 20 Hz default

        [SerializeField]
        [Tooltip("When true, the NON-owner uses transform-velocity heuristics as fallback.")]
        private bool _enableServerFallback = true;

        // ── Runtime state ──
        private NPCPlayerCharacterController _controller;
        private NPCCharacterAnimatorBridge _bridge;
        private float _lastSendTime;

        // ── Transform-velocity tracking (server fallback) ──
        private Vector3 _lastPosition;
        private float _serverFallbackSpeed;

        void Awake()
        {
            _controller = GetComponent<NPCPlayerCharacterController>();
            _bridge = GetComponent<NPCCharacterAnimatorBridge>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            ReplicatedState.OnValueChanged += OnReplicatedAnimStateChanged;

            if (IsServer)
            {
                _lastPosition = transform.position;
            }
        }

        public override void OnNetworkDespawn()
        {
            ReplicatedState.OnValueChanged -= OnReplicatedAnimStateChanged;
            base.OnNetworkDespawn();
        }

        void Update()
        {
            // ── Owner client: capture local motor state, send to server ──
            if (IsOwner && _controller != null && _controller.Motor != null)
            {
                float now = Time.time;
                AnimatorSnapshot localSnapshot = CaptureFromMotor();

                // Apply locally immediately (no latency for the owner)
                if (_bridge != null)
                    _bridge.ApplySnapshot(localSnapshot);

                // Send to server at reduced rate
                if (now - _lastSendTime >= _sendIntervalSec)
                {
                    _lastSendTime = now;
                    SubmitAnimStateServerRpc(localSnapshot);
                }
                return;
            }

            // ── Server / host: apply state from NetworkVariable when it changes ──
            // (handled via OnReplicatedAnimStateChanged on all clients)

            // ── Server fallback: estimate anim state from transform deltas ──
            if (IsServer && _enableServerFallback && !IsOwner)
            {
                // Compute velocity from transform changes
                Vector3 displacement = transform.position - _lastPosition;
                _lastPosition = transform.position;

                float moveMagnitude = new Vector3(displacement.x, 0, displacement.z).magnitude;
                float verticalDelta = displacement.y;

                // Estimate speed ratio from velocity
                float estimatedSpeed = Mathf.Clamp01(moveMagnitude / (_sendIntervalSec * 8f));

                // Detect grounded from vertical stability
                bool estimatedGrounded = Mathf.Abs(verticalDelta) < 0.05f;

                if (_bridge != null)
                {
                    var fallbackSnapshot = new AnimatorSnapshot
                    {
                        Speed = estimatedSpeed,
                        MotionSpeed = estimatedSpeed,
                        Grounded = estimatedGrounded,
                        Sprinting = estimatedSpeed > 0.7f,
                    };
                    _bridge.ApplySnapshot(fallbackSnapshot);
                }
            }
        }

        /// <summary>
        /// Build an <see cref="AnimatorSnapshot"/> from the local character motor.
        /// </summary>
        AnimatorSnapshot CaptureFromMotor()
        {
            NPCCharacterMotor motor = _controller.Motor;
            if (motor == null)
                return default;

            Vector2 input = motor.MoveInput;
            return new AnimatorSnapshot
            {
                MoveX = input.x,
                MoveY = input.y,
                Speed = motor.SpeedRatio,
                MotionSpeed = motor.SpeedRatio,
                Grounded = motor.Grounded,
                Sprinting = motor.IsSprinting,
                JumpTriggered = false, // TriggerJump is called separately; consumed per-frame
            };
        }

        /// <summary>
        /// Client sends its computed animation snapshot to the server at a throttled rate.
        /// </summary>
        [Rpc(SendTo.Server)]
        void SubmitAnimStateServerRpc(AnimatorSnapshot snapshot, RpcParams rpcParams = default)
        {
            if (!IsServer)
                return;
            ReplicatedState.Value = snapshot;
        }

        /// <summary>
        /// Fires on ALL clients when the server broadcasts a new animation snapshot.
        /// Applies the snapshot to the local Animator via the bridge.
        /// </summary>
        void OnReplicatedAnimStateChanged(AnimatorSnapshot previousValue, AnimatorSnapshot newValue)
        {
            // Owner already applied locally in Update() — skip to avoid double-apply
            if (IsOwner)
                return;

            if (_bridge != null)
                _bridge.ApplySnapshot(newValue);
        }

        /// <summary>
        /// Called by the player controller when a jump is triggered locally.
        /// The bridge applies the trigger to the Animator immediately.
        /// </summary>
        public void NotifyJumpTriggered()
        {
            if (_bridge != null)
                _bridge.TriggerJump();
        }
    }
}
