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

namespace NPCSystem.Character.Animation
{
    /// <summary>
    /// Drives animator parameters from an <see cref="AnimatorSnapshot"/> (network-authoritative)
    /// or falls back to reading from <see cref="NPCCharacterMotor"/> directly.
    ///
    /// When <see cref="NPCNetworkAnimatorState"/> is present on the same GameObject, the bridge
    /// receives snapshots via <see cref="ApplySnapshot"/> and the motor is used only as a
    /// second source for immediate local response (owner client).
    ///
    /// No movement logic, no input logic — pure animation parameter application.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public sealed class NPCCharacterAnimatorBridge : MonoBehaviour
    {
        // ─── Animator hashes (computed once) ───
        static readonly int MoveXHash = Animator.StringToHash("MoveX");
        static readonly int MoveYHash = Animator.StringToHash("MoveY");
        static readonly int SpeedHash = Animator.StringToHash("Speed");
        static readonly int GroundedHash = Animator.StringToHash("Grounded");
        static readonly int SprintingHash = Animator.StringToHash("Sprinting");
        static readonly int JumpHash = Animator.StringToHash("Jump");
        static readonly int MotionSpeedHash = Animator.StringToHash("MotionSpeed");

        [Header("References")]
        [SerializeField]
        Animator animator;

        [SerializeField]
        NPCCharacterMotor motor;

        [Header("Tuning")]
        [SerializeField]
        float moveXScale = 1f;

        [SerializeField]
        float moveYScale = 1f;

        // ── Runtime state ──
        AnimatorSnapshot _pendingSnapshot;
        bool _hasPendingSnapshot;

        void Awake()
        {
            if (animator == null)
                animator = GetComponent<Animator>();
            if (motor == null)
                motor = GetComponent<NPCCharacterMotor>();
        }

        void Update()
        {
            if (animator == null)
                return;

            // If a network snapshot is available, apply it (authoritative path).
            // Otherwise, fall back to reading local motor state (legacy/local-play path).
            if (_hasPendingSnapshot)
            {
                ApplySnapshotToAnimator(_pendingSnapshot);
                _hasPendingSnapshot = false;
            }
            else if (motor != null)
            {
                Vector2 moveInput = motor.MoveInput;
                bool grounded = motor.Grounded;
                bool sprinting = motor.IsSprinting;

                animator.SetFloat(MoveXHash, moveInput.x * moveXScale);
                animator.SetFloat(MoveYHash, moveInput.y * moveYScale);
                animator.SetFloat(SpeedHash, motor.SpeedRatio);
                animator.SetFloat(MotionSpeedHash, motor.SpeedRatio);
                animator.SetBool(GroundedHash, grounded);
                animator.SetBool(SprintingHash, sprinting);
            }
        }

        /// <summary>
        /// Called by <see cref="NPCNetworkAnimatorState"/> when a new authoritative snapshot arrives.
        /// Stores it for the next Update() to apply.
        /// </summary>
        public void ApplySnapshot(AnimatorSnapshot snapshot)
        {
            _pendingSnapshot = snapshot;
            _hasPendingSnapshot = true;
        }

        /// <summary>
        /// Immediately apply an <see cref="AnimatorSnapshot"/> to the Animator parameters.
        /// </summary>
        void ApplySnapshotToAnimator(AnimatorSnapshot snapshot)
        {
            animator.SetFloat(MoveXHash, snapshot.MoveX * moveXScale);
            animator.SetFloat(MoveYHash, snapshot.MoveY * moveYScale);
            animator.SetFloat(SpeedHash, snapshot.Speed);
            animator.SetFloat(MotionSpeedHash, snapshot.MotionSpeed);
            animator.SetBool(GroundedHash, snapshot.Grounded);
            animator.SetBool(SprintingHash, snapshot.Sprinting);

            // Jump trigger: single-frame, consumed after emission
            if (snapshot.JumpTriggered)
            {
                animator.SetTrigger(JumpHash);
            }
        }

        /// <summary>
        /// Call to trigger the Jump animation directly (local authority).
        /// Used by NPCNetworkAnimatorState.NotifyJumpTriggered().
        /// </summary>
        public void TriggerJump()
        {
            if (animator != null)
                animator.SetTrigger(JumpHash);
        }

        /// <summary>
        /// Direct animator access for custom parameter sets.
        /// </summary>
        public void SetFloat(int hash, float value)
        {
            if (animator != null)
                animator.SetFloat(hash, value);
        }

        public void SetBool(int hash, bool value)
        {
            if (animator != null)
                animator.SetBool(hash, value);
        }

        public void SetTrigger(int hash)
        {
            if (animator != null)
                animator.SetTrigger(hash);
        }
    }
}
