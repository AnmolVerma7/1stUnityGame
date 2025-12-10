using UnityEngine;

namespace Antigravity.Movement
{
    /// <summary>
    /// Interface for modular movement abilities (jumping, wallrun, combat, etc.).
    /// <para>
    /// Each module encapsulates the physics logic for a specific movement mode.
    /// PlayerMovementSystem manages which module is active based on state.
    /// </para>
    /// </summary>
    public interface IMovementModule
    {
        /// <summary>
        /// Update physics/velocity for this movement mode.
        /// Called from PlayerController.UpdateVelocity().
        /// </summary>
        /// <param name="velocity">Current velocity to modify.</param>
        /// <param name="deltaTime">Time since last update.</param>
        void UpdatePhysics(ref Vector3 velocity, float deltaTime);

        /// <summary>
        /// Called after character update (KCC's AfterCharacterUpdate).
        /// Used for state cleanup, jump consumption, etc.
        /// </summary>
        /// <param name="deltaTime">Time since last update.</param>
        void AfterUpdate(float deltaTime);

        /// <summary>
        /// Called when this module is activated.
        /// </summary>
        void OnActivated();

        /// <summary>
        /// Called when this module is deactivated.
        /// </summary>
        void OnDeactivated();
    }
}
