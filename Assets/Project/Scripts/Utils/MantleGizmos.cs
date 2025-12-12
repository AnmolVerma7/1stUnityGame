using Antigravity.Controllers;
using UnityEngine;

namespace Antigravity.Utils
{
    /// <summary>
    /// Visualizes mantle detection parameters in the Scene view.
    /// Attach to player object to see live gizmos for grab distance, ledge heights, etc.
    /// </summary>
    public class MantleGizmos : MonoBehaviour
    {
        // ═══════════════════════════════════════════════════════════════════════
        // REFERENCES
        // ═══════════════════════════════════════════════════════════════════════
        [Header("References")]
        [Tooltip("Reference to the player's movement config")]
        [SerializeField]
        private PlayerMovementConfig _config;

        // ═══════════════════════════════════════════════════════════════════════
        // GIZMO SETTINGS
        // ═══════════════════════════════════════════════════════════════════════
        [Header("Gizmo Colors")]
        [SerializeField]
        private Color _grabDistanceColor = new Color(0f, 1f, 1f, 0.8f); // Cyan

        [SerializeField]
        private Color _minLedgeHeightColor = new Color(1f, 0.5f, 0f, 0.8f); // Orange

        [SerializeField]
        private Color _maxLedgeHeightColor = new Color(0f, 1f, 0f, 0.8f); // Green

        [SerializeField]
        private Color _ledgeRangeColor = new Color(1f, 1f, 0f, 0.3f); // Yellow (transparent)

        [Header("Display Options")]
        [SerializeField]
        private bool _showGrabDistance = true;

        [SerializeField]
        private bool _showLedgeHeights = true;

        [SerializeField]
        private bool _showLedgeRange = true;

        // ═══════════════════════════════════════════════════════════════════════
        // GIZMO DRAWING
        // ═══════════════════════════════════════════════════════════════════════
        private void OnDrawGizmos()
        {
            if (_config == null)
                return;

            Vector3 origin = transform.position;
            Vector3 forward = transform.forward;
            Vector3 up = Vector3.up;

            // Grab Distance - Ray showing how far we check for walls
            if (_showGrabDistance)
            {
                Gizmos.color = _grabDistanceColor;
                Vector3 grabEnd = origin + forward * _config.MaxGrabDistance;
                Gizmos.DrawLine(origin, grabEnd);
                Gizmos.DrawWireSphere(grabEnd, 0.05f);
            }

            // Wall hit point (end of grab distance)
            Vector3 wallPoint = origin + forward * _config.MaxGrabDistance;

            // Min Ledge Height - Horizontal line at minimum grabbable height
            if (_showLedgeHeights)
            {
                Gizmos.color = _minLedgeHeightColor;
                Vector3 minHeightPoint = wallPoint + up * _config.MinLedgeHeight;
                DrawHorizontalLine(minHeightPoint, 0.5f);
                Gizmos.DrawWireSphere(minHeightPoint, 0.03f);
            }

            // Max Ledge Height - Horizontal line at maximum grabbable height
            if (_showLedgeHeights)
            {
                Gizmos.color = _maxLedgeHeightColor;
                Vector3 maxHeightPoint = wallPoint + up * _config.MaxLedgeHeight;
                DrawHorizontalLine(maxHeightPoint, 0.5f);
                Gizmos.DrawWireSphere(maxHeightPoint, 0.03f);
            }

            // Ledge Range - Transparent box showing grabbable zone
            if (_showLedgeRange)
            {
                Gizmos.color = _ledgeRangeColor;
                float rangeHeight = _config.MaxLedgeHeight - _config.MinLedgeHeight;
                float centerY = _config.MinLedgeHeight + (rangeHeight / 2f);
                Vector3 rangeCenter = wallPoint + up * centerY;
                Vector3 rangeSize = new Vector3(0.8f, rangeHeight, 0.1f);

                // Rotate box to face forward
                Matrix4x4 oldMatrix = Gizmos.matrix;
                Gizmos.matrix = Matrix4x4.TRS(rangeCenter, transform.rotation, Vector3.one);
                Gizmos.DrawCube(Vector3.zero, rangeSize);
                Gizmos.DrawWireCube(Vector3.zero, rangeSize);
                Gizmos.matrix = oldMatrix;
            }
        }

        /// <summary>
        /// Draws a horizontal line perpendicular to forward direction.
        /// </summary>
        private void DrawHorizontalLine(Vector3 center, float width)
        {
            Vector3 right = transform.right;
            Vector3 start = center - right * (width / 2f);
            Vector3 end = center + right * (width / 2f);
            Gizmos.DrawLine(start, end);
        }
    }
}
