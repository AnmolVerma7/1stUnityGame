using Antigravity.Controllers;
using KinematicCharacterController;
using UnityEngine;

namespace Antigravity.Movement
{
    /// <summary>
    /// Default movement module handling ground movement, jumping, and basic physics.
    /// <para>
    /// Simplified DefaultMovement using composition for Jump Logic.
    /// </para>
    /// </summary>
    public class DefaultMovement : MovementModuleBase
    {
        #region Additional Dependencies

        private readonly PlayerInputHandler _input;
        private readonly Transform _meshRoot;
        private readonly JumpHandler _jumpHandler; // Composition: Handles all jump logic
        #endregion

        #region State

        // Movement
        private Vector3 _moveInputVector;
        private Vector3 _internalVelocityAdd = Vector3.zero;

        // Crouch State
        private bool _isCrouching;
        private Collider[] _probedColliders = new Collider[8]; // Buffer for overlap checks

        // Sprint & Dash State
        private float _dashAuthenticationTimer; // "Intermission" timer
        private bool _pendingDash;

        // Dash Charges
        private int _currentDashCharges;
        private float _dashReloadTimer;

        // Slide State
        private bool _isSliding;
        private float _slideTimer; // Track duration
        private Vector3 _slideDirection; // Locked at slide start
        private bool _pendingSlideEntry; // Cached from input before LateUpdate reset
        private float _lastSlideExitTime = -999f; // Allow immediate slide on start reset
        #endregion

        #region Constructor

        public DefaultMovement(
            KinematicCharacterMotor motor,
            PlayerMovementConfig config,
            PlayerInputHandler input,
            Transform meshRoot
        )
            : base(motor, config)
        {
            _input = input;
            _meshRoot = meshRoot;
            _jumpHandler = new JumpHandler(motor, config);

            // Initialize full charges
            _currentDashCharges = config.MaxDashCharges;
        }

        #endregion

        #region IMovementModule Implementation

        public override void OnActivated()
        {
            _jumpHandler.OnActivated();
        }

        public override void OnDashStarted()
        {
            _pendingDash = true;
        }

        public override void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
        {
            if (_moveInputVector != Vector3.zero && Config.OrientationSharpness > 0f)
            {
                Vector3 smoothedLookInputDirection = Vector3
                    .Slerp(
                        Motor.CharacterForward,
                        _moveInputVector,
                        1 - Mathf.Exp(-Config.OrientationSharpness * deltaTime)
                    )
                    .normalized;

                currentRotation = Quaternion.LookRotation(
                    smoothedLookInputDirection,
                    Motor.CharacterUp
                );
            }

            if (Config.OrientTowardsGravity)
            {
                currentRotation =
                    Quaternion.FromToRotation((currentRotation * Vector3.up), -Config.Gravity)
                    * currentRotation;
            }
        }

        public override void UpdatePhysics(ref Vector3 currentVelocity, float deltaTime)
        {
            // 1. Movement
            if (Motor.GroundingStatus.IsStableOnGround)
            {
                ApplyGroundMovement(ref currentVelocity, deltaTime);
            }
            else
            {
                ApplyAirMovement(ref currentVelocity, deltaTime);
            }

            // 2. Jump (Delegated to Scalable Handler)
            _jumpHandler.ProcessJump(ref currentVelocity, deltaTime);

            // 3. Internal forces
            ApplyInternalVelocity(ref currentVelocity);

            // 4. Reset one-shot events
            _pendingDash = false;
        }

        public override void AfterUpdate(float deltaTime)
        {
            // 1. Jump cleanup
            _jumpHandler.PostUpdate(deltaTime);

            // 2. Slide entry/exit handling
            HandleSlide();

            // 3. Crouch handling
            HandleCrouch();

            // 4. Dash Charge Logic
            HandleDashCharges(deltaTime);
        }

        #endregion

        #region Public API

        public void SetMoveInput(Vector3 moveVector)
        {
            _moveInputVector = moveVector;
        }

        public void RequestJump()
        {
            _jumpHandler.RequestJump();
        }

        public void OnWallHit(Vector3 wallNormal)
        {
            _jumpHandler.OnWallHit(wallNormal);
        }

        /// <summary>
        /// Called by PlayerController when crouch is activated.
        /// Requests slide entry (will be processed in HandleSlide).
        /// </summary>
        public void RequestSlide()
        {
            _pendingSlideEntry = true;
        }

        public int CurrentDashCharges => _currentDashCharges;

        public bool IsSliding => _isSliding;

        #endregion

        #region Movement Helper Methods

        private void ApplyGroundMovement(ref Vector3 currentVelocity, float deltaTime)
        {
            // SLIDE CHECK: Override normal movement if sliding
            if (_isSliding)
            {
                ApplySlidePhysics(ref currentVelocity, deltaTime);
                return; // Skip normal ground movement
            }

            currentVelocity =
                Motor.GetDirectionTangentToSurface(
                    currentVelocity,
                    Motor.GroundingStatus.GroundNormal
                ) * currentVelocity.magnitude;

            Vector3 inputRight = Vector3.Cross(_moveInputVector, Motor.CharacterUp);
            Vector3 reorientedInput =
                Vector3.Cross(Motor.GroundingStatus.GroundNormal, inputRight).normalized
                * _moveInputVector.magnitude;

            // Dash Logic (Ground) ⚡️
            ApplyDash(ref _internalVelocityAdd, reorientedInput.normalized);

            float targetSpeed = _input.IsSprinting
                ? Config.MaxSprintMoveSpeed
                : Config.MaxStableMoveSpeed;
            Vector3 targetVelocity = reorientedInput * targetSpeed;

            currentVelocity = Vector3.Lerp(
                currentVelocity,
                targetVelocity,
                1 - Mathf.Exp(-Config.StableMovementSharpness * deltaTime)
            );
        }

        private void ApplyAirMovement(ref Vector3 currentVelocity, float deltaTime)
        {
            // Dash Logic (Air) ✈️⚡️
            if (_pendingDash)
            {
                ApplyDash(ref _internalVelocityAdd, _moveInputVector.normalized);
            }

            if (_moveInputVector.sqrMagnitude > 0f)
            {
                // KCC Improvement: Better air velocity cap (prevents bunny-hop exploits)
                // Preserves momentum (like dash) but prevents adding speed beyond MaxAirMoveSpeed
                Vector3 addedVelocity = _moveInputVector * Config.AirAccelerationSpeed * deltaTime;
                Vector3 currentVelocityOnInputsPlane = Vector3.ProjectOnPlane(
                    currentVelocity,
                    Motor.CharacterUp
                );

                // Cap air velocity more precisely
                if (currentVelocityOnInputsPlane.magnitude < Config.MaxAirMoveSpeed)
                {
                    // Clamp total velocity to not exceed max
                    Vector3 newTotal = Vector3.ClampMagnitude(
                        currentVelocityOnInputsPlane + addedVelocity,
                        Config.MaxAirMoveSpeed
                    );
                    addedVelocity = newTotal - currentVelocityOnInputsPlane;
                }
                else
                {
                    // Velocity is already high (e.g. from Dash)
                    // Don't allow acceleration in direction of already-exceeding velocity
                    if (Vector3.Dot(currentVelocityOnInputsPlane, addedVelocity) > 0f)
                    {
                        addedVelocity = Vector3.ProjectOnPlane(
                            addedVelocity,
                            currentVelocityOnInputsPlane.normalized
                        );
                    }
                }

                // KCC Improvement: Better air wall prevention
                if (Motor.GroundingStatus.FoundAnyGround)
                {
                    Vector3 perpenticularObstructionNormal = Vector3
                        .Cross(
                            Vector3.Cross(Motor.CharacterUp, Motor.GroundingStatus.GroundNormal),
                            Motor.CharacterUp
                        )
                        .normalized;
                    addedVelocity = Vector3.ProjectOnPlane(
                        addedVelocity,
                        perpenticularObstructionNormal
                    );
                }

                currentVelocity += addedVelocity;
            }

            // Consistency Fix: High-speed decay in air
            // On ground, friction (StableMovementSharpness) slows dash down quickly.
            // In air, we need similar logic to prevent "infinite" momentum and keep dash distance predictable.
            Vector3 planarVelocity = Vector3.ProjectOnPlane(currentVelocity, Motor.CharacterUp);
            float currentPlanarSpeed = planarVelocity.magnitude;

            if (currentPlanarSpeed > Config.MaxAirMoveSpeed)
            {
                // Target velocity is the same direction but clamped to max speed
                Vector3 targetPlanarVelocity = planarVelocity.normalized * Config.MaxAirMoveSpeed;

                // Recombine with vertical velocity
                Vector3 targetVelocity =
                    targetPlanarVelocity + Vector3.Project(currentVelocity, Motor.CharacterUp);

                // Decay towards target.
                // We use a value (e.g. 5f) that is lower than ground sharpness (15f)
                // to making air dashing slightly "freer" but still controlled.
                currentVelocity = Vector3.Lerp(
                    currentVelocity,
                    targetVelocity,
                    1 - Mathf.Exp(-5f * deltaTime)
                );
            }

            currentVelocity += Config.Gravity * deltaTime;
            currentVelocity *= 1f / (1f + (Config.Drag * deltaTime));
        }

        private void ApplyInternalVelocity(ref Vector3 currentVelocity)
        {
            if (_internalVelocityAdd.sqrMagnitude > 0f)
            {
                currentVelocity += _internalVelocityAdd;
                _internalVelocityAdd = Vector3.zero;
            }
        }

        private void ApplyDash(ref Vector3 velocityAdd, Vector3 direction)
        {
            // Requirement: Pending Request + Off "Intermission" + Has Charges + Moving
            if (
                _pendingDash
                && _dashAuthenticationTimer <= 0
                && _currentDashCharges > 0
                && direction.sqrMagnitude > 0
            )
            {
                // Apply Force
                velocityAdd += direction * Config.DashForce;

                // Consume Charge
                _currentDashCharges--;

                // Set Intermission (prevent spamming 10 dashes in 1 frame)
                _dashAuthenticationTimer = Config.DashIntermissionTime;

                // If this was the first charge used (we were full), start reload timer immediately?
                // Or does it always run? Usually reloading starts if not full.
                // We handle reloading in HandleDashCharges.
            }
        }

        private void HandleDashCharges(float deltaTime)
        {
            // Tick Intermission
            if (_dashAuthenticationTimer > 0)
                _dashAuthenticationTimer -= deltaTime;

            // Reload Logic
            if (_currentDashCharges < Config.MaxDashCharges)
            {
                _dashReloadTimer += deltaTime;
                if (_dashReloadTimer >= Config.DashReloadTime)
                {
                    _currentDashCharges++;
                    _dashReloadTimer = 0f; // Reset for next charge
                }
            }
            else
            {
                _dashReloadTimer = 0f;
            }
        }

        #endregion

        #region Crouch Helper Methods

        private void HandleCrouch()
        {
            // Don't manage crouch if we're sliding (slide owns the crouch state)
            if (_isSliding)
                return;

            // Fix: Prevent standard crouch while sprinting (Crouch input is reserved for Slide in this state)
            // If slide fails (cooldown/speed), we should stay sprinting, not dip into a crouch.
            bool shouldCrouch = _input.IsCrouching && !_input.IsSprinting;

            if (_isCrouching && !shouldCrouch)
            {
                TryUncrouch();
            }
            else if (!_isCrouching && shouldCrouch)
            {
                EnterCrouch();
            }
        }

        private void TryUncrouch()
        {
            Motor.SetCapsuleDimensions(0.5f, 2f, 1f);

            if (
                Motor.CharacterOverlap(
                    Motor.TransientPosition,
                    Motor.TransientRotation,
                    _probedColliders,
                    Motor.CollidableLayers,
                    QueryTriggerInteraction.Ignore
                ) > 0
            )
            {
                Motor.SetCapsuleDimensions(0.5f, 1f, 0.5f);
            }
            else
            {
                _meshRoot.localScale = new Vector3(1f, 1f, 1f);
                _isCrouching = false;
            }
        }

        private void EnterCrouch()
        {
            _isCrouching = true;
            Motor.SetCapsuleDimensions(0.5f, 1f, 0.5f);
            _meshRoot.localScale = new Vector3(1f, 0.5f, 1f);
        }

        #endregion

        #region Slide Helper Methods

        /// <summary>
        /// Manages slide entry/exit based on input state transitions.
        /// </summary>
        private void HandleSlide()
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
                else if (Config.ToggleSlide)
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
                if (Motor.GroundingStatus.FoundAnyGround == false)
                {
                    ExitSlide();
                    return;
                }

                if (Config.ToggleSlide)
                {
                    // Toggle Mode Exits:
                    // - Jump (Handled above)
                    // - Crouch Press (Handled above)
                    // - Speed Loss (Handled in UpdatePhysics/ApplyMovement or separate check?
                    //   Actually speed loss exit is likely handled by logic that checks velocity,
                    //   but let's rely on the MinSpeed check which presumably happens elsewhere or we add it here?)
                    //   MinSpeed check handles auto-exit usually.
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

            float currentSpeed = Motor.Velocity.magnitude;
            bool isSprinting = _input.IsSprinting;
            bool isGrounded = Motor.GroundingStatus.IsStableOnGround;
            bool notCrouching = !_isCrouching;

            // Use a LOW threshold for entry (1 m/s = barely moving)
            // We'll use the higher MinSlideSpeedToMaintain for EXIT
            bool canSlide =
                isSprinting
                && currentSpeed > 1f // Lower threshold for entry
                && isGrounded
                && notCrouching // Critical: Prevents Crouch→Sprint from sliding
                && (UnityEngine.Time.time >= _lastSlideExitTime + Config.SlideCooldown); // Cooldown check

            if (canSlide)
            {
                _isSliding = true;
                _slideDirection = Motor.Velocity.normalized; // Lock direction at slide start
                _slideTimer = 0f;
                EnterCrouch(); // Set crouch capsule dimensions
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
                TryUncrouch();
            }
        }

        /// <summary>
        /// Applies surface-aware slide physics (slope modifies speed).
        /// </summary>
        private void ApplySlidePhysics(ref Vector3 currentVelocity, float deltaTime)
        {
            // 1. Get slope info
            Vector3 groundNormal = Motor.GroundingStatus.GroundNormal;
            float slopeAngle = Vector3.Angle(Motor.CharacterUp, groundNormal);

            // 2. Calculate slide direction (project along slope)
            Vector3 slopeDirection = Vector3
                .ProjectOnPlane(_slideDirection, groundNormal)
                .normalized;

            // 3. Calculate speed modifier based on slope
            float slopeInfluence =
                Mathf.Sign(Vector3.Dot(slopeDirection, -Motor.CharacterUp)) * slopeAngle; // +angle downhill, -angle uphill
            float speedModifier =
                Config.BaseSlideSpeed + (Config.SlideGravityInfluence * slopeInfluence / 90f); // Normalize to 90° scale

            // 4. Target velocity
            Vector3 targetVelocity = slopeDirection * speedModifier;

            // 5. Apply friction (gradual deceleration/acceleration)
            currentVelocity = Vector3.Lerp(
                currentVelocity,
                targetVelocity,
                Config.SlideFriction * deltaTime
            );

            // 6. Auto-exit if too slow
            if (currentVelocity.magnitude < Config.MinSlideSpeedToMaintain)
            {
                ExitSlide();
                return;
            }

            // 7. Max duration check (if set)
            _slideTimer += deltaTime;
            if (Config.MaxSlideDuration > 0 && _slideTimer >= Config.MaxSlideDuration)
            {
                ExitSlide();
            }
        }

        #endregion
    }
}
