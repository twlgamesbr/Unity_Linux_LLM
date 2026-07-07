using UnityEngine;

namespace NPCSystem
{
    /// <summary>
    /// Drives animator parameters from character movement state.
    /// Reads from NPCCharacterMotor (or optionally from raw input).
    /// No movement logic, no input logic \u2014 pure animation.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public sealed class NPCCharacterAnimatorBridge : MonoBehaviour
    {
        // \u2500\u2500\u2500 Animator hashes (computed once) \u2500\u2500\u2500
        static readonly int MoveXHash = Animator.StringToHash("MoveX");
        static readonly int MoveYHash = Animator.StringToHash("MoveY");
        static readonly int SpeedHash = Animator.StringToHash("Speed");
        static readonly int GroundedHash = Animator.StringToHash("Grounded");
        static readonly int SprintingHash = Animator.StringToHash("Sprinting");
        static readonly int JumpHash = Animator.StringToHash("Jump");
        static readonly int MotionSpeedHash = Animator.StringToHash("MotionSpeed");

        [Header("References")]
        [SerializeField] Animator animator;
        [SerializeField] NPCCharacterMotor motor;

        [Header("Tuning")]
        [SerializeField] float moveXScale = 1f;
        [SerializeField] float moveYScale = 1f;

        void Awake()
        {
            if (animator == null)
                animator = GetComponent<Animator>();
            if (motor == null)
                motor = GetComponent<NPCCharacterMotor>();
        }

        void Update()
        {
            if (animator == null || motor == null)
                return;

            Vector2 moveInput = motor.MoveInput;
            bool grounded = motor.Grounded;
            bool sprinting = motor.IsSprinting;

            // Blend tree input: use raw move direction for blend shapes
            animator.SetFloat(MoveXHash, moveInput.x * moveXScale);
            animator.SetFloat(MoveYHash, moveInput.y * moveYScale);

            // Normalized speed (0-1) for blend tree weight
            animator.SetFloat(SpeedHash, motor.SpeedRatio);

            // General motion speed
            animator.SetFloat(MotionSpeedHash, motor.SpeedRatio);

            // State booleans
            animator.SetBool(GroundedHash, grounded);
            animator.SetBool(SprintingHash, sprinting);
        }

        /// <summary>
        /// Call to trigger the Jump animation.
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
