using System.Collections.Generic;
using Antigravity.Character.States;
using Antigravity.Movement;
using Antigravity.Time;
using KinematicCharacterController;
using UnityEngine;

namespace Antigravity.Controllers
{
    /// <summary>
    /// Character modes for special movement states.
    /// </summary>
    public enum CharacterState
    {
        Default, // Normal physics-based movement
        NoClip, // Fly-through mode for debug/testing
    }

    /// <summary>
    /// Main player controller implementing KCC's ICharacterController interface.
    /// <para>
    /// <strong>Architecture:</strong>
    /// - Physics/Movement: Handled via KCC callbacks (UpdateVelocity, UpdateRotation, etc.)
    /// - State Observation: HSM tracks state for animations/UI but does NOT control physics
    /// - Input: Delegated to PlayerInputHandler
    /// - Parameters: Configured via PlayerMovementConfig ScriptableObject
    /// </para>
    /// </summary>
    [RequireComponent(typeof(PlayerInputHandler))]
    [RequireComponent(typeof(KinematicCharacterMotor))]
    public class PlayerController : MonoBehaviour
    {
        #region Inspector Fields

        [Header("References")]
        public KinematicCharacterMotor Motor;
        public PlayerInputHandler InputHandler;
        public PlayerMovementConfig Config;

        [Header("State Machine (Debug - Observation Only)")]
        [SerializeField]
        private string _currentStateDebug;

        [Header("Misc")]
        public List<Collider> IgnoredColliders = new List<Collider>();
        public Transform MeshRoot;

        #endregion

        #region Properties

        public CharacterState CurrentCharacterState { get; private set; }

        #endregion

        #region Private Fields

        // HSM (Observation only - does NOT control physics)
        private PlayerStateMachine _stateMachine;
        private PlayerStateFactory _stateFactory;

        // Movement System
        private PlayerMovementSystem _movementSystem;
        private DefaultMovement _defaultMovement;
        private NoClipMovement _noClipMovement;

        // Input State (passed to movement modules)
        private Vector3 _moveInputVector;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // Connect Adapter to Motor
            Motor.CharacterController = new PlayerKCCAdapter(this);

            // Auto-find input handler if not assigned
            if (InputHandler == null)
                InputHandler = GetComponent<PlayerInputHandler>();

            // Initialize Movement System
            _movementSystem = new PlayerMovementSystem();

            _defaultMovement = new DefaultMovement(Motor, Config, InputHandler, MeshRoot);
            _noClipMovement = new NoClipMovement(Motor, Config, InputHandler);

            _movementSystem.RegisterModule(_defaultMovement, isDefault: true);
            _movementSystem.RegisterModule(_noClipMovement);

            // Initialize State Machine (observation only)
            _stateMachine = new PlayerStateMachine();
            _stateFactory = new PlayerStateFactory(_stateMachine, this);
            _stateMachine.Initialize(_stateFactory.Grounded());

            TransitionToState(CharacterState.Default);
        }

        private void Update()
        {
            if (InputHandler == null)
                return;

            // Handle Noclip Toggle
            if (InputHandler.NoclipToggleDown)
            {
                ToggleNoClip();
            }

            // Handle Rewind
            if (InputHandler.RewindHeld)
            {
                TimeManager.Instance.StartRewind();
            }
            else
            {
                TimeManager.Instance.StopRewind();
            }

            // Calculate Move Vector
            _moveInputVector = CameraInputProcessor.GetCameraRelativeMoveVector(
                InputHandler.MoveInput,
                Camera.main
            );

            // Pass input to ALL modules (since we don't know which is active easily without casting)
            // Or better: pass to both. They are cheap to set.
            _defaultMovement.SetMoveInput(_moveInputVector);
            _noClipMovement.SetMoveInput(_moveInputVector);

            // Handle Jump Request
            if (InputHandler.JumpDown)
            {
                // Only default movement cares about jump triggers usually
                _defaultMovement.RequestJump();
            }

            // Update HSM (observation only)
            _stateMachine.CurrentState.UpdateStates();

            // Debug: Show current state hierarchy
            _currentStateDebug = GetCurrentStateHierarchy();
        }

        #endregion

        #region State Machine Helpers

        /// <summary>
        /// Gets a formatted string showing the current state hierarchy (e.g., "grounded>move").
        /// Used for debug display in Inspector.
        /// </summary>
        private string GetCurrentStateHierarchy()
        {
            if (_stateMachine?.CurrentState == null)
                return "none";

            // Get root state name
            string rootName = _stateMachine
                .CurrentState.GetType()
                .Name.Replace("Player", "")
                .Replace("State", "")
                .ToLower();

            // Get substate directly via public property
            var subState = _stateMachine.CurrentState.CurrentSubState;
            if (subState != null)
            {
                string subName = subState
                    .GetType()
                    .Name.Replace("Player", "")
                    .Replace("State", "")
                    .ToLower();

                // Special case: "doublejump" -> "double jump" for readability
                if (subName == "doublejump")
                    subName = "double jump";

                return $"{rootName}>{subName}";
            }

            return rootName;
        }

        #endregion

        #region Character State Management

        /// <summary>
        /// Toggles between Default and NoClip character states.
        /// </summary>
        private void ToggleNoClip()
        {
            if (CurrentCharacterState == CharacterState.Default)
                TransitionToState(CharacterState.NoClip);
            else
                TransitionToState(CharacterState.Default);
        }

        // --- KCC Logic ---

        public void TransitionToState(CharacterState newState)
        {
            CharacterState tmpInitialState = CurrentCharacterState;
            OnStateExit(tmpInitialState, newState);
            CurrentCharacterState = newState;
            OnStateEnter(newState, tmpInitialState);
        }

        public void OnStateEnter(CharacterState state, CharacterState fromState)
        {
            // Activate corresponding Movement Module
            switch (state)
            {
                case CharacterState.Default:
                    _movementSystem.ActivateDefaultModule();
                    break;
                case CharacterState.NoClip:
                    _movementSystem.ActivateModule<NoClipMovement>();
                    break;
            }
        }

        public void OnStateExit(CharacterState state, CharacterState toState)
        {
            // Nothing needed here - activating a new module automatically deactivates the old one
        }

        #endregion

        #region KCC Adapter Handlers (Called via Adapter)

        public void HandleRotation(ref Quaternion currentRotation, float deltaTime)
        {
            if (TimeManager.Instance.IsRewinding)
                return;

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

        public void HandleVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            if (TimeManager.Instance.IsRewinding)
                return;

            // PURE DELEGATION! 🎉
            _movementSystem.UpdatePhysics(ref currentVelocity, deltaTime);
        }

        public void HandleAfterUpdate(float deltaTime)
        {
            if (TimeManager.Instance.IsRewinding)
                return;

            // PURE DELEGATION! 🎉
            _movementSystem.AfterUpdate(deltaTime);
        }

        public void HandleMovementHit(
            Collider hitCollider,
            Vector3 hitNormal,
            Vector3 hitPoint,
            ref HitStabilityReport hitStabilityReport
        )
        {
            if (CurrentCharacterState == CharacterState.Default && !hitStabilityReport.IsStable)
            {
                // Delegate wall hit detection to movement module
                _defaultMovement?.OnWallHit(hitNormal);
            }
        }

        #endregion
    }
}
