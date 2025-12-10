using KinematicCharacterController;
using UnityEngine;

namespace Antigravity.Controllers
{
    /// <summary>
    /// Adapter class that implements KCC's ICharacterController interface.
    /// Redirects physics callbacks to the PlayerController, keeping the main class clean of boilerplate.
    /// </summary>
    public class PlayerKCCAdapter : ICharacterController
    {
        private readonly PlayerController _controller;

        public PlayerKCCAdapter(PlayerController controller)
        {
            _controller = controller;
        }

        #region Physics Updates

        public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
        {
            _controller.HandleRotation(ref currentRotation, deltaTime);
        }

        public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            _controller.HandleVelocity(ref currentVelocity, deltaTime);
        }

        public void AfterCharacterUpdate(float deltaTime)
        {
            _controller.HandleAfterUpdate(deltaTime);
        }

        public void BeforeCharacterUpdate(float deltaTime)
        {
            // Optional: Forward if needed
        }

        #endregion

        #region Collision Queries

        public bool IsColliderValidForCollisions(Collider coll)
        {
            return !_controller.IgnoredColliders.Contains(coll);
        }

        public void OnMovementHit(
            Collider hitCollider,
            Vector3 hitNormal,
            Vector3 hitPoint,
            ref HitStabilityReport hitStabilityReport
        )
        {
            _controller.HandleMovementHit(hitCollider, hitNormal, hitPoint, ref hitStabilityReport);
        }

        public void OnGroundHit(
            Collider hitCollider,
            Vector3 hitNormal,
            Vector3 hitPoint,
            ref HitStabilityReport hitStabilityReport
        )
        {
            // Boilerplate
        }

        public void ProcessHitStabilityReport(
            Collider hitCollider,
            Vector3 hitNormal,
            Vector3 hitPoint,
            Vector3 atCharacterPosition,
            Quaternion atCharacterRotation,
            ref HitStabilityReport hitStabilityReport
        )
        {
            // Boilerplate
        }

        public void PostGroundingUpdate(float deltaTime)
        {
            // Boilerplate
        }

        public void OnDiscreteCollisionDetected(Collider hitCollider)
        {
            // Boilerplate
        }

        #endregion
    }
}
