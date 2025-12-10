using Antigravity.Controllers;
using KinematicCharacterController;
using UnityEngine;

namespace Antigravity.Movement
{
    /// <summary>
    /// Base class for movement modules providing common dependencies.
    /// <para>
    /// All modules get Motor and Config for free.
    /// Add module-specific dependencies in your derived class constructor.
    /// </para>
    /// </summary>
    public abstract class MovementModuleBase : IMovementModule
    {
        #region Protected Dependencies (Available to All Modules)

        /// <summary>
        /// The KCC motor for physics queries and manipulation.
        /// </summary>
        protected readonly KinematicCharacterMotor Motor;

        /// <summary>
        /// Movement configuration (speeds, jump force, etc.).
        /// </summary>
        protected readonly PlayerMovementConfig Config;

        #endregion

        #region Constructor

        protected MovementModuleBase(KinematicCharacterMotor motor, PlayerMovementConfig config)
        {
            Motor = motor;
            Config = config;
        }

        #endregion

        #region IMovementModule Implementation (Abstract)

        /// <summary>
        /// Update physics/velocity for this movement mode.
        /// Must be implemented by derived classes.
        /// </summary>
        public abstract void UpdatePhysics(ref Vector3 velocity, float deltaTime);

        /// <summary>
        /// Update rotation for this movement mode.
        /// Must be implemented by derived classes.
        /// </summary>
        public abstract void UpdateRotation(ref Quaternion currentRotation, float deltaTime);

        public virtual void OnSprintStarted() { }

        public virtual void OnDashStarted() { }

        /// <summary>
        /// Called after character update (cleanup, state management).
        /// Must be implemented by derived classes.
        /// </summary>
        public abstract void AfterUpdate(float deltaTime);

        #endregion

        #region IMovementModule Implementation (Virtual)

        /// <summary>
        /// Called when this module is activated.
        /// Override to add custom initialization.
        /// </summary>
        public virtual void OnActivated()
        {
            // Default: Do nothing
        }

        /// <summary>
        /// Called when this module is deactivated.
        /// Override to add custom cleanup.
        /// </summary>
        public virtual void OnDeactivated()
        {
            // Default: Do nothing
        }

        #endregion
    }
}
