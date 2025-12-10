using Antigravity.Controllers;
using UnityEngine;

namespace Antigravity.Movement
{
    /// <summary>
    /// Encapsulates all dash-related logic and state.
    /// Handles charge management, cooldown, and impulse application.
    /// </summary>
    public class DashHandler
    {
        private readonly PlayerMovementConfig _config;

        // State
        private int _currentDashCharges;
        private float _dashReloadTimer;
        private float _dashAuthenticationTimer; // "Intermission" timer (prevents spam)
        private bool _pendingDash;

        /// <summary>
        /// Current number of dash charges available.
        /// </summary>
        public int CurrentDashCharges => _currentDashCharges;

        public DashHandler(PlayerMovementConfig config)
        {
            _config = config;
            // Initialize with full charges
            _currentDashCharges = config.MaxDashCharges;
        }

        /// <summary>
        /// Resets dash state when module is activated.
        /// </summary>
        public void OnActivated()
        {
            _pendingDash = false;
            _dashAuthenticationTimer = 0f;
        }

        /// <summary>
        /// Called by PlayerController when dash button is pressed.
        /// </summary>
        public void RequestDash()
        {
            _pendingDash = true;
        }

        /// <summary>
        /// Updates charge reload and intermission timers.
        /// Called from AfterUpdate.
        /// </summary>
        public void UpdateCharges(float deltaTime)
        {
            // Tick Intermission (cooldown between consecutive dashes)
            if (_dashAuthenticationTimer > 0)
                _dashAuthenticationTimer -= deltaTime;

            // Reload Logic (regenerate charges)
            if (_currentDashCharges < _config.MaxDashCharges)
            {
                _dashReloadTimer += deltaTime;
                if (_dashReloadTimer >= _config.DashReloadTime)
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

        /// <summary>
        /// Attempts to apply dash impulse if conditions are met.
        /// Returns true if dash was applied.
        /// </summary>
        public bool TryApplyDash(ref Vector3 velocityAdd, Vector3 direction)
        {
            // Requirements:
            // 1. Pending request
            // 2. Off "Intermission" cooldown
            // 3. Has charges
            // 4. Moving (has direction)

            if (
                _pendingDash
                && _dashAuthenticationTimer <= 0
                && _currentDashCharges > 0
                && direction.sqrMagnitude > 0
            )
            {
                // Apply impulse
                velocityAdd += direction * _config.DashForce;

                // Consume charge
                _currentDashCharges--;

                // Set intermission (prevent spamming multiple dashes in 1 frame)
                _dashAuthenticationTimer = _config.DashIntermissionTime;

                // Clear pending flag
                _pendingDash = false;

                return true;
            }

            // Clear pending flag even if dash didn't trigger
            _pendingDash = false;
            return false;
        }
    }
}
