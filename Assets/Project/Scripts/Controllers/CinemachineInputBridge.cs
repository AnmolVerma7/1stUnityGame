using Unity.Cinemachine;
using UnityEngine;

namespace Antigravity.Controllers
{
    /// <summary>
    /// Bridges the PlayerInputHandler to Cinemachine 3's Orbital Follow component.
    /// <para>
    /// Manually drives the Horizontal (Yaw) and Vertical (Pitch) axes using our custom input system.
    /// This replaces the default CinemachineInputAxisController, giving full control over sensitivity
    /// and inversion while supporting both mouse (delta) and gamepad (rate) inputs.
    /// </para>
    /// </summary>
    public class CinemachineInputBridge : MonoBehaviour
    {
        #region Inspector Fields

        [Header("References")]
        [Tooltip("The input handler that provides Look input. Auto-found if not assigned.")]
        [SerializeField]
        private PlayerInputHandler _inputHandler;

        [Tooltip("The Cinemachine Orbital Follow component to drive. Auto-found if not assigned.")]
        [SerializeField]
        private CinemachineOrbitalFollow _orbitalFollow;

        [Header("Settings")]
        [Tooltip("Sensitivity multiplier for look input (X = Horizontal, Y = Vertical).")]
        [SerializeField]
        private Vector2 _lookSensitivity = new Vector2(1f, 1f);

        [Tooltip("Multiplier for gamepad stick input to match mouse delta feel.")]
        [SerializeField]
        private float _gamepadRateScale = 200f;

        [Tooltip(
            "When true, moving stick/mouse UP looks UP. When false, inverted (flight sim style)."
        )]
        [SerializeField]
        private bool _invertY = true;

        #endregion

        #region Public Properties

        /// <summary>
        /// The input handler providing look input.
        /// </summary>
        public PlayerInputHandler InputHandler
        {
            get => _inputHandler;
            set => _inputHandler = value;
        }

        /// <summary>
        /// The Cinemachine Orbital Follow component being driven.
        /// </summary>
        public CinemachineOrbitalFollow OrbitalFollow
        {
            get => _orbitalFollow;
            set => _orbitalFollow = value;
        }

        /// <summary>
        /// Look sensitivity (X = Horizontal, Y = Vertical).
        /// </summary>
        public Vector2 LookSensitivity
        {
            get => _lookSensitivity;
            set => _lookSensitivity = value;
        }

        /// <summary>
        /// Multiplier for gamepad rate input.
        /// </summary>
        public float GamepadRateScale
        {
            get => _gamepadRateScale;
            set => _gamepadRateScale = value;
        }

        /// <summary>
        /// Whether Y-axis is inverted (true = natural, false = flight sim).
        /// </summary>
        public bool InvertY
        {
            get => _invertY;
            set => _invertY = value;
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            AutoFindReferences();
        }

        private void Update()
        {
            if (_inputHandler == null || _orbitalFollow == null)
                return;

            ApplyLookInput();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Auto-finds required components if not assigned in the Inspector.
        /// </summary>
        private void AutoFindReferences()
        {
            if (_inputHandler == null)
            {
                _inputHandler = FindFirstObjectByType<PlayerInputHandler>();
            }

            if (_orbitalFollow == null)
            {
                _orbitalFollow = GetComponent<CinemachineOrbitalFollow>();
            }

            // CM3 often has OrbitalFollow on parent/child - search hierarchy
            if (_orbitalFollow == null)
            {
                _orbitalFollow = GetComponentInParent<CinemachineOrbitalFollow>();
            }
        }

        /// <summary>
        /// Reads input from the handler and applies it to the orbital follow axes.
        /// </summary>
        private void ApplyLookInput()
        {
            // Get raw input values
            Vector2 deltaInput = _inputHandler.LookDelta; // Mouse: per-frame pixels
            Vector2 rateInput = _inputHandler.LookRate; // Gamepad: -1 to 1 continuous

            // Safety: Ignore mouse delta when cursor is unlocked (e.g., in menus)
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                deltaInput = Vector2.zero;
            }

            // Calculate combined input with sensitivity
            // Mouse: Sensitivity * Delta (already frame-independent)
            // Gamepad: Sensitivity * Rate * DeltaTime * Scale (make it frame-independent)
            float xInput =
                (deltaInput.x * _lookSensitivity.x)
                + (
                    rateInput.x
                    * _lookSensitivity.x
                    * _gamepadRateScale
                    * UnityEngine.Time.deltaTime
                );
            float yInput =
                (deltaInput.y * _lookSensitivity.y)
                + (
                    rateInput.y
                    * _lookSensitivity.y
                    * _gamepadRateScale
                    * UnityEngine.Time.deltaTime
                );

            // Apply Y inversion (when false, invert the input)
            if (!_invertY)
            {
                yInput = -yInput;
            }

            // Apply to Horizontal axis (Yaw)
            _orbitalFollow.HorizontalAxis.Value += xInput;
            if (!_orbitalFollow.HorizontalAxis.Wrap)
            {
                _orbitalFollow.HorizontalAxis.Value = Mathf.Clamp(
                    _orbitalFollow.HorizontalAxis.Value,
                    _orbitalFollow.HorizontalAxis.Range.x,
                    _orbitalFollow.HorizontalAxis.Range.y
                );
            }

            // Apply to Vertical axis (Pitch)
            _orbitalFollow.VerticalAxis.Value += yInput;
            if (!_orbitalFollow.VerticalAxis.Wrap)
            {
                _orbitalFollow.VerticalAxis.Value = Mathf.Clamp(
                    _orbitalFollow.VerticalAxis.Value,
                    _orbitalFollow.VerticalAxis.Range.x,
                    _orbitalFollow.VerticalAxis.Range.y
                );
            }
        }

        #endregion
    }
}
