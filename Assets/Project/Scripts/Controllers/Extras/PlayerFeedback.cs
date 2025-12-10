using Antigravity.Controllers;
using KinematicCharacterController;
using UnityEngine;

namespace Antigravity.Feedback
{
    /// <summary>
    /// A decoupled feedback script to visualize player state (Sprint, Speed, etc.)
    /// Simply drop this on the Player object.
    /// </summary>
    public class PlayerFeedback : MonoBehaviour
    {
        #region Inspector Fields

        [Header("References")]
        [Tooltip("Input handler reference (auto-found if null).")]
        public PlayerInputHandler InputHandler;

        [Tooltip("KCC Motor reference (auto-found if null).")]
        public KinematicCharacterMotor Motor;

        [Tooltip("Main camera for FOV effects.")]
        public Camera TargetCamera;

        [Tooltip("Renderer to change color on (auto-found if null).")]
        // Try to find a renderer to change color
        public Renderer TargetRenderer;

        [Header("Settings")]
        public Color NormalColor = Color.white;
        public Color SprintColor = Color.cyan;
        public Color DashColor = Color.red;

        [Tooltip("How long the red dash color persists.")]
        public float DashVisualDuration = 0.5f;

        [Header("FOV Settings")]
        public float BaseFOV = 60f;
        public float SprintFOV = 80f;
        public float FOVSharpness = 10f;

        #endregion

        #region Private State

        private float _dashTimer;
        private PlayerController _controller; // To access charges
        #endregion

        #region Unity Lifecycle
        private void Start()
        {
            if (InputHandler == null)
                InputHandler = GetComponent<PlayerInputHandler>();
            if (Motor == null)
                Motor = GetComponent<KinematicCharacterMotor>();
            if (TargetCamera == null)
                TargetCamera = Camera.main;

            _controller = GetComponent<PlayerController>();

            // Auto-find a renderer if not assigned (MeshRoot or self)
            if (TargetRenderer == null)
            {
                TargetRenderer = GetComponentInChildren<Renderer>();
            }
        }

        private void Update()
        {
            if (InputHandler == null)
                return;

            bool isSprinting = InputHandler.IsSprinting; // Read state from input

            // Trigger Dash Visuals
            if (InputHandler.DashJustActivated)
            {
                _dashTimer = DashVisualDuration;
            }

            if (_dashTimer > 0)
            {
                _dashTimer -= UnityEngine.Time.deltaTime;
            }

            // 1. Color Change (Visual Debug)
            if (TargetRenderer != null)
            {
                Color targetColor = NormalColor;

                if (_dashTimer > 0)
                    targetColor = DashColor;
                else if (isSprinting)
                    targetColor = SprintColor;

                // Simple material color change (works for standard shaders)
                TargetRenderer.material.color = Color.Lerp(
                    TargetRenderer.material.color,
                    targetColor,
                    UnityEngine.Time.deltaTime * 10f
                );
            }

            // 2. FOV Change (Game Feel)
            if (TargetCamera != null)
            {
                float targetFOV = isSprinting ? SprintFOV : BaseFOV;
                TargetCamera.fieldOfView = Mathf.Lerp(
                    TargetCamera.fieldOfView,
                    targetFOV,
                    UnityEngine.Time.deltaTime * FOVSharpness
                );
            }
        }

        #endregion

        #region GUI

        private void OnGUI()
        {
            if (Motor == null)
                return;

            // 3. Simple Speedometer (Decoupled UI)
            float speed = Motor.Velocity.magnitude;
            float horizontalSpeed = Vector3.ProjectOnPlane(Motor.Velocity, Vector3.up).magnitude;

            GUIStyle style = new GUIStyle();
            style.fontSize = 24;
            style.normal.textColor = Color.white;
            style.fontStyle = FontStyle.Bold;

            GUILayout.BeginArea(new Rect(20, 20, 300, 150)); // Height increased for charges
            GUILayout.Label($"Speed: {speed:F1} m/s", style);
            GUILayout.Label($"H-Speed: {horizontalSpeed:F1} m/s", style);

            string status = "OFF";
            if (_dashTimer > 0)
                status = "DASH! 💥";
            else if (InputHandler.IsSprinting)
                status = "ON";

            GUILayout.Label($"Sprint: {status}", style);

            // Show Charges
            if (_controller != null)
            {
                GUILayout.Label($"Charges: {_controller.CurrentDashCharges:F0}", style);
            }

            GUILayout.EndArea();
        }

        #endregion
    }
}
