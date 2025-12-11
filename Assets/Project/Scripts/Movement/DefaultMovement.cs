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
        private readonly DashHandler _dashHandler; // Composition: Handles all dash logic
        private readonly MantleHandler _mantleHandler; // Composition: Handles ledge grab/mantle logic
        #endregion

        #region State

        // Movement
        private Vector3 _moveInputVector;
        private Vector3 _internalVelocityAdd = Vector3.zero;

        // Crouch State
        private bool _isCrouching;
        private Collider[] _probedColliders = new Collider[8]; // Buffer for overlap checks
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
            _dashHandler = new DashHandler(config);
            _mantleHandler = new MantleHandler(motor, config);
        }

        #endregion

        #region IMovementModule Implementation

        public override void OnActivated()
        {
            _jumpHandler.OnActivated();
            _slideHandler.OnActivated();
            _dashHandler.OnActivated();
            _mantleHandler.OnActivated();
        }

        public override void OnDashStarted()
        {
            _dashHandler.RequestDash();
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
            // 0. Mantle override (takes full control when active)
            if (_mantleHandler.IsActive)
            {
                // Check for mantle confirm while hanging
                if (_mantleHandler.CurrentState == MantleState.Hanging && _input.JumpDown)
                {
                    _mantleHandler.RequestMantle();
                }

                _mantleHandler.OverrideVelocity(ref currentVelocity);
                return;
            }

            // 1. Movement
            if (Motor.GroundingStatus.IsStableOnGround)
            {
                ApplyGroundMovement(ref currentVelocity, deltaTime);
            }
            else
            {
                ApplyAirMovement(ref currentVelocity, deltaTime);
            }

            // 3. Jump (Delegated to Scalable Handler)
            _jumpHandler.ProcessJump(ref currentVelocity, deltaTime);

            // 4. Mantle grab check
            // Can grab either by jumping near ledge OR falling past one
            bool tryGrabOnJump = _jumpHandler.JumpConsumedThisUpdate && _mantleHandler.CanGrab();
            bool tryGrabWhileFalling =
                !Motor.GroundingStatus.IsStableOnGround
                && currentVelocity.y < 0f // Falling down
                && _mantleHandler.CanGrab();

            if (tryGrabOnJump || tryGrabWhileFalling)
            {
                // Debug.Log("🧗 GRABBING LEDGE!");
                _mantleHandler.TryGrab();
            }

            // 4. Internal forces
            ApplyInternalVelocity(ref currentVelocity);
        }

        public override void AfterUpdate(float deltaTime)
        {
            // 1. Jump cleanup
            _jumpHandler.PostUpdate(deltaTime);

            // 2. Mantle state machine
            _mantleHandler.Update(deltaTime);

            // 3. Slide entry/exit handling
            _slideHandler.HandleSlide();

            // 4. Crouch handling
            HandleCrouch();

            // 5. Dash charge logic
            _dashHandler.UpdateCharges(deltaTime);
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

        public int CurrentDashCharges => _dashHandler.CurrentDashCharges;

        public bool IsSliding => _slideHandler.IsSliding;

        public bool IsMantling => _mantleHandler.IsActive;

        /// <summary>
        /// Requests mantle confirmation while hanging.
        /// </summary>
        public void RequestMantleConfirm()
        {
            _mantleHandler.RequestMantle();
        }

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
            _dashHandler.TryApplyDash(ref _internalVelocityAdd, reorientedInput.normalized);

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
            _dashHandler.TryApplyDash(ref _internalVelocityAdd, _moveInputVector.normalized);

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
