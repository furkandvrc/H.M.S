using UnityEngine;

namespace HMS.Ship
{
    /// <summary>
    /// Purely visual feedback for the ship model based on pilot steering.
    /// 
    /// CRITICAL: This script NEVER changes position. Ship stays at (0,0,0).
    /// Only rotation is affected to create the illusion of turning.
    /// 
    /// Reads CurrentSteering from FloatingWorldManager (synced via NetworkVariable).
    /// Works on both server and clients.
    /// </summary>
    public class ShipVisualFeedback : MonoBehaviour
    {
        [Header("Tilt Settings")]
        [Tooltip("Max bank angle when steering horizontally (roll)")]
        [SerializeField] private float maxRollAngle = 25f;

        [Tooltip("Max pitch angle when steering vertically")]
        [SerializeField] private float maxPitchAngle = 10f;

        [Tooltip("How fast the visual tilt responds")]
        [SerializeField] private float tiltSmooth = 5f;

        private float _currentRoll;
        private float _currentPitch;

        private void LateUpdate()
        {
            var manager = Systems.FloatingWorldManager.Instance;
            if (manager == null) return;

            Vector2 steering = manager.CurrentSteering;

            // Bank into the turn (steer right → roll left visually)
            float targetRoll = -steering.x * maxRollAngle;
            float targetPitch = steering.y * maxPitchAngle;

            _currentRoll = Mathf.Lerp(_currentRoll, targetRoll, tiltSmooth * Time.deltaTime);
            _currentPitch = Mathf.Lerp(_currentPitch, targetPitch, tiltSmooth * Time.deltaTime);

            // ONLY rotation - position stays at (0,0,0) ALWAYS
            transform.localRotation = Quaternion.Euler(_currentPitch, 0f, _currentRoll);
        }
    }
}
