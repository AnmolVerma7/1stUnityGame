using UnityEngine;

namespace Antigravity.Controllers
{
    /// <summary>
    /// Handles all input detection for the player.
    /// This script's ONLY job is to listen to the Input System and store the results.
    /// It does not decide *what* to do with the input (that's the Controller's job).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(InputRouter))]
    public class PlayerInputHandler : MonoBehaviour, IInputUser
    {
        #region Inspector Fields

        [Header("Movement Input")]
        [SerializeField]
        private Vector2 _moveInput;

        [Header("Look Input")]
        [SerializeField]
        private Vector2 _lookDelta; // Mouse

        [SerializeField]
        private Vector2 _lookRate; // Gamepad

        [Header("Button States")]
        [Tooltip("True while Jump button is held down")]
        [SerializeField]
        private bool _jumpPressed;

        [Tooltip("True while Crouch button is held down")]
        [SerializeField]
        private bool _crouchPressed;

        [Tooltip("True while Rewind/Shoot button is held down")]
        [SerializeField]
        private bool _rewindPressed;

        [Header("Sprint Settings")]
        private PlayerMovementConfig _config;

        [Tooltip("Debug: Is Sprint Toggled On?")]
        [SerializeField]
        private bool _sprintToggledOn;

        [Tooltip("Debug view of sprint hold state")]
        [SerializeField]
        private bool _sprintHeld;

        #endregion

        #region Private Fields (Not Shown in Inspector)

        // One-frame triggers - reset each frame, not useful to display
        private bool _jumpTriggered;
        private bool _noclipTriggered;
        private bool _dashTriggered;

        #endregion

        #region Public Accessors

        /// <summary>Current movement input (WASD/Stick).</summary>
        public Vector2 MoveInput => _moveInput;

        /// <summary>Mouse delta this frame (pixels).</summary>
        public Vector2 LookDelta => _lookDelta;

        /// <summary>Gamepad stick rate (-1 to 1).</summary>
        public Vector2 LookRate => _lookRate;

        /// <summary>True for ONE frame when jump is first pressed.</summary>
        public bool JumpDown => _jumpTriggered;

        /// <summary>True while jump button is held.</summary>
        public bool JumpHeld => _jumpPressed;

        /// <summary>True while crouch button is held.</summary>
        public bool CrouchHeld => _crouchPressed;

        /// <summary>True while rewind button is held.</summary>
        public bool RewindHeld => _rewindPressed;

        /// <summary>True for ONE frame when noclip is toggled.</summary>
        public bool NoclipToggleDown => _noclipTriggered;

        /// <summary>True if sprint is currently active (handles both Toggle and Hold modes).</summary>
        public bool IsSprinting =>
            (_config != null && _config.ToggleSprint) ? _sprintToggledOn : _sprintHeld;

        /// <summary>True for ONE frame when sprint became active.</summary>
        public bool SprintJustActivated { get; private set; }

        /// <summary>True for ONE frame when dash is pressed.</summary>
        public bool DashJustActivated => _dashTriggered;

        #endregion

        /// <inheritdoc />
        public void RegisterInputs(InputBuilder builder)
        {
            // Move
            builder.Bind(builder.Actions.Move).To<Vector2>(v => _moveInput = v);

            // Look (Advanced: Handle Mouse vs Gamepad)
            var router = GetComponent<InputRouter>();
            if (router != null)
            {
                router.Bind(
                    builder.Actions.Look,
                    new LookInputCommand(
                        delta => _lookDelta += delta, // Accumulate delta for this frame
                        rate => _lookRate = rate // Set continuous rate
                    )
                );
            }

            // Jump
            builder
                .Bind(builder.Actions.Jump)
                .Press(() =>
                {
                    _jumpTriggered = true;
                    _jumpPressed = true;
                })
                .Release(() => _jumpPressed = false)
                .Register();

            // Crouch
            builder
                .Bind(builder.Actions.Crouch)
                .Press(() => _crouchPressed = true)
                .Release(() => _crouchPressed = false)
                .Register();

            // Rewind
            builder
                .Bind(builder.Actions.Shoot)
                .Hold(0.1f)
                .Press(() => _rewindPressed = true)
                .Release(() => _rewindPressed = false)
                .Register();

            // Sprint
            builder
                .Bind(builder.Actions.Sprint)
                .Press(OnSprintPress)
                .Release(OnSprintRelease)
                .Register();

            // Dash (Instant Impulse)
            builder.Bind(builder.Actions.Dash).Press(() => _dashTriggered = true).Register();
        }

        private void Awake()
        {
            // Self-Registration to avoid needing "UniversalInput" component
            var router = GetComponent<InputRouter>();
            if (router != null)
            {
                var builder = new InputBuilder(router);
                RegisterInputs(builder);
            }

            // Try to find config on controller
            var controller = GetComponent<PlayerController>();
            if (controller != null)
                _config = controller.Config;
        }

        private void OnEnable()
        {
            // Lock cursor on start
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            // Dev Inputs
            if (UnityEngine.Input.GetKeyDown(KeyCode.N))
            {
                Debug.Log("Noclip Toggle Pressed");
                _noclipTriggered = true;
            }

            // Cursor Handling
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (UnityEngine.Input.GetMouseButtonDown(0) && Cursor.visible)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            // Auto-cancel sprint if idle (only for Toggle mode)
            bool toggleMode = _config != null ? _config.ToggleSprint : true; // Default true if no config found

            if (toggleMode && _sprintToggledOn && _moveInput.sqrMagnitude < 0.01f)
            {
                if (IsGroundedForSprintReset())
                {
                    _sprintToggledOn = false;
                }
            }
        }

        // Helper check - in a pure Input Handler we might not know grounding, but we can just check input mag.
        // For now, input magnitude check is robust enough for "Stop Moving".
        private bool IsGroundedForSprintReset() => true;

        private void LateUpdate()
        {
            // Reset one-frame triggers
            _jumpTriggered = false;
            SprintJustActivated = false;
            _dashTriggered = false;
            _noclipTriggered = false;
            _lookDelta = Vector2.zero;
        }

        private void OnSprintPress()
        {
            _sprintHeld = true;
            bool toggleMode = _config != null ? _config.ToggleSprint : true;

            if (toggleMode)
            {
                // Toggle mode: Only toggle if moving (prevents weird idle sprint state)
                if (_moveInput.sqrMagnitude > 0.01f)
                {
                    _sprintToggledOn = !_sprintToggledOn;
                    if (_sprintToggledOn)
                        SprintJustActivated = true;
                }
            }
            else
            {
                // Hold mode: Activated on press
                SprintJustActivated = true;
            }
        }

        private void OnSprintRelease()
        {
            _sprintHeld = false;

            // Toggle mode ignores release
            // Hold mode uses _sprintHeld prop
        }
    }
}
