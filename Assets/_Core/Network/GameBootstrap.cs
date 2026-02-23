using UnityEngine;
using Unity.Netcode;

namespace HMS.Network
{
    /// <summary>
    /// Handles early game initialization before network session starts.
    /// Configures NetworkManager settings at runtime.
    /// Must run after NetworkManager.Awake() - uses Start() for safety.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class GameBootstrap : MonoBehaviour
    {
        [Header("Player Spawn")]
        [Tooltip("Enable this when the ship interior is ready for player spawning")]
        [SerializeField] private bool enablePlayerSpawn = false;

        private void Start()
        {
            ConfigurePlayerSpawn();
        }

        private void ConfigurePlayerSpawn()
        {
            // Try Singleton first, then fallback to GetComponent on same object
            var nm = NetworkManager.Singleton;
            if (nm == null)
            {
                nm = GetComponent<NetworkManager>();
            }

            if (nm == null)
            {
                Debug.LogError("[GameBootstrap] NetworkManager not found!");
                return;
            }

            if (!enablePlayerSpawn)
            {
                nm.NetworkConfig.PlayerPrefab = null;
                Debug.Log("[GameBootstrap] Player spawning DISABLED. Ship camera mode active.");
            }
            else
            {
                Debug.Log("[GameBootstrap] Player spawning ENABLED.");
            }
        }
    }
}