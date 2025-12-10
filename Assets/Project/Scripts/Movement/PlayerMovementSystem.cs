using System;
using System.Collections.Generic;
using UnityEngine;

namespace Antigravity.Movement
{
    /// <summary>
    /// Manages movement modules and delegates physics to the active module.
    /// <para>
    /// Acts as a coordinator between PlayerController and movement modules.
    /// Only one module can be active at a time (e.g., DefaultMovement OR WallRun, not both).
    /// </para>
    /// </summary>
    public class PlayerMovementSystem
    {
        #region State

        private readonly Dictionary<Type, IMovementModule> _modules = new();
        private IMovementModule _activeModule;
        private IMovementModule _defaultModule;

        #endregion

        #region Public API

        /// <summary>
        /// Register a movement module.
        /// </summary>
        /// <typeparam name="T">Module type</typeparam>
        /// <param name="module">Module instance</param>
        /// <param name="isDefault">If true, this becomes the fallback module</param>
        public void RegisterModule<T>(T module, bool isDefault = false)
            where T : IMovementModule
        {
            var type = typeof(T);
            if (_modules.ContainsKey(type))
            {
                Debug.LogWarning($"Module {type.Name} already registered. Replacing.");
            }

            _modules[type] = module;

            if (isDefault || _defaultModule == null)
            {
                _defaultModule = module;
                if (_activeModule == null)
                {
                    ActivateModule<T>();
                }
            }
        }

        /// <summary>
        /// Activate a specific module.
        /// </summary>
        public void ActivateModule<T>()
            where T : IMovementModule
        {
            var type = typeof(T);
            if (!_modules.TryGetValue(type, out var module))
            {
                Debug.LogError($"Module {type.Name} not registered!");
                return;
            }

            if (_activeModule == module)
                return; // Already active

            // Deactivate current
            if (_activeModule != null)
            {
                _activeModule.OnDeactivated();
            }

            // Activate new
            _activeModule = module;
            _activeModule.OnActivated();
        }

        /// <summary>
        /// Activate the default module (usually DefaultMovement).
        /// </summary>
        public void ActivateDefaultModule()
        {
            if (_defaultModule == null)
            {
                Debug.LogError("No default module registered!");
                return;
            }

            if (_activeModule == _defaultModule)
                return;

            if (_activeModule != null)
            {
                _activeModule.OnDeactivated();
            }

            _activeModule = _defaultModule;
            _activeModule.OnActivated();
        }

        /// <summary>
        /// Get the currently active module.
        /// </summary>
        public IMovementModule ActiveModule => _activeModule;

        /// <summary>
        /// Get a registered module by type.
        /// </summary>
        public T GetModule<T>()
            where T : class, IMovementModule
        {
            if (_modules.TryGetValue(typeof(T), out var module))
            {
                return module as T;
            }
            return null;
        }

        #endregion

        #region Physics Delegation

        /// <summary>
        /// Delegate physics update to the active module.
        /// Called from PlayerController.UpdateVelocity().
        /// </summary>
        public void UpdatePhysics(ref Vector3 velocity, float deltaTime)
        {
            if (_activeModule == null)
            {
                Debug.LogWarning("No active movement module!");
                return;
            }

            _activeModule.UpdatePhysics(ref velocity, deltaTime);
        }

        /// <summary>
        /// Delegate rotation update to the active module.
        /// Called from PlayerController.UpdateRotation().
        /// </summary>
        public void UpdateRotation(ref Quaternion rotation, float deltaTime)
        {
            if (_activeModule == null)
                return;

            _activeModule.UpdateRotation(ref rotation, deltaTime);
        }

        public void OnSprintStarted()
        {
            _activeModule?.OnSprintStarted();
        }

        public void OnDashStarted()
        {
            _activeModule?.OnDashStarted();
        }

        /// <summary>
        /// Delegate after-update to the active module.
        /// Called from PlayerController.AfterCharacterUpdate().
        /// </summary>
        public void AfterUpdate(float deltaTime)
        {
            if (_activeModule == null)
                return;

            _activeModule.AfterUpdate(deltaTime);
        }

        #endregion
    }
}
