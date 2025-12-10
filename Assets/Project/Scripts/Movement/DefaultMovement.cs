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
        private readonly SlideHandler _slideHandler; // Composition: Handles all slide logic
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
            _slideHandler = new SlideHandler(
                motor,
                config,
                input,
                () => _isCrouching,
                EnterCrouch,
                TryUncrouch
            );

            // Initialize full charges
            _currentDashCharges = config.MaxDashCharges;
        }

        #endregion

        #region IMovementModule Implementation

        public override void OnActivated()
        {
            _jumpHandler.OnActivated();
            _slideHandler.OnActivated();
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
            _slideHandler.HandleSlide();

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
        /// Requests slide entry (will be processed in SlideHandler).
        /// </summary>
        public void RequestSlide()
        {
            _slideHandler.RequestSlide();
        }

        public int CurrentDashCharges => _currentDashCharges;

        public bool IsSliding => _slideHandler.IsSliding;

        #endregion

        #region Movement Helper Methods

        private void ApplyGroundMovement(ref Vector3 currentVelocity, float deltaTime)
        {
            // If sliding, delegate to SlideHandler
            if (_slideHandler.IsSliding)
            {
                _slideHandler.ApplySlidePhysics(ref currentVelocity, deltaTime);
                return;
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
            if (_slideHandler.IsSliding)
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
    }
}
