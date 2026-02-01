using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using TMPro;

namespace HMS.Network
{
    /// <summary>
    /// Displays network statistics (FPS, Ping, etc.) in the top-left corner.
    /// </summary>
    public class NetworkStatsUI : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float updateInterval = 0.5f;
        [SerializeField] private bool showOnStart = true;
        
        private TextMeshProUGUI _statsText;
        private GameObject _statsPanel;
        private float _deltaTime;
        private float _updateTimer;
        
        // Stats
        private float _fps;
        private float _ping;
        private int _connectedClients;
        private bool _isConnected;
        private bool _isHost;
        
        private void Start()
        {
            CreateUI();
            _statsPanel.SetActive(showOnStart);
        }

        private void CreateUI()
        {
            // Create Canvas
            GameObject canvasObj = new GameObject("StatsCanvas");
            canvasObj.transform.SetParent(transform);
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;
            canvasObj.AddComponent<CanvasScaler>();
            
            // Create Panel
            _statsPanel = new GameObject("StatsPanel");
            _statsPanel.transform.SetParent(canvasObj.transform, false);
            
            RectTransform panelRect = _statsPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0, 1);
            panelRect.anchorMax = new Vector2(0, 1);
            panelRect.pivot = new Vector2(0, 1);
            panelRect.anchoredPosition = new Vector2(10, -10);
            panelRect.sizeDelta = new Vector2(180, 110);
            
            Image bg = _statsPanel.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.75f);
            
            // Create Text
            GameObject textObj = new GameObject("StatsText");
            textObj.transform.SetParent(_statsPanel.transform, false);
            
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8, 5);
            textRect.offsetMax = new Vector2(-8, -5);
            
            _statsText = textObj.AddComponent<TextMeshProUGUI>();
            _statsText.fontSize = 14;
            _statsText.alignment = TextAlignmentOptions.TopLeft;
            _statsText.color = Color.white;
            _statsText.text = "Initializing...";
        }

        private void Update()
        {
            // Toggle visibility with F3
            if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame)
            {
                _statsPanel.SetActive(!_statsPanel.activeSelf);
            }
            
            if (!_statsPanel.activeSelf) return;
            
            // Calculate FPS
            _deltaTime += (Time.unscaledDeltaTime - _deltaTime) * 0.1f;
            
            // Update stats periodically
            _updateTimer += Time.unscaledDeltaTime;
            if (_updateTimer >= updateInterval)
            {
                _updateTimer = 0f;
                UpdateStats();
            }
        }

        private void UpdateStats()
        {
            // FPS
            _fps = 1.0f / _deltaTime;
            
            // Network stats
            if (NetworkManager.Singleton != null)
            {
                _isConnected = NetworkManager.Singleton.IsConnectedClient || NetworkManager.Singleton.IsHost;
                _isHost = NetworkManager.Singleton.IsHost;
                _connectedClients = NetworkManager.Singleton.ConnectedClientsIds.Count;
                
                // Get ping from transport
                if (_isConnected && !_isHost)
                {
                    _ping = NetworkManager.Singleton.NetworkConfig.NetworkTransport.GetCurrentRtt(NetworkManager.ServerClientId);
                }
                else if (_isHost)
                {
                    _ping = 0f;
                }
            }
            else
            {
                _isConnected = false;
                _isHost = false;
                _connectedClients = 0;
                _ping = 0f;
            }
            
            // Update UI
            string fpsColor = _fps >= 60 ? "#00FF00" : (_fps >= 30 ? "#FFFF00" : "#FF0000");
            string pingColor = _ping <= 50 ? "#00FF00" : (_ping <= 100 ? "#FFFF00" : "#FF0000");
            string status = _isConnected 
                ? (_isHost ? "<color=#00FF00>HOST</color>" : "<color=#00FFFF>CLIENT</color>") 
                : "<color=#888888>OFFLINE</color>";
            
            _statsText.text = $"<b>FPS:</b> <color={fpsColor}>{_fps:F0}</color>\n" +
                              $"<b>Ping:</b> <color={pingColor}>{_ping:F0} ms</color>\n" +
                              $"<b>Status:</b> {status}\n" +
                              $"<b>Players:</b> {_connectedClients}\n" +
                              $"<size=10><color=#888888>[F3] Toggle</color></size>";
        }
    }
}
