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
                Vector3 addedVelocity = _moveInputVector * Config.AirAccelerationSpeed * deltaTime;

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

                // Simple horizontal cap (doesn't interfere with vertical/dash impulses)
                Vector3 horizontalVelocity = Vector3.ProjectOnPlane(
                    currentVelocity,
                    Motor.CharacterUp
                );
                Vector3 newHorizontalVelocity =
                    horizontalVelocity + Vector3.ProjectOnPlane(addedVelocity, Motor.CharacterUp);

                // Only limit horizontal speed, not total velocity (preserves dash impulse)
                if (newHorizontalVelocity.magnitude > Config.MaxAirMoveSpeed)
                {
                    newHorizontalVelocity =
                        newHorizontalVelocity.normalized * Config.MaxAirMoveSpeed;
                }

                addedVelocity =
                    newHorizontalVelocity
                    - horizontalVelocity
                    + Vector3.Project(addedVelocity, Motor.CharacterUp); // Preserve vertical component

                currentVelocity += addedVelocity;
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

            bool shouldCrouch = _input.IsCrouching; // Use IsCrouching (handles toggle/hold)

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
            // Entry: Sprint → Crouch (while moving and grounded)
            // Uses _pendingSlideEntry which was cached in UpdatePhysics before LateUpdate reset
            if (_pendingSlideEntry && !_isSliding)
            {
                TryEnterSlide(); // Only succeeds if conditions met
            }

            // Always reset the pending flag after check
            _pendingSlideEntry = false;

            // Exit conditions (already in slide)
            if (_isSliding)
            {
                // Exit if jumping
                if (Motor.GroundingStatus.FoundAnyGround == false)
                {
                    ExitSlide();
                }
                // Exit if sprint released (unless crouch toggle keeps us crouched)
                else if (!_input.IsSprinting)
                {
                    ExitSlide();
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
                && notCrouching; // Critical: Prevents Crouch→Sprint from sliding

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

            // Stay crouched if user still holding crouch button
            if (!_input.IsCrouching)
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
