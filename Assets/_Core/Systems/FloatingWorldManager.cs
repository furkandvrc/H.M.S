using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HMS.Systems
{
    /// <summary>
    /// Server-authoritative floating world system.
    /// 
    /// CORE PRINCIPLE: The ship NEVER moves. It stays at (0,0,0).
    /// The world (asteroids, debris, space FX) flows TOWARD and PAST the ship.
    /// 
    /// Pilot steering applies a WORLD DRIFT to ALL asteroids:
    /// - Pilot steers right → ALL asteroids drift LEFT → illusion of ship turning right
    /// - This affects every asteroid currently alive, not just new spawns
    /// </summary>
    public class FloatingWorldManager : NetworkBehaviour
    {
        public static FloatingWorldManager Instance { get; private set; }

        [Header("Asteroid Prefabs")]
        [SerializeField] private GameObject[] asteroidPrefabs;

        [Header("Spawn Settings")]
        [SerializeField] private float spawnDistance = 150f;
        [SerializeField] private float despawnBehind = 50f;
        [SerializeField] private int maxAsteroids = 20;
        [SerializeField] private float spawnInterval = 0.6f;

        [Header("Spawn Area (centered on ship path)")]
        [Tooltip("Inner zone: asteroids that WILL hit the ship if you don't dodge")]
        [SerializeField] private float dangerWidth = 8f;
        [SerializeField] private float dangerHeight = 5f;
        [Tooltip("Outer zone: ambient asteroids that fly past the sides")]
        [SerializeField] private float ambientWidth = 40f;
        [SerializeField] private float ambientHeight = 20f;
        [Tooltip("Chance (0-1) of spawning in the danger zone vs ambient")]
        [SerializeField] private float dangerZoneChance = 0.5f;

        [Header("Asteroid Speed")]
        [SerializeField] private float minSpeed = 40f;
        [SerializeField] private float maxSpeed = 70f;

        [Header("Asteroid Rotation")]
        [SerializeField] private float minRotationSpeed = 10f;
        [SerializeField] private float maxRotationSpeed = 40f;

        [Header("Scale Variation")]
        [SerializeField] private float minScale = 0.3f;
        [SerializeField] private float maxScale = 1.5f;

        [Header("Pilot Steering → World Drift")]
        [Tooltip("How fast the world drifts laterally when pilot steers (units/sec at full input)")]
        [SerializeField] private float worldDriftSpeed = 30f;
        [Tooltip("How fast steering responds to input")]
        [SerializeField] private float steerSmooth = 4f;

#if UNITY_EDITOR
        [Header("Fallback Paths (Editor Only)")]
        [SerializeField] private string[] asteroidPrefabPaths = new string[]
        {
            "Assets/_Core/Systems/Prefabs/NetworkAsteroidGroup.prefab"
        };
#endif

        // Network synced so clients can read for visuals (ship tilt, camera parallax)
        private NetworkVariable<Vector2> _networkSteering = new NetworkVariable<Vector2>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>
        /// Current steering. Read by ShipVisualFeedback and ShipCamera.
        /// </summary>
        public Vector2 CurrentSteering => _networkSteering.Value;

        private readonly List<AsteroidBehaviour> _activeAsteroids = new List<AsteroidBehaviour>();
        private float _spawnTimer;
        private Vector2 _smoothSteering;
        private Keyboard _keyboard;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            ValidatePrefabReferences();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer)
            {
                _spawnTimer = 0f;
                _keyboard = Keyboard.current;
                NetworkManager.NetworkTickSystem.Tick += OnServerTick;
                Debug.Log("[FloatingWorldManager] Server started. World is flowing.");
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                NetworkManager.NetworkTickSystem.Tick -= OnServerTick;
                DespawnAllAsteroids();
            }
            base.OnNetworkDespawn();
        }

        private new void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ─────────────────────────────────────────────
        //  INPUT (every frame for responsiveness)
        // ─────────────────────────────────────────────
        private void Update()
        {
            if (!IsServer || !IsSpawned) return;

            if (_keyboard == null) _keyboard = Keyboard.current;
            if (_keyboard == null) return;

            float h = 0f, v = 0f;
            if (_keyboard.aKey.isPressed || _keyboard.leftArrowKey.isPressed) h -= 1f;
            if (_keyboard.dKey.isPressed || _keyboard.rightArrowKey.isPressed) h += 1f;
            if (_keyboard.wKey.isPressed || _keyboard.upArrowKey.isPressed) v += 1f;
            if (_keyboard.sKey.isPressed || _keyboard.downArrowKey.isPressed) v -= 1f;

            Vector2 target = new Vector2(h, v);
            _smoothSteering = Vector2.Lerp(_smoothSteering, target, steerSmooth * Time.deltaTime);
            _networkSteering.Value = _smoothSteering;
        }

        // ─────────────────────────────────────────────
        //  SERVER TICK
        // ─────────────────────────────────────────────
        private void OnServerTick()
        {
            if (!IsServer) return;

            float tickDelta = 1f / NetworkManager.NetworkTickSystem.TickRate;

            // ── World drift from pilot steering ──
            // Pilot steers right (+X) → world drifts LEFT (-X)
            Vector3 worldDrift = new Vector3(
                -_smoothSteering.x * worldDriftSpeed,
                -_smoothSteering.y * worldDriftSpeed,
                0f
            );

            // ── Spawn ──
            _spawnTimer += tickDelta;
            if (_spawnTimer >= spawnInterval && _activeAsteroids.Count < maxAsteroids)
            {
                _spawnTimer = 0f;
                SpawnAsteroid();
            }

            // ── Update ALL asteroids with world drift ──
            for (int i = _activeAsteroids.Count - 1; i >= 0; i--)
            {
                var asteroid = _activeAsteroids[i];
                if (asteroid == null || !asteroid.IsSpawned)
                {
                    _activeAsteroids.RemoveAt(i);
                    continue;
                }

                // World drift affects EVERY asteroid, not just new ones
                asteroid.ServerUpdate(tickDelta, worldDrift);

                if (asteroid.ShouldDespawn(despawnBehind))
                {
                    asteroid.NetworkObject.Despawn(true);
                    _activeAsteroids.RemoveAt(i);
                }
            }
        }

        // ─────────────────────────────────────────────
        //  SPAWN
        // ─────────────────────────────────────────────
        private void SpawnAsteroid()
        {
            if (asteroidPrefabs == null || asteroidPrefabs.Length == 0) return;

            GameObject prefab = asteroidPrefabs[Random.Range(0, asteroidPrefabs.Length)];

            // Decide: danger zone (will hit ship) or ambient (flies past sides)
            float x, y;
            if (Random.value < dangerZoneChance)
            {
                // DANGER ZONE: centered on ship, these WILL hit if you don't steer
                x = Random.Range(-dangerWidth * 0.5f, dangerWidth * 0.5f);
                y = Random.Range(-dangerHeight * 0.5f, dangerHeight * 0.5f);
            }
            else
            {
                // AMBIENT: wider field for visual richness
                x = Random.Range(-ambientWidth * 0.5f, ambientWidth * 0.5f);
                y = Random.Range(-ambientHeight * 0.5f, ambientHeight * 0.5f);
            }

            Vector3 spawnPos = new Vector3(x, y, spawnDistance);

            // Movement: straight toward ship (-Z) with tiny random wobble
            Vector3 moveDir = new Vector3(
                Random.Range(-0.02f, 0.02f),
                Random.Range(-0.02f, 0.02f),
                -1f
            ).normalized;

            float speed = Random.Range(minSpeed, maxSpeed);
            float rotSpeed = Random.Range(minRotationSpeed, maxRotationSpeed);
            float scale = Random.Range(minScale, maxScale);

            GameObject asteroidObj = Instantiate(prefab, spawnPos, Random.rotation);
            asteroidObj.transform.localScale = prefab.transform.localScale * scale;

            var networkObj = asteroidObj.GetComponent<NetworkObject>();
            if (networkObj == null)
            {
                Destroy(asteroidObj);
                return;
            }

            networkObj.Spawn();

            var behaviour = asteroidObj.GetComponent<AsteroidBehaviour>();
            if (behaviour != null)
            {
                behaviour.Initialize(moveDir, speed, rotSpeed);
                _activeAsteroids.Add(behaviour);
            }
        }

        // ─────────────────────────────────────────────
        //  CLEANUP
        // ─────────────────────────────────────────────
        private void DespawnAllAsteroids()
        {
            for (int i = _activeAsteroids.Count - 1; i >= 0; i--)
            {
                if (_activeAsteroids[i] != null && _activeAsteroids[i].IsSpawned)
                    _activeAsteroids[i].NetworkObject.Despawn(true);
            }
            _activeAsteroids.Clear();
        }

        private void ValidatePrefabReferences()
        {
            bool needsLoad = asteroidPrefabs == null || asteroidPrefabs.Length == 0;
            if (!needsLoad)
            {
                needsLoad = true;
                foreach (var p in asteroidPrefabs)
                    if (p != null) { needsLoad = false; break; }
            }
            if (!needsLoad) return;

#if UNITY_EDITOR
            if (asteroidPrefabPaths != null && asteroidPrefabPaths.Length > 0)
            {
                var loaded = new List<GameObject>();
                foreach (var path in asteroidPrefabPaths)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab != null) loaded.Add(prefab);
                }
                if (loaded.Count > 0)
                {
                    asteroidPrefabs = loaded.ToArray();
                    Debug.Log($"[FloatingWorldManager] Auto-loaded {loaded.Count} asteroid prefab(s).");
                }
            }
#endif
        }
    }
}
