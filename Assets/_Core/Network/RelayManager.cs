using System;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

namespace HMS.Network
{
    /// <summary>
    /// Manages Unity Relay connections for online multiplayer.
    /// Handles UGS initialization, authentication, and relay allocation.
    /// </summary>
    public class RelayManager : MonoBehaviour
    {
        public static RelayManager Instance { get; private set; }
        
        [Header("Settings")]
        [SerializeField] private int maxConnections = 4;
        
        public event Action<string> OnJoinCodeGenerated;
        public event Action<string> OnConnectionStatusChanged;
        public event Action<string> OnError;
        
        public string CurrentJoinCode { get; private set; }
        public bool IsInitialized { get; private set; }
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private async void Start()
        {
            await InitializeServices();
        }

        /// <summary>
        /// Initialize Unity Gaming Services and authenticate.
        /// </summary>
        public async Task InitializeServices()
        {
            try
            {
                OnConnectionStatusChanged?.Invoke("Initializing services...");
                
                if (UnityServices.State != ServicesInitializationState.Initialized)
                {
                    await UnityServices.InitializeAsync();
                }
                
                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }
                
                IsInitialized = true;
                OnConnectionStatusChanged?.Invoke("Services ready");
                Debug.Log($"[RelayManager] Initialized. Player ID: {AuthenticationService.Instance.PlayerId}");
            }
            catch (Exception e)
            {
                OnError?.Invoke($"Failed to initialize: {e.Message}");
                Debug.LogError($"[RelayManager] Init failed: {e}");
            }
        }

        /// <summary>
        /// Create a Relay allocation and start as Host.
        /// </summary>
        public async Task<string> StartHostWithRelay()
        {
            if (!IsInitialized)
            {
                await InitializeServices();
            }
            
            try
            {
                OnConnectionStatusChanged?.Invoke("Creating relay...");
                
                // Create relay allocation
                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
                
                // Get join code
                CurrentJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                
                // Configure transport
                var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                transport.SetRelayServerData(
                    allocation.RelayServer.IpV4,
                    (ushort)allocation.RelayServer.Port,
                    allocation.AllocationIdBytes,
                    allocation.Key,
                    allocation.ConnectionData
                );
                
                // Start host
                NetworkManager.Singleton.StartHost();
                
                OnJoinCodeGenerated?.Invoke(CurrentJoinCode);
                OnConnectionStatusChanged?.Invoke($"Hosting - Code: {CurrentJoinCode}");
                Debug.Log($"[RelayManager] Host started. Join Code: {CurrentJoinCode}");
                
                return CurrentJoinCode;
            }
            catch (Exception e)
            {
                OnError?.Invoke($"Failed to start host: {e.Message}");
                Debug.LogError($"[RelayManager] StartHost failed: {e}");
                return null;
            }
        }

        /// <summary>
        /// Join an existing Relay session using a join code.
        /// </summary>
        public async Task<bool> JoinWithRelay(string joinCode)
        {
            if (string.IsNullOrEmpty(joinCode))
            {
                OnError?.Invoke("Join code cannot be empty");
                return false;
            }
            
            if (!IsInitialized)
            {
                await InitializeServices();
            }
            
            try
            {
                OnConnectionStatusChanged?.Invoke("Joining relay...");
                
                // Join relay allocation
                JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode.ToUpper());
                
                // Configure transport
                var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                transport.SetRelayServerData(
                    joinAllocation.RelayServer.IpV4,
                    (ushort)joinAllocation.RelayServer.Port,
                    joinAllocation.AllocationIdBytes,
                    joinAllocation.Key,
                    joinAllocation.ConnectionData,
                    joinAllocation.HostConnectionData
                );
                
                // Start client
                NetworkManager.Singleton.StartClient();
                
                CurrentJoinCode = joinCode;
                OnConnectionStatusChanged?.Invoke("Connected!");
                Debug.Log($"[RelayManager] Joined relay with code: {joinCode}");
                
                return true;
            }
            catch (Exception e)
            {
                OnError?.Invoke($"Failed to join: {e.Message}");
                Debug.LogError($"[RelayManager] Join failed: {e}");
                return false;
            }
        }

        /// <summary>
        /// Disconnect from current session.
        /// </summary>
        public void Disconnect()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.Shutdown();
            }
            CurrentJoinCode = null;
            OnConnectionStatusChanged?.Invoke("Disconnected");
            Debug.Log("[RelayManager] Disconnected");
        }
    }
}