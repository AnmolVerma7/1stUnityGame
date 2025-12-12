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
        // ═══════════════════════════════════════════════════════════════════════
        // GROUND MOVEMENT
        // ═══════════════════════════════════════════════════════════════════════

        [Header("Ground Movement")]
        [Tooltip("Standard movement speed on ground.")]
        public float MaxStableMoveSpeed = 8f;

        [Tooltip("How quickly the player accelerates/decelerates on ground.")]
        public float StableMovementSharpness = 15f;

        [Tooltip("How quickly the character rotates to face input direction.")]
        public float OrientationSharpness = 10f;

        [Tooltip("Max distance from ledge before falling off.")]
        public float MaxStableDistanceFromLedge = 5f;

        [Range(0f, 180f)]
        [Tooltip("Max angle for stable ground (180 = any surface).")]
        public float MaxStableDenivelationAngle = 180f;

        // ═══════════════════════════════════════════════════════════════════════
        // SPRINT
        // ═══════════════════════════════════════════════════════════════════════

        [Header("Sprint")]
        [Tooltip("Movement speed while sprinting.")]
        public float MaxSprintMoveSpeed = 15f;

        [Tooltip("If true, pressing Sprint toggles it on/off. If false, hold is required.")]
        public bool ToggleSprint = true;

        // ═══════════════════════════════════════════════════════════════════════
        // CROUCH
        // ═══════════════════════════════════════════════════════════════════════

        [Header("Crouch")]
        [Tooltip("If true, pressing Crouch toggles it on/off. If false, hold is required.")]
        public bool ToggleCrouch = false;

        // ═══════════════════════════════════════════════════════════════════════
        // SLIDE
        // ═══════════════════════════════════════════════════════════════════════

        [Header("Slide - Entry")]
        [Tooltip(
            "If true, slide continues until speed loss. If false, releasing crouch exits slide."
        )]
        public bool ToggleSlide = false;

        [Tooltip(
            "Time (seconds) required to hold Crouch before slide starts (only for Hold Mode)."
        )]
        public float SlideHoldDelay = 0.2f;

        [Tooltip("Minimum time (seconds) to wait before sliding again.")]
        public float SlideCooldown = 0.5f;

        [Tooltip("Minimum speed required to initiate a slide.")]
        public float MinSlideEntrySpeed = 1f;

        [Header("Slide - Momentum Physics")]
        [Tooltip("Speed multiplier applied at the start of the slide (burst of speed).")]
        public float SlideSpeedBoost = 1.2f;

        [Tooltip("Maximum time (in seconds) a slide can last.")]
        public float MaxSlideDuration = 1.5f;

        [Tooltip("Curve controlling how slide speed falls off over time (0=start at 100%, 1=end).")]
        public AnimationCurve SlideFrictionCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0.3f);

        [Tooltip("How strongly slope affects the slide speed (positive = downhill boost).")]
        public float SlopeInfluence = 0.5f;

        [Tooltip("How much the player can steer left/right while sliding.")]
        public float SlideSteerStrength = 2f;

        [Tooltip("If speed falls below this, the slide will auto-exit.")]
        public float MinSlideExitSpeed = 2f;

        // ═══════════════════════════════════════════════════════════════════════
        // DASH
        // ═══════════════════════════════════════════════════════════════════════

        [Header("Dash")]
        [Tooltip("Impulse force applied when dashing.")]
        public float DashForce = 15f;

        [Tooltip("Minimum time between dashes (prevents input spam).")]
        public float DashIntermissionTime = 0.1f;

        [Tooltip("Maximum number of dash charges available.")]
        public int MaxDashCharges = 3;

        [Tooltip("Time (seconds) to regenerate one dash charge.")]
        public float DashReloadTime = 2.0f;

        // ═══════════════════════════════════════════════════════════════════════
        // AIR MOVEMENT
        // ═══════════════════════════════════════════════════════════════════════

        [Header("Air Movement")]
        [Tooltip("Maximum horizontal movement speed in air.")]
        public float MaxAirMoveSpeed = 10f;

        [Tooltip("Acceleration speed when moving in air.")]
        public float AirAccelerationSpeed = 5f;

        [Tooltip("Air drag coefficient (higher = more resistance).")]
        public float Drag = 0.1f;

        // ═══════════════════════════════════════════════════════════════════════
        // JUMPING
        // ═══════════════════════════════════════════════════════════════════════

        [Header("Jumping")]
        [Tooltip("Allow jumping while in slide state.")]
        public bool AllowJumpingWhenSliding = false;

        [Tooltip("Allow double jump in air.")]
        public bool AllowDoubleJump = true;

        [Tooltip("Allow jumping off walls.")]
        public bool AllowWallJump = true;

        [Tooltip("Vertical speed applied on jump.")]
        public float JumpSpeed = 10f;

        [Tooltip("Forward speed added in direction of input when jumping.")]
        public float JumpScalableForwardSpeed = 10f;

        [Tooltip("Vertical speed applied on double jump.")]
        public float DoubleJumpSpeed = 10f;

        [Tooltip("Forward speed added in direction of input when double jumping.")]
        public float DoubleJumpScalableForwardSpeed = 10f;

        [Tooltip("Jump Buffer: How long (seconds) a jump input is remembered before landing.")]
        public float JumpPreGroundingGraceTime = 0.15f;

        [Tooltip("Coyote Time: How long (seconds) after leaving ground you can still jump.")]
        public float JumpPostGroundingGraceTime = 0.1f;

        // ═══════════════════════════════════════════════════════════════════════
        // NOCLIP
        // ═══════════════════════════════════════════════════════════════════════

        [Header("NoClip")]
        [Tooltip("Movement speed in noclip mode.")]
        public float NoClipMoveSpeed = 10f;

        [Tooltip("Acceleration sharpness in noclip mode.")]
        public float NoClipSharpness = 15f;

        // ═══════════════════════════════════════════════════════════════════════
        // PHYSICS
        // ═══════════════════════════════════════════════════════════════════════
        // MANTLE
        // ═══════════════════════════════════════════════════════════════════════

        [Header("Mantle")]
        [Tooltip(
            "Layers to check for mantleable surfaces. Set to specific layers for optimization."
        )]
        public LayerMask MantleLayers = -1; // -1 = Everything

        [Tooltip("Maximum horizontal distance from ledge to allow grab.")]
        public float MaxGrabDistance = 0.3f;

        [Tooltip("Minimum ledge height that can be mantled.")]
        public float MinLedgeHeight = 1.0f;

        [Tooltip("Maximum ledge height that can be mantled.")]
        public float MaxLedgeHeight = 2.5f;

        [Tooltip("Duration of the mantle animation (seconds).")]
        public float MantleDuration = 0.55f;

        [Tooltip("Curve for overall mantle timing (controls arc motion smoothness).")]
        public AnimationCurve MantleCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 2f), // Start slow, accelerate
            new Keyframe(0.5f, 0.5f, 1.5f, 1.5f), // Middle: smooth transition
            new Keyframe(1f, 1f, 2f, 0f) // End slow, decelerate
        );

        [Header("Shimmy")]
        [Tooltip("Horizontal speed while shimmying along ledge.")]
        public float ShimmySpeed = 2.0f;

        [Tooltip("Distance to check ahead for ledge continuation.")]
        public float ShimmyCheckDistance = 0.5f;

        [Tooltip("Minimum input threshold to start shimmy (dead zone).")]
        public float ShimmyInputThreshold = 0.3f;

        // ═══════════════════════════════════════════════════════════════════════
        // PHYSICS
        // ═══════════════════════════════════════════════════════════════════════

        [Header("Physics")]
        [Tooltip("If true, character orients to gravity direction.")]
        public bool OrientTowardsGravity = false;

        [Tooltip("Gravity vector applied to character.")]
        public Vector3 Gravity = new Vector3(0, -30f, 0);
    }
}
