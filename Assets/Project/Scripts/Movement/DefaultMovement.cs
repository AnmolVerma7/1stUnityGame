using Antigravity.Controllers;
using KinematicCharacterController;
using UnityEngine;

namespace Antigravity.Movement
{
    /// <summary>
    /// Default movement module handling ground movement, jumping, and basic physics.
    /// <para>
    /// This is the "standard" movement - what the player uses 90% of the time.
    /// Special modules (wallrun, combat) override this when active.
    /// </para>
    /// </summary>
    public class DefaultMovement : IMovementModule
    {
        #region Dependencies

        private readonly KinematicCharacterMotor _motor;
        private readonly PlayerMovementConfig _config;
        private readonly PlayerInputHandler _input;
        private readonly Transform _meshRoot;

        #endregion

        #region State

        // Jump System
        private bool _jumpRequested;
        private bool _jumpConsumed;
        private bool _doubleJumpConsumed;
        private bool _jumpedThisFrame;
        private bool _canWallJump;
        private Vector3 _wallJumpNormal;
        private float _timeSinceJumpRequested = Mathf.Infinity;
        private float _timeSinceLastAbleToJump;

        // Movement
        private Vector3 _moveInputVector;
        private Vector3 _internalVelocityAdd = Vector3.zero;

        // Crouch
        private bool _isCrouching;
        private Collider[] _probedColliders = new Collider[8];

        #endregion

        #region Constructor

        public DefaultMovement(
            KinematicCharacterMotor motor,
            PlayerMovementConfig config,
            PlayerInputHandler input,
            Transform meshRoot
        )
        {
            _motor = motor;
            _config = config;
            _input = input;
            _meshRoot = meshRoot;
        }

        #endregion

        #region IMovementModule Implementation

        public void OnActivated()
        {
            // Reset state when activated
            _jumpRequested = false;
            _jumpConsumed = false;
            _doubleJumpConsumed = false;
        }

        public void OnDeactivated()
        {
            // Cleanup if needed
        }

        public void UpdatePhysics(ref Vector3 currentVelocity, float deltaTime)
        {
            // Ground/Air Movement
            Vector3 targetMovementVelocity = Vector3.zero;
            if (_motor.GroundingStatus.IsStableOnGround)
            {
                // Ground Movement
                currentVelocity =
                    _motor.GetDirectionTangentToSurface(
                        currentVelocity,
                        _motor.GroundingStatus.GroundNormal
                    ) * currentVelocity.magnitude;

                Vector3 inputRight = Vector3.Cross(_moveInputVector, _motor.CharacterUp);
                Vector3 reorientedInput =
                    Vector3.Cross(_motor.GroundingStatus.GroundNormal, inputRight).normalized
                    * _moveInputVector.magnitude;

                targetMovementVelocity = reorientedInput * _config.MaxStableMoveSpeed;
                currentVelocity = Vector3.Lerp(
                    currentVelocity,
                    targetMovementVelocity,
                    1 - Mathf.Exp(-_config.StableMovementSharpness * deltaTime)
                );
            }
            else
            {
                // Air Movement
                if (_moveInputVector.sqrMagnitude > 0f)
                {
                    targetMovementVelocity = _moveInputVector * _config.MaxAirMoveSpeed;

                    if (_motor.GroundingStatus.FoundAnyGround)
                    {
                        Vector3 perpenticularObstructionNormal = Vector3
                            .Cross(
                                Vector3.Cross(
                                    _motor.CharacterUp,
                                    _motor.GroundingStatus.GroundNormal
                                ),
                                _motor.CharacterUp
                            )
                            .normalized;
                        targetMovementVelocity = Vector3.ProjectOnPlane(
                            targetMovementVelocity,
                            perpenticularObstructionNormal
                        );
                    }

                    Vector3 velocityDiff = Vector3.ProjectOnPlane(
                        targetMovementVelocity - currentVelocity,
                        _config.Gravity
                    );
                    currentVelocity += velocityDiff * _config.AirAccelerationSpeed * deltaTime;
                }

                // Gravity
                currentVelocity += _config.Gravity * deltaTime;
                currentVelocity *= (1f / (1f + (_config.Drag * deltaTime)));
            }

            // Jump System
            _jumpedThisFrame = false;
            _timeSinceJumpRequested += deltaTime;

            if (_jumpRequested)
            {
                // Double Jump
                if (_config.AllowDoubleJump)
                {
                    if (
                        _jumpConsumed
                        && !_doubleJumpConsumed
                        && (
                            _config.AllowJumpingWhenSliding
                                ? !_motor.GroundingStatus.FoundAnyGround
                                : !_motor.GroundingStatus.IsStableOnGround
                        )
                    )
                    {
                        _motor.ForceUnground(0.1f);
                        currentVelocity +=
                            (_motor.CharacterUp * _config.JumpSpeed)
                            - Vector3.Project(currentVelocity, _motor.CharacterUp);
                        _jumpRequested = false;
                        _doubleJumpConsumed = true;
                        _jumpedThisFrame = true;
                    }
                }

                // Regular Jump / Wall Jump / Coyote Time
                if (
                    _canWallJump
                    || (
                        !_jumpConsumed
                        && (
                            (
                                _config.AllowJumpingWhenSliding
                                    ? _motor.GroundingStatus.FoundAnyGround
                                    : _motor.GroundingStatus.IsStableOnGround
                            )
                            || _timeSinceLastAbleToJump <= _config.JumpPostGroundingGraceTime
                        )
                    )
                )
                {
                    Vector3 jumpDirection = _motor.CharacterUp;

                    if (_canWallJump)
                        jumpDirection = _wallJumpNormal;
                    else if (
                        _motor.GroundingStatus.FoundAnyGround
                        && !_motor.GroundingStatus.IsStableOnGround
                    )
                        jumpDirection = _motor.GroundingStatus.GroundNormal;

                    _motor.ForceUnground(0.1f);
                    currentVelocity +=
                        (jumpDirection * _config.JumpSpeed)
                        - Vector3.Project(currentVelocity, _motor.CharacterUp);
                    _jumpRequested = false;
                    _jumpConsumed = true;
                    _jumpedThisFrame = true;
                }
            }

            _canWallJump = false;

            // Internal velocity (for knockback, forces, etc.)
            if (_internalVelocityAdd.sqrMagnitude > 0f)
            {
                currentVelocity += _internalVelocityAdd;
                _internalVelocityAdd = Vector3.zero;
            }
        }

        public void AfterUpdate(float deltaTime)
        {
            // Jump buffer timeout
            if (_jumpRequested && _timeSinceJumpRequested > _config.JumpPreGroundingGraceTime)
            {
                _jumpRequested = false;
            }

            // Reset jump consumption when grounded
            if (
                _config.AllowJumpingWhenSliding
                    ? _motor.GroundingStatus.FoundAnyGround
                    : _motor.GroundingStatus.IsStableOnGround
            )
            {
                if (!_jumpedThisFrame)
                {
                    _doubleJumpConsumed = false;
                    _jumpConsumed = false;
                }
                _timeSinceLastAbleToJump = 0f;
            }
            else
            {
                _timeSinceLastAbleToJump += deltaTime;
            }

            // Crouch handling
            bool shouldBeCrouching = _input.CrouchHeld;

            if (_isCrouching && !shouldBeCrouching)
            {
                // Try to uncrouch
                _motor.SetCapsuleDimensions(0.5f, 2f, 1f);
                if (
                    _motor.CharacterOverlap(
                        _motor.TransientPosition,
                        _motor.TransientRotation,
                        _probedColliders,
                        _motor.CollidableLayers,
                        QueryTriggerInteraction.Ignore
                    ) > 0
                )
                {
                    // Can't uncrouch, revert
                    _motor.SetCapsuleDimensions(0.5f, 1f, 0.5f);
                }
                else
                {
                    _meshRoot.localScale = new Vector3(1f, 1f, 1f);
                    _isCrouching = false;
                }
            }
            else if (!_isCrouching && shouldBeCrouching)
            {
                // Crouch
                _isCrouching = true;
                _motor.SetCapsuleDimensions(0.5f, 1f, 0.5f);
                _meshRoot.localScale = new Vector3(1f, 0.5f, 1f);
            }
        }

        #endregion

        #region Public API (Called by PlayerController)

        /// <summary>
        /// Set the movement input vector (called from PlayerController.Update).
        /// </summary>
        public void SetMoveInput(Vector3 moveVector)
        {
            _moveInputVector = moveVector;
        }

        /// <summary>
        /// Request a jump (called when jump button is pressed).
        /// </summary>
        public void RequestJump()
        {
            _timeSinceJumpRequested = 0f;
            _jumpRequested = true;
        }

        /// <summary>
        /// Called when hitting a wall (for wall jump detection).
        /// </summary>
        public void OnWallHit(Vector3 wallNormal)
        {
            if (_config.AllowWallJump && !_motor.GroundingStatus.IsStableOnGround)
            {
                _canWallJump = true;
                _wallJumpNormal = wallNormal;
            }
        }

        #endregion
    }
}
