using Antigravity.Controllers;
using KinematicCharacterController;
using UnityEngine;

namespace Antigravity.Movement
{
    /// <summary>
    /// Encapsulates all slide-related logic and state.
    /// Handles entry/exit conditions, cooldown, and surface-aware physics.
    /// </summary>
    public class SlideHandler
    {
        private readonly KinematicCharacterMotor _motor;
        private readonly PlayerMovementConfig _config;
        private readonly PlayerInputHandler _input;

        // State
        private bool _isSliding;
        private float _slideTimer;
        private Vector3 _slideDirection;
        private bool _pendingSlideEntry;
        private float _lastSlideExitTime = -999f; // Allow immediate slide on start

        // Dependencies (injected)
        private readonly System.Func<bool> _isCrouchingGetter;
        private readonly System.Action _enterCrouchAction;
        private readonly System.Action _tryUncrouchAction;

        /// <summary>
        /// Whether the player is currently sliding.
        /// </summary>
        public bool IsSliding => _isSliding;

        public SlideHandler(
            KinematicCharacterMotor motor,
            PlayerMovementConfig config,
            PlayerInputHandler input,
            System.Func<bool> isCrouchingGetter,
            System.Action enterCrouchAction,
            System.Action tryUncrouchAction
        )
        {
            _motor = motor;
            _config = config;
            _input = input;
            _isCrouchingGetter = isCrouchingGetter;
            _enterCrouchAction = enterCrouchAction;
            _tryUncrouchAction = tryUncrouchAction;
        }

        /// <summary>
        /// Resets slide state when module is activated.
        /// </summary>
        public void OnActivated()
        {
            _isSliding = false;
            _pendingSlideEntry = false;
            _slideTimer = 0f;
        }

        /// <summary>
        /// Called by PlayerController when crouch is activated.
        /// Requests slide entry (will be processed in HandleSlide).
        /// </summary>
        public void RequestSlide()
        {
            _pendingSlideEntry = true;
        }

        /// <summary>
        /// Manages slide entry/exit based on input state transitions.
        /// Called from AfterUpdate.
        /// </summary>
        public void HandleSlide()
        {
            // Capture pending request and reset flag immediately
            bool requestedSlide = _pendingSlideEntry;
            _pendingSlideEntry = false;

            // 1. Input-based Entry/Exit Triggers
            if (requestedSlide)
            {
                if (!_isSliding)
                {
                    TryEnterSlide();
                }
                else if (_config.ToggleSlide)
                {
                    // Toggle Mode: Pressing crouch again cancels slide
                    ExitSlide();
                    return; // Stop processing to prevent immediate re-entry checks
                }
            }

            // 2. Continuous State Checks (if still sliding)
            if (_isSliding)
            {
                // General Exit: Left Ground (Jumping/Falling)
                if (_motor.GroundingStatus.FoundAnyGround == false)
                {
                    ExitSlide();
                    return;
                }

                if (_config.ToggleSlide)
                {
                    // Toggle Mode Exits:
                    // - Jump (Handled above)
                    // - Crouch Press (Handled above)
                    // - Speed Loss (Handled in ApplySlidePhysics via MinSlideSpeedToMaintain)
                }
                else
                {
                    // Hold Mode Exits:
                    // - Crouch Release
                    if (!_input.IsCrouching)
                    {
                        ExitSlide();
                        return;
                    }

                    // - Sprint Release (Optional for Hold Mode, keeps it responsive)
                    if (!_input.IsSprinting)
                    {
                        ExitSlide();
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Attempts to enter slide state. Only triggers from Sprint → Crouch when moving.
        /// </summary>
        private bool TryEnterSlide()
        {
            // Requirements:
            // 1. Must be sprinting
            // 2. Must be moving (velocity > low threshold for entry)
            // 3. Must be grounded
            // 4. Not already crouching (prevents Crouch→Sprint inadvertent slide)
            // 5. Not on cooldown

            float currentSpeed = _motor.Velocity.magnitude;
            bool isSprinting = _input.IsSprinting;
            bool isGrounded = _motor.GroundingStatus.IsStableOnGround;
            bool notCrouching = !_isCrouchingGetter();

            // Use a LOW threshold for entry (1 m/s = barely moving)
            // We'll use the higher MinSlideSpeedToMaintain for EXIT
            bool canSlide =
                isSprinting
                && currentSpeed > 1f // Lower threshold for entry
                && isGrounded
                && notCrouching // Critical: Prevents Crouch→Sprint from sliding
                && (UnityEngine.Time.time >= _lastSlideExitTime + _config.SlideCooldown); // Cooldown check

            if (canSlide)
            {
                _isSliding = true;
                _slideDirection = _motor.Velocity.normalized; // Lock direction at slide start
                _slideTimer = 0f;
                _enterCrouchAction(); // Set crouch capsule dimensions
                return true;
            }
            return false;
        }

        /// <summary>
        /// Exits slide state. Stays crouched if user still holding crouch.
        /// </summary>
        private void ExitSlide()
        {
            _isSliding = false;
            _slideTimer = 0f;
            _lastSlideExitTime = UnityEngine.Time.time; // Record exit time for cooldown

            // Stay crouched if user still holding crouch button
            if (_input.IsCrouching)
            {
                // Do nothing, stay crouched
            }
            else
            {
                _tryUncrouchAction();
            }
        }

        /// <summary>
        /// Applies surface-aware slide physics (slope modifies speed).
        /// Called from ApplyGroundMovement when IsSliding is true.
        /// </summary>
        public void ApplySlidePhysics(ref Vector3 currentVelocity, float deltaTime)
        {
            _slideTimer += deltaTime;

            // Get ground normal to detect slope
            Vector3 groundNormal = _motor.GroundingStatus.GroundNormal;

            // Calculate slope angle (0 = flat, 90 = vertical wall)
            float slopeAngle = Vector3.Angle(groundNormal, _motor.CharacterUp);

            // Determine if we're going downhill or uphill
            // Dot product of slide direction with "down-slope" direction
            Vector3 slopeDirection = Vector3
                .ProjectOnPlane(-_motor.CharacterUp, groundNormal)
                .normalized;
            float slopeDot = Vector3.Dot(_slideDirection, slopeDirection);

            // Slope influence: positive = downhill boost, negative = uphill penalty
            float slopeInfluence = slopeDot * (slopeAngle / 90f) * _config.SlideGravityInfluence;

            // Calculate target speed (base + slope modifier)
            float targetSpeed = _config.BaseSlideSpeed + slopeInfluence;
            targetSpeed = Mathf.Max(targetSpeed, 0f); // Never negative

            // Current speed
            float currentSpeed = currentVelocity.magnitude;

            // Apply friction (slow down over time)
            float friction = _config.SlideFriction;
            float newSpeed = Mathf.Lerp(currentSpeed, targetSpeed, friction * deltaTime);

            // Check for speed-based exit
            if (newSpeed < _config.MinSlideSpeedToMaintain)
            {
                ExitSlide();
                return;
            }

            // Check for duration-based exit (if enabled)
            if (_config.MaxSlideDuration > 0f && _slideTimer >= _config.MaxSlideDuration)
            {
                ExitSlide();
                return;
            }

            // Set velocity (maintain locked direction)
            currentVelocity = _slideDirection * newSpeed;
        }
    }
}
