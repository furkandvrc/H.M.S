using UnityEngine;

namespace HMS.Ship
{
    /// <summary>
    /// Subtle parallax camera that enhances the illusion of ship movement.
    /// 
    /// The camera sits behind the ship at a fixed base position.
    /// When the pilot steers, the camera shifts slightly in the OPPOSITE direction,
    /// creating a parallax effect that sells the turning illusion.
    /// 
    /// The ship never moves - this is purely a camera trick.
    /// </summary>
    public class ShipCamera : MonoBehaviour
    {
        [Header("Base Position")]
        [SerializeField] private Vector3 basePosition = new Vector3(0f, 4f, -12f);
        [SerializeField] private Vector3 lookTarget = new Vector3(0f, 0f, 20f);

        [Header("Parallax Effect")]
        [Tooltip("How much the camera shifts opposite to steering")]
        [SerializeField] private float parallaxStrength = 2f;
        [SerializeField] private float parallaxSmooth = 3f;

        private Vector3 _currentOffset;

        private void LateUpdate()
        {
            var manager = Systems.FloatingWorldManager.Instance;
            Vector2 steering = manager != null ? manager.CurrentSteering : Vector2.zero;

            // Camera shifts OPPOSITE to steering for parallax depth effect
            // Pilot steers right → camera drifts slightly left → world feels like it moved
            Vector3 targetOffset = new Vector3(
                -steering.x * parallaxStrength,
                -steering.y * parallaxStrength * 0.5f,
                0f
            );

            _currentOffset = Vector3.Lerp(_currentOffset, targetOffset, parallaxSmooth * Time.deltaTime);

            // Apply position
            transform.position = basePosition + _currentOffset;

            // Always look ahead
            transform.LookAt(lookTarget + _currentOffset * 0.5f);
        }
    }
}
