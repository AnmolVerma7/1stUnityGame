using System;
using Antigravity.Controllers;
using KinematicCharacterController;
using UnityEngine;

namespace Antigravity.Movement
{
    public enum JumpType
    {
        None,
        Ground,
        Coyote,
        Air, // Double/Triple jump
        Wall
    }

    /// <summary>
    /// Encapsulates all jump-related logic and state.
    /// Uses a counter system for scalable multi-jumps (Double, Triple, etc).
    /// </summary>
    public class JumpHandler
    {
        private readonly KinematicCharacterMotor _motor;
        private readonly PlayerMovementConfig _config;

        // Configuration
        private int _maxAirJumps; // 1 = double jump allowed, 0 = standard only

        // State
        private int _airJumpsUsed;
        private bool _jumpRequested;
        private bool _jumpedThisFrame;

        // Wall Jump State
        private bool _canWallJump;
        private Vector3 _wallJumpNormal;

        // Timers
        private float _timeSinceJumpRequested = Mathf.Infinity;
        private float _timeSinceLastAbleToJump;

        // Events (Hook up Audio/VFX here!)
        public event Action<JumpType> OnJumpPerformed;

        public JumpHandler(KinematicCharacterMotor motor, PlayerMovementConfig config)
        {
            _motor = motor;
            _config = config;

            // Convert boolean config to counter system
            // In future, you can simply add "public int MaxAirJumps" to config
            _maxAirJumps = config.AllowDoubleJump ? 1 : 0;
        }

        /// <summary>
        /// Resets jump state when module is activated.
        /// </summary>
        public void OnActivated()
        {
            _jumpRequested = false;
            _airJumpsUsed = 0;
            _timeSinceJumpRequested = Mathf.Infinity;
            _timeSinceLastAbleToJump = 0f;
        }

        /// <summary>
        /// Buffers a jump request for the next physics update.
        /// </summary>
        public void RequestJump()
        {
            _timeSinceJumpRequested = 0f;
            _jumpRequested = true;
        }

        public void OnWallHit(Vector3 wallNormal)
        {
            if (_config.AllowWallJump && !_motor.GroundingStatus.IsStableOnGround)
            {
                _canWallJump = true;
                _wallJumpNormal = wallNormal;
            }
        }

        /// <summary>
        /// Main jump logic loop. Evaluates Wall -> Ground -> Air priority.
        /// </summary>
        public void ProcessJump(ref Vector3 currentVelocity, float deltaTime)
        {
            _jumpedThisFrame = false;
            _timeSinceJumpRequested += deltaTime;

            if (!_jumpRequested)
                return;

            // Priority 1: Wall Jump (Always takes precedence if available)
            if (TryWallJump(ref currentVelocity))
                return;

            // Priority 2: Ground/Coyote Jump (Reset air jumps)
            if (TryGroundOrCoyoteJump(ref currentVelocity))
                return;

            // Priority 3: Air Jump (Double/Triple)
            if (TryAirJump(ref currentVelocity))
                return;
        }

        public void PostUpdate(float deltaTime)
        {
            _canWallJump = false; // Reset wall jump opportunity every frame

            // Buffer Expiration
            if (_jumpRequested && _timeSinceJumpRequested > _config.JumpPreGroundingGraceTime)
            {
                _jumpRequested = false;
            }

            // Ground State Logic
            bool isGrounded = _config.AllowJumpingWhenSliding
                ? _motor.GroundingStatus.FoundAnyGround
                : _motor.GroundingStatus.IsStableOnGround;

            if (isGrounded)
            {
                if (!_jumpedThisFrame)
                {
                    _airJumpsUsed = 0; // Reset double jump ability
                    _timeSinceLastAbleToJump = 0f;
                }
            }
            else
            {
                _timeSinceLastAbleToJump += deltaTime;
            }
        }

        #region Internal Jump Logic

        private bool TryWallJump(ref Vector3 currentVelocity)
        {
            if (_canWallJump)
            {
                ExecuteJump(ref currentVelocity, _wallJumpNormal, JumpType.Wall);
                return true;
            }
            return false;
        }

        private bool TryGroundOrCoyoteJump(ref Vector3 currentVelocity)
        {
            // Logic: Not grounded right now, BUT within grace period
            // Note: If we are actually grounded, timeSinceLastAbleToJump is 0
            bool canJump = _timeSinceLastAbleToJump <= _config.JumpPostGroundingGraceTime;

            // Extra check: If we've already used air jumps, we probably shouldn't coyote jump
            // (prevents jumping, falling, then coyote jumping again)
            if (_airJumpsUsed > 0)
                canJump = false;

            if (canJump)
            {
                Vector3 jumpDir = _motor.CharacterUp;

                // If on slope, jump normal to surface
                if (
                    _motor.GroundingStatus.FoundAnyGround
                    && !_motor.GroundingStatus.IsStableOnGround
                )
                {
                    jumpDir = _motor.GroundingStatus.GroundNormal;
                }

                JumpType type = (_timeSinceLastAbleToJump > 0) ? JumpType.Coyote : JumpType.Ground;
                ExecuteJump(ref currentVelocity, jumpDir, type);
                return true;
            }
            return false;
        }

        private bool TryAirJump(ref Vector3 currentVelocity)
        {
            if (_airJumpsUsed < _maxAirJumps)
            {
                ExecuteJump(ref currentVelocity, _motor.CharacterUp, JumpType.Air);
                _airJumpsUsed++;
                return true;
            }
            return false;
        }

        private void ExecuteJump(ref Vector3 currentVelocity, Vector3 jumpDirection, JumpType type)
        {
            // Unground to prevent KCC from snapping back
            _motor.ForceUnground(0.1f);

            // Apply velocity
            currentVelocity +=
                (jumpDirection * _config.JumpSpeed)
                - Vector3.Project(currentVelocity, _motor.CharacterUp);

            // 3. State Updates
            _jumpRequested = false;
            _jumpedThisFrame = true;
            _timeSinceLastAbleToJump = Mathf.Infinity; // Invalidate Coyote Time immediately!

            // 4. Notify Listeners (Audio/VFX/Animation)
            OnJumpPerformed?.Invoke(type);

            // Debug log for verification
            // Debug.Log($"Jump Performed: {type} | AirJumps: {_airJumpsUsed}/{_maxAirJumps}");
        }

        #endregion
    }
}
