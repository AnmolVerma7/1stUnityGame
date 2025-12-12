using Antigravity.Controllers;
using KinematicCharacterController;
using UnityEngine;

namespace Antigravity.Movement
{
    public enum MantleState
    {
        None,
        Grabbing,
        Hanging,
        Mantling
    }

    /// <summary>
    /// Encapsulates ledge grab, hang, shimmy, and mantle logic.
    /// Uses manual raycast-based detection for parkour-style climbing.
    /// </summary>
    public class MantleHandler
    {
        // ═══════════════════════════════════════════════════════════════════════
        // MAGIC NUMBERS - TODO: Move to config
        // ═══════════════════════════════════════════════════════════════════════
        private const float SHIMMY_CHECK_VERTICAL = 0.3f; // Multiplier for check height above grab
        private const float SHIMMY_CHECK_FORWARD = 0.5f; // Distance toward wall for edge check
        private const float SHIMMY_SPHERE_RADIUS = 0.3f; // OverlapSphere radius for edge detection
        private const float GRAB_PULLBACK = 0.05f; // Distance to pull back from wall when grabbing
        private const float MANTLE_FORWARD_OFFSET = 0.15f; // Extra forward distance for mantle target
        private const float DROP_COOLDOWN = 0.5f; // Time before can grab again after dropping

        // ═══════════════════════════════════════════════════════════════════════
        // DEPENDENCIES
        // ═══════════════════════════════════════════════════════════════════════
        private readonly KinematicCharacterMotor _motor;
        private readonly PlayerMovementConfig _config;
        private readonly PlayerInputHandler _input;

        // ═══════════════════════════════════════════════════════════════════════
        // STATE
        // ═══════════════════════════════════════════════════════════════════════
        private MantleState _state = MantleState.None;
        private Vector3 _grabPosition;
        private Vector3 _mantleTargetPosition;
        private float _stateTimer;
        private bool _mantleRequested;
        private bool _dropRequested;
        private float _dropCooldownTimer;

        // Shimmy state
        private Vector3 _ledgeRightDirection;
        private Vector3 _wallNormal;

        // ═══════════════════════════════════════════════════════════════════════
        // PROPERTIES
        // ═══════════════════════════════════════════════════════════════════════
        public bool IsActive => _state != MantleState.None;
        public MantleState CurrentState => _state;
        public Vector3 WallNormal => _wallNormal;

        public MantleHandler(
            KinematicCharacterMotor motor,
            PlayerMovementConfig config,
            PlayerInputHandler input
        )
        {
            _motor = motor;
            _config = config;
            _input = input;
        }

        /// <summary>
        /// Resets mantle state when module is activated.
        /// </summary>
        public void OnActivated()
        {
            _state = MantleState.None;
            _stateTimer = 0f;
            _mantleRequested = false;
        }

        /// <summary>
        /// Called when jump is pressed - used to confirm mantle while hanging.
        /// </summary>
        public void RequestMantle()
        {
            _mantleRequested = true;
        }

        /// <summary>
        /// Called when crouch is pressed - used to drop from ledge while hanging.
        /// </summary>
        public void RequestDrop()
        {
            _dropRequested = true;
        }

        /// <summary>
        /// Checks if the player can currently grab a ledge using manual detection.
        /// </summary>
        public bool CanGrab()
        {
            // Must not be already mantling
            if (IsActive)
            {
                // Debug.Log("Mantle: Already active");
                return false;
            }

            // Must be in air (not grounded)
            if (_motor.GroundingStatus.IsStableOnGround)
            {
                // Debug.Log("Mantle: Grounded");
                return false;
            }

            // Check cooldown
            if (_dropCooldownTimer > 0f)
            {
                return false;
            }

            // Use character forward instead of velocity direction
            // This allows mantling even when jumping straight up at a wall
            Vector3 forwardDirection = _motor.CharacterForward;

            // Check for wall in front
            RaycastHit wallHit;
            if (
                !Physics.Raycast(
                    _motor.TransientPosition,
                    forwardDirection,
                    out wallHit,
                    _config.MaxGrabDistance,
                    _config.MantleLayers,
                    QueryTriggerInteraction.Ignore
                )
            )
            {
                // Debug.Log("Mantle: No wall detected");
                return false;
            }

            // Wall must be roughly vertical (not a slope or ceiling)
            // Vertical wall = 90° angle between normal and CharacterUp
            // Accept walls between 60° and 120° (within 30° of vertical)
            float wallAngle = Vector3.Angle(wallHit.normal, _motor.CharacterUp);
            if (wallAngle < 60f || wallAngle > 120f)
            {
                // Debug.Log($"Mantle: Wall not vertical enough ({wallAngle:F1}°)");
                return false;
            }

            // Check for ledge above (flat surface we can stand on)
            Vector3 ledgeCheckStart = wallHit.point + (_motor.CharacterUp * _config.MaxLedgeHeight);
            RaycastHit ledgeHit;

            if (
                !Physics.Raycast(
                    ledgeCheckStart,
                    -_motor.CharacterUp,
                    out ledgeHit,
                    _config.MaxLedgeHeight - _config.MinLedgeHeight,
                    _config.MantleLayers,
                    QueryTriggerInteraction.Ignore
                )
            )
            {
                // Debug.Log("Mantle: No ledge top found");
                return false;
            }

            // Ledge must be walkable (check surface angle)
            float surfaceAngle = Vector3.Angle(ledgeHit.normal, _motor.CharacterUp);
            if (surfaceAngle > 45f) // Max 45 degree slope
            {
                // Debug.Log($"Mantle: Surface too steep ({surfaceAngle:F1}°)");
                return false;
            }

            // Valid ledge found!
            // Debug.Log("✅ Mantle: VALID LEDGE DETECTED!");
            return true;
        }

        /// <summary>
        /// Attempts to grab the detected ledge.
        /// </summary>
        public void TryGrab()
        {
            if (!CanGrab())
                return;

            // Use character forward direction
            Vector3 forwardDirection = _motor.CharacterForward;

            // Find wall
            RaycastHit wallHit;
            Physics.Raycast(
                _motor.TransientPosition,
                forwardDirection,
                out wallHit,
                _config.MaxGrabDistance,
                _config.MantleLayers,
                QueryTriggerInteraction.Ignore
            );

            // Find ledge top
            Vector3 ledgeCheckStart = wallHit.point + (_motor.CharacterUp * _config.MaxLedgeHeight);
            RaycastHit ledgeHit;
            Physics.Raycast(
                ledgeCheckStart,
                -_motor.CharacterUp,
                out ledgeHit,
                _config.MaxLedgeHeight - _config.MinLedgeHeight,
                _config.MantleLayers,
                QueryTriggerInteraction.Ignore
            );

            // Calculate grab position (hanging below ledge)
            _grabPosition = ledgeHit.point;
            _grabPosition.y -= _motor.Capsule.height * 0.5f;
            _grabPosition -= forwardDirection * (_motor.Capsule.radius + GRAB_PULLBACK);

            // Calculate mantle target (on top of ledge)
            _mantleTargetPosition = ledgeHit.point;
            _mantleTargetPosition +=
                forwardDirection * (_motor.Capsule.radius + MANTLE_FORWARD_OFFSET);

            // Calculate ledge right direction for shimmy (perpendicular to wall, horizontal)
            _wallNormal = wallHit.normal;
            _ledgeRightDirection = Vector3.Cross(_motor.CharacterUp, _wallNormal).normalized;

            // Enter grabbing state
            _state = MantleState.Grabbing;
            _stateTimer = 0f;
        }

        /// <summary>
        /// Cancels the current mantle (e.g., player pressed jump to drop).
        /// </summary>
        public void CancelGrab()
        {
            if (_state == MantleState.Hanging || _state == MantleState.Grabbing)
            {
                _state = MantleState.None;
                _stateTimer = 0f;
            }
        }

        /// <summary>
        /// Updates the mantle state machine.
        /// </summary>
        public void Update(float deltaTime)
        {
            // Cooldown handling - update always
            if (_dropCooldownTimer > 0f)
            {
                _dropCooldownTimer -= deltaTime;
            }

            if (!IsActive)
                return;

            _stateTimer += deltaTime;

            switch (_state)
            {
                case MantleState.Grabbing:
                    UpdateGrabbing(deltaTime);
                    break;
                case MantleState.Hanging:
                    UpdateHanging(deltaTime);
                    break;
                case MantleState.Mantling:
                    UpdateMantling(deltaTime);
                    break;
            }
        }

        private void UpdateGrabbing(float deltaTime)
        {
            // Snap to grab position and transition to hanging
            _motor.MoveCharacter(_grabPosition);
            _state = MantleState.Hanging;
            _stateTimer = 0f;
        }

        /// <summary>
        /// Checks if shimmy is allowed in the given direction.
        /// Uses OverlapSphere to detect if ledge continues.
        /// </summary>
        private bool CanShimmy(float direction)
        {
            // Calculate next position along the ledge
            float checkDistance = _config.ShimmyCheckDistance;
            Vector3 nextPos = _grabPosition + (_ledgeRightDirection * direction * checkDistance);

            // Check position at ledge level, moved forward toward wall
            Vector3 checkPos =
                nextPos
                + _motor.CharacterUp * (_motor.Capsule.height * SHIMMY_CHECK_VERTICAL)
                - _wallNormal * SHIMMY_CHECK_FORWARD;

            // OverlapSphere to check if collider exists at target position
            Collider[] hits = Physics.OverlapSphere(
                checkPos,
                SHIMMY_SPHERE_RADIUS,
                _config.MantleLayers,
                QueryTriggerInteraction.Ignore
            );

            return hits.Length > 0;
        }

        private void UpdateHanging(float deltaTime)
        {
            // Shimmy: Use camera-relative 2D input projected onto ledge direction
            Vector2 moveInput = _input.MoveInput;

            if (moveInput.magnitude > _config.ShimmyInputThreshold)
            {
                // Get camera orientation (horizontal plane only)
                Transform cameraTransform = Camera.main.transform;
                Vector3 cameraForward = cameraTransform.forward;
                Vector3 cameraRight = cameraTransform.right;

                // Flatten to horizontal plane
                cameraForward.y = 0f;
                cameraRight.y = 0f;
                cameraForward.Normalize();
                cameraRight.Normalize();

                // Project 2D input onto 3D world space relative to CAMERA orientation
                Vector3 desiredMove = cameraRight * moveInput.x + cameraForward * moveInput.y;

                // Project onto ledge tangent to get shimmy component
                float shimmyAmount = Vector3.Dot(desiredMove, _ledgeRightDirection);

                if (Mathf.Abs(shimmyAmount) > 0.01f)
                {
                    // Check if we can shimmy in this direction
                    float shimmyDirection = Mathf.Sign(shimmyAmount);

                    if (CanShimmy(shimmyDirection))
                    {
                        // Calculate movement delta (use full projected amount for natural feel)
                        float shimmyDelta = shimmyAmount * _config.ShimmySpeed * deltaTime;

                        // Update grab position
                        _grabPosition += _ledgeRightDirection * shimmyDelta;

                        // Move character using KCC
                        _motor.MoveCharacter(_grabPosition);

                        // Update mantle target to new position
                        _mantleTargetPosition =
                            _grabPosition
                            + _motor.CharacterUp * (_motor.Capsule.height * 0.5f)
                            + _motor.CharacterForward
                                * (_motor.Capsule.radius + MANTLE_FORWARD_OFFSET);
                    }
                }
            }

            // Mantle confirmation (jump while hanging)
            if (_mantleRequested)
            {
                _state = MantleState.Mantling;
                _stateTimer = 0f;
                _mantleRequested = false;
            }
            // Drop confirmation (crouch while hanging)
            else if (_dropRequested)
            {
                _state = MantleState.None;
                _dropRequested = false;
                _dropCooldownTimer = DROP_COOLDOWN;
            }
        }

        private void UpdateMantling(float deltaTime)
        {
            // Calculate lerp progress using animation curve
            float progress = _stateTimer / _config.MantleDuration;

            if (progress >= 1f)
            {
                // Mantle complete - use MoveCharacter for final position
                _motor.MoveCharacter(_mantleTargetPosition);
                _state = MantleState.None;
                return;
            }

            // Evaluate curve for smooth motion
            float curveValue = _config.MantleCurve.Evaluate(progress);

            // Arc-based motion: Move UP first (0-60% of motion), then FORWARD (40-100%)
            // This creates a natural "pull up then climb over" feel like Fortnite

            // Vertical progress: accelerated (completes by ~70% of total time)
            float verticalProgress = Mathf.Clamp01(curveValue * 1.4f);

            // Horizontal progress: delayed start (begins at ~50% of total time) and smoothed
            float horizontalRaw = Mathf.Clamp01((curveValue - 0.5f) / 0.5f);
            // Apply smoothstep for eased horizontal motion
            float horizontalProgress = horizontalRaw * horizontalRaw * (3f - 2f * horizontalRaw);

            // Calculate vertical position (from grab Y to target Y)
            float currentY = Mathf.Lerp(_grabPosition.y, _mantleTargetPosition.y, verticalProgress);

            // Calculate horizontal position (from grab XZ to target XZ)
            float currentX = Mathf.Lerp(
                _grabPosition.x,
                _mantleTargetPosition.x,
                horizontalProgress
            );
            float currentZ = Mathf.Lerp(
                _grabPosition.z,
                _mantleTargetPosition.z,
                horizontalProgress
            );

            Vector3 targetPosition = new Vector3(currentX, currentY, currentZ);

            // Use MoveCharacter for collision detection
            _motor.MoveCharacter(targetPosition);
        }

        /// <summary>
        /// Overrides velocity when mantling to prevent physics interference.
        /// </summary>
        public void OverrideVelocity(ref Vector3 velocity)
        {
            if (IsActive)
            {
                // Zero out velocity while mantling
                velocity = Vector3.zero;
            }
        }
    }
}
