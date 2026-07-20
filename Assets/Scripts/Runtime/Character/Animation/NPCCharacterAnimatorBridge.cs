using UnityEngine;

namespace NPCSystem.Character.Animation
{
    /// <summary>
    /// Drives animator parameters from <see cref="AnimatorSnapshot"/> snapshots
    /// received via <see cref="ApplySnapshot"/> from <see cref="NPCNetworkAnimatorState"/>.
    ///
    /// No direct motor polling — all animation state comes through snapshots only.
    /// The owner client captures state via NPCNetworkAnimatorState.CaptureFromMotor()
    /// which feeds ApplySnapshot() in the same frame for immediate local response.
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
        }

        void Update()
        {
            if (animator == null)
                return;

            // Only apply snapshots from NPCNetworkAnimatorState — no direct motor polling.
            // The owner client is handled via NPCNetworkAnimatorState.CaptureFromMotor()
            // which feeds ApplySnapshot() in the same frame.
            if (_hasPendingSnapshot)
            {
                ApplySnapshotToAnimator(_pendingSnapshot);
                _hasPendingSnapshot = false;
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
