using UnityEngine;

namespace Antigravity.Controllers
{
    [CreateAssetMenu(
        fileName = "PlayerMovementConfig",
        menuName = "Antigravity/Player Movement Config"
    )]
    /// <summary>
    /// Configuration asset for Player Movement physics and abilities.
    /// <para>Defines speeds, forces, cooldowns, and toggle settings.</para>
    /// </summary>
    public class PlayerMovementConfig : ScriptableObject
    {
        [Header("Movement")]
        [Tooltip("Standard movement speed on ground.")]
        public float MaxStableMoveSpeed = 8f;

        [Tooltip("How quickly the player accelerates/decelerates on ground.")]
        public float StableMovementSharpness = 15f;

        [Tooltip("How quickly the character rotates to face input direction.")]
        public float OrientationSharpness = 10f;

        [Tooltip("Max distance from ledge before falling off.")]
        public float MaxStableDistanceFromLedge = 5f;

        [Range(0f, 180f)]
        public float MaxStableDenivelationAngle = 180f;

        [Header("Sprint")]
        public float MaxSprintMoveSpeed = 15f;

        [Header("Dash")]
        [Tooltip("Impulse force applied when dashing.")]
        public float DashForce = 15f;

        [Tooltip("Minimum time between dashes (prevents input spam).")]
        public float DashIntermissionTime = 0.1f;

        [Tooltip("Maximum number of dash charges available.")]
        public int MaxDashCharges = 3;

        [Tooltip("Time (seconds) to regenerate one dash charge.")]
        public float DashReloadTime = 2.0f;

        [Tooltip("If true, pressing Sprint toggles it on/off. If false, hold is required.")]
        public bool ToggleSprint = true;

        [Tooltip("If true, pressing Crouch toggles it on/off. If false, hold is required.")]
        public bool ToggleCrouch = false; // Default: Hold (traditional FPS)

        [Header("Slide")]
        [Tooltip("Base slide speed before slope modifiers.")]
        public float BaseSlideSpeed = 12f;

        [Tooltip("How much slope angle affects slide speed (higher = more influence).")]
        public float SlideGravityInfluence = 5f;

        [Tooltip("Friction rate during slide (higher = faster deceleration).")]
        public float SlideFriction = 0.8f;

        [Tooltip("Minimum speed to maintain slide (auto-exit if slower).")]
        public float MinSlideSpeedToMaintain = 3f;

        [Tooltip("Max slide duration in seconds (0 = infinite, speed-based only).")]
        public float MaxSlideDuration = 0f;

        [Tooltip("Minimum time (seconds) to wait before sliding again.")]
        public float SlideCooldown = 0.5f;

        [Header("Air Movement")]
        public float MaxAirMoveSpeed = 10f;
        public float AirAccelerationSpeed = 5f;
        public float Drag = 0.1f;

        [Header("Jumping")]
        public bool AllowJumpingWhenSliding = false;
        public bool AllowDoubleJump = true;
        public bool AllowWallJump = true;
        public float JumpSpeed = 10f;

        [Tooltip("Jump Buffer: How long (seconds) a jump input is remembered before landing")]
        public float JumpPreGroundingGraceTime = 0.15f;

        [Tooltip("Coyote Time: How long (seconds) after leaving ground you can still jump")]
        public float JumpPostGroundingGraceTime = 0.1f;

        [Header("NoClip")]
        public float NoClipMoveSpeed = 10f;
        public float NoClipSharpness = 15;

        [Header("Misc")]
        public bool OrientTowardsGravity = false;
        public Vector3 Gravity = new Vector3(0, -30f, 0);
    }
}
