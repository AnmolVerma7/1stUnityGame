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
        private readonly JumpHandler _jumpHandler;

        #endregion

        #region State

        // Movement
        private Vector3 _moveInputVector;
        private Vector3 _internalVelocityAdd = Vector3.zero;

        // Crouch State
        private bool _isCrouching;
        private Collider[] _probedColliders = new Collider[8];

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

            // 2. Crouch handling
            HandleCrouch();

            // 3. Dash Charge Logic
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

        public int CurrentDashCharges => _currentDashCharges;

        #endregion

        #region Movement Helper Methods

        private void ApplyGroundMovement(ref Vector3 currentVelocity, float deltaTime)
        {
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
                Vector3 targetVelocity = _moveInputVector * Config.MaxAirMoveSpeed;

                if (Motor.GroundingStatus.FoundAnyGround)
                {
                    Vector3 obstructionNormal = Vector3
                        .Cross(
                            Vector3.Cross(Motor.CharacterUp, Motor.GroundingStatus.GroundNormal),
                            Motor.CharacterUp
                        )
                        .normalized;
                    targetVelocity = Vector3.ProjectOnPlane(targetVelocity, obstructionNormal);
                }

                Vector3 velocityDiff = Vector3.ProjectOnPlane(
                    targetVelocity - currentVelocity,
                    Config.Gravity
                );
                currentVelocity += velocityDiff * Config.AirAccelerationSpeed * deltaTime;
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
            bool shouldCrouch = _input.CrouchHeld;

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
