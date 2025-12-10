using UnityEngine;

namespace Antigravity.Controllers
{
    [CreateAssetMenu(
        fileName = "PlayerMovementConfig",
        menuName = "Antigravity/Player Movement Config"
    )]
    public class PlayerMovementConfig : ScriptableObject
    {
        [Header("Movement")]
        public float MaxStableMoveSpeed = 8f;
        public float StableMovementSharpness = 15f;
        public float OrientationSharpness = 10f;
        public float MaxStableDistanceFromLedge = 5f;

        [Range(0f, 180f)]
        public float MaxStableDenivelationAngle = 180f;

        [Header("Sprint")]
        public float MaxSprintMoveSpeed = 15f;

        [Header("Dash")]
        public float DashForce = 15f; // Increased default since it's a dedicated dash now? User liked 10, maybe 15 for "Dash". I'll stick to 10 for now as per "Map red to dash".
        public float DashCooldown = 1.0f;

        [Tooltip("If true, pressing Sprint toggles it on/off. If false, hold is required.")]
        public bool ToggleSprint = true;

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
