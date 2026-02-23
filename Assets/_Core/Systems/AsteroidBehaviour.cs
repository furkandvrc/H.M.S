using UnityEngine;
using Unity.Netcode;

namespace HMS.Systems
{
    /// <summary>
    /// Server-authoritative asteroid movement.
    /// Each asteroid has its own forward velocity.
    /// FloatingWorldManager also passes a worldDrift vector every tick
    /// that shifts ALL asteroids based on pilot steering.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class AsteroidBehaviour : NetworkBehaviour
    {
        private Vector3 _moveDirection;
        private float _moveSpeed;
        private float _rotationSpeed;
        private Vector3 _rotationAxis;

        public void Initialize(Vector3 direction, float speed, float rotationSpeed)
        {
            _moveDirection = direction.normalized;
            _moveSpeed = speed;
            _rotationSpeed = rotationSpeed;
            _rotationAxis = Random.onUnitSphere;
        }

        /// <summary>
        /// Called by FloatingWorldManager each server tick.
        /// worldDrift = pilot steering converted to lateral movement for ALL asteroids.
        /// This is what makes "the world move, not the ship".
        /// </summary>
        public void ServerUpdate(float deltaTime, Vector3 worldDrift)
        {
            // Own velocity (toward ship) + world drift (pilot steering)
            Vector3 velocity = _moveDirection * _moveSpeed + worldDrift;
            transform.position += velocity * deltaTime;
            transform.Rotate(_rotationAxis, _rotationSpeed * deltaTime, Space.Self);
        }

        public bool ShouldDespawn(float despawnBehind)
        {
            return transform.position.z < -despawnBehind;
        }
    }
}
