using KinematicCharacterController;
using UnityEngine;

namespace Antigravity.Character
{
    /// <summary>
    /// Bridges movement system state to the Animator.
    /// Reads velocity, input, and movement states, then sets animator parameters.
    /// </summary>
    public class PlayerAnimator : MonoBehaviour
    {
        #region Inspector Fields

        [Header("References")]
        [Tooltip("The Animator component on the character model.")]
        [SerializeField]
        private Animator _animator;

        [Tooltip("The KCC motor for reading velocity and grounding.")]
        [SerializeField]
        private KinematicCharacterMotor _motor;

        [Header("Animation Speed Tuning")]
        [Tooltip("Expected speed (m/s) for the walk animation clip.")]
        [SerializeField]
        private float _walkClipSpeed = 3f;

        [Tooltip("Expected speed (m/s) for the jog animation clip.")]
        [SerializeField]
        private float _jogClipSpeed = 8f;

        [Tooltip("Expected speed (m/s) for the sprint animation clip.")]
        [SerializeField]
        private float _sprintClipSpeed = 15f;

        [Header("Thresholds")]
        [Tooltip("Speed below which character is considered idle.")]
        [SerializeField]
        private float _idleThreshold = 0.1f;

        [Tooltip("Speed at which jog transitions to sprint blend.")]
        [SerializeField]
        private float _sprintThreshold = 10f;

        [Header("Smoothing")]
        [Tooltip("How quickly animator parameters blend to target values.")]
        [SerializeField]
        private float _dampTime = 0.1f;

        #endregion

        #region Animator Parameter Names (For Unity Setup)

        // ═══════════════════════════════════════════════════════════════════════
        // FLOATS - Use these in your Blend Trees
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>Horizontal movement speed (m/s). Use for locomotion blend tree.</summary>
        public const string PARAM_SPEED = "Speed";

        /// <summary>Vertical velocity (m/s). Positive = rising, Negative = falling.</summary>
        public const string PARAM_VERTICAL_SPEED = "VerticalSpeed";

        /// <summary>Stick/input magnitude (0-1). Use for analog blending.</summary>
        public const string PARAM_INPUT_MAGNITUDE = "InputMagnitude";

        /// <summary>Animation playback speed multiplier. Feed to "Speed" on blend tree.</summary>
        public const string PARAM_ANIM_SPEED = "AnimSpeed";

        // ═══════════════════════════════════════════════════════════════════════
        // BOOLS - Use these for state transitions
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>True when character is on stable ground.</summary>
        public const string PARAM_IS_GROUNDED = "IsGrounded";

        /// <summary>True when character is crouching (not sliding).</summary>
        public const string PARAM_IS_CROUCHING = "IsCrouching";

        /// <summary>True when character is sliding.</summary>
        public const string PARAM_IS_SLIDING = "IsSliding";

        /// <summary>True when character is hanging from a ledge.</summary>
        public const string PARAM_IS_HANGING = "IsHanging";

        /// <summary>True when character is in mantle animation.</summary>
        public const string PARAM_IS_MANTLING = "IsMantling";

        // ═══════════════════════════════════════════════════════════════════════
        // TRIGGERS - Use these for one-shot animations
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>Trigger for jump animation.</summary>
        public const string PARAM_JUMP = "Jump";

        /// <summary>Trigger for landing animation.</summary>
        public const string PARAM_LAND = "Land";

        #endregion

        #region Cached Hashes

        private static readonly int HashSpeed = Animator.StringToHash(PARAM_SPEED);
        private static readonly int HashVerticalSpeed = Animator.StringToHash(PARAM_VERTICAL_SPEED);
        private static readonly int HashInputMagnitude = Animator.StringToHash(
            PARAM_INPUT_MAGNITUDE
        );
        private static readonly int HashAnimSpeed = Animator.StringToHash(PARAM_ANIM_SPEED);
        private static readonly int HashIsGrounded = Animator.StringToHash(PARAM_IS_GROUNDED);
        private static readonly int HashIsCrouching = Animator.StringToHash(PARAM_IS_CROUCHING);
        private static readonly int HashIsSliding = Animator.StringToHash(PARAM_IS_SLIDING);
        private static readonly int HashIsHanging = Animator.StringToHash(PARAM_IS_HANGING);
        private static readonly int HashIsMantling = Animator.StringToHash(PARAM_IS_MANTLING);
        private static readonly int HashJump = Animator.StringToHash(PARAM_JUMP);
        private static readonly int HashLand = Animator.StringToHash(PARAM_LAND);

        #endregion

        #region State

        private bool _wasGrounded;
        private float _inputMagnitude;

        #endregion

        #region Public API

        /// <summary>
        /// Set the current stick/input magnitude (0-1).
        /// Call this from PlayerController each frame.
        /// </summary>
        public void SetInputMagnitude(float magnitude)
        {
            _inputMagnitude = Mathf.Clamp01(magnitude);
        }

        /// <summary>
        /// Set crouching state.
        /// </summary>
        public void SetCrouching(bool isCrouching)
        {
            if (_animator != null)
                _animator.SetBool(HashIsCrouching, isCrouching);
        }

        /// <summary>
        /// Set sliding state.
        /// </summary>
        public void SetSliding(bool isSliding)
        {
            if (_animator != null)
                _animator.SetBool(HashIsSliding, isSliding);
        }

        /// <summary>
        /// Set hanging state (on ledge).
        /// </summary>
        public void SetHanging(bool isHanging)
        {
            if (_animator != null)
                _animator.SetBool(HashIsHanging, isHanging);
        }

        /// <summary>
        /// Set mantling state.
        /// </summary>
        public void SetMantling(bool isMantling)
        {
            if (_animator != null)
                _animator.SetBool(HashIsMantling, isMantling);
        }

        /// <summary>
        /// Trigger jump animation.
        /// </summary>
        public void TriggerJump()
        {
            if (_animator != null)
                _animator.SetTrigger(HashJump);
        }

        /// <summary>
        /// Trigger land animation.
        /// </summary>
        public void TriggerLand()
        {
            if (_animator != null)
                _animator.SetTrigger(HashLand);
        }

        #endregion

        #region Unity Lifecycle

        private void Update()
        {
            if (_animator == null || _motor == null)
                return;

            UpdateLocomotion();
            UpdateGrounding();
        }

        #endregion

        #region Internal

        private void UpdateLocomotion()
        {
            // Calculate horizontal speed (ignore vertical)
            Vector3 horizontalVelocity = new Vector3(_motor.Velocity.x, 0f, _motor.Velocity.z);
            float speed = horizontalVelocity.magnitude;

            // Vertical speed (for falling/rising blend)
            float verticalSpeed = _motor.Velocity.y;

            // Calculate animation playback speed
            float animSpeed = CalculateAnimSpeed(speed);

            // Set float parameters with damping for smooth transitions
            _animator.SetFloat(HashSpeed, speed, _dampTime, UnityEngine.Time.deltaTime);
            _animator.SetFloat(
                HashVerticalSpeed,
                verticalSpeed,
                _dampTime,
                UnityEngine.Time.deltaTime
            );
            _animator.SetFloat(
                HashInputMagnitude,
                _inputMagnitude,
                _dampTime,
                UnityEngine.Time.deltaTime
            );
            _animator.SetFloat(HashAnimSpeed, animSpeed);
        }

        private void UpdateGrounding()
        {
            bool isGrounded = _motor.GroundingStatus.IsStableOnGround;
            _animator.SetBool(HashIsGrounded, isGrounded);

            // Detect landing
            if (isGrounded && !_wasGrounded)
            {
                TriggerLand();
            }

            _wasGrounded = isGrounded;
        }

        private float CalculateAnimSpeed(float speed)
        {
            if (speed < _idleThreshold)
                return 1f; // Idle, no scaling

            // Determine which clip we're likely in based on speed
            float expectedSpeed;
            if (speed < 3f)
                expectedSpeed = _walkClipSpeed;
            else if (speed < _sprintThreshold)
                expectedSpeed = _jogClipSpeed;
            else
                expectedSpeed = _sprintClipSpeed;

            // Scale playback so footsteps match actual movement
            float animSpeed = speed / expectedSpeed;

            // Clamp to reasonable range
            return Mathf.Clamp(animSpeed, 0.5f, 2f);
        }

        #endregion
    }
}
