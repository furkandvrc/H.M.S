using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace HMS.Network
{
    /// <summary>
    /// Simple UI for Relay connection management.
    /// Host creates a game and gets a code, Client joins with that code.
    /// </summary>
    public class ConnectionUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject connectionPanel;
        [SerializeField] private Button hostButton;
        [SerializeField] private Button joinButton;
        [SerializeField] private Button disconnectButton;
        [SerializeField] private TMP_InputField joinCodeInput;
        [SerializeField] private TextMeshProUGUI joinCodeDisplay;
        [SerializeField] private TextMeshProUGUI statusText;
        
        [Header("Settings")]
        [SerializeField] private bool createUIOnStart = true;
        
        private Canvas _canvas;
        private RelayManager _relayManager;

        private void Start()
        {
            if (createUIOnStart && connectionPanel == null)
            {
                CreateUI();
            }
            else
            {
                SetupExistingUI();
            }
            
            _relayManager = RelayManager.Instance;
            if (_relayManager != null)
            {
                _relayManager.OnJoinCodeGenerated += OnJoinCodeGenerated;
                _relayManager.OnConnectionStatusChanged += OnStatusChanged;
                _relayManager.OnError += OnError;
            }
        }

        private void OnDestroy()
        {
            if (_relayManager != null)
            {
                _relayManager.OnJoinCodeGenerated -= OnJoinCodeGenerated;
                _relayManager.OnConnectionStatusChanged -= OnStatusChanged;
                _relayManager.OnError -= OnError;
            }
        }

        private void SetupExistingUI()
        {
            if (hostButton != null) hostButton.onClick.AddListener(OnHostClicked);
            if (joinButton != null) joinButton.onClick.AddListener(OnJoinClicked);
            if (disconnectButton != null) disconnectButton.onClick.AddListener(OnDisconnectClicked);
        }

        private void CreateUI()
        {
            // Create Canvas
            GameObject canvasObj = new GameObject("ConnectionCanvas");
            canvasObj.transform.SetParent(transform);
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
            
            // Create Panel
            connectionPanel = CreatePanel(canvasObj.transform);
            
            // Create UI Elements
            float yPos = 100f;
            
            // Status Text
            statusText = CreateText(connectionPanel.transform, "Status: Initializing...", new Vector2(0, yPos));
            yPos -= 40f;
            
            // Join Code Display
            joinCodeDisplay = CreateText(connectionPanel.transform, "Join Code: ---", new Vector2(0, yPos));
            joinCodeDisplay.fontSize = 28;
            joinCodeDisplay.color = Color.yellow;
            yPos -= 50f;
            
            // Host Button
            hostButton = CreateButton(connectionPanel.transform, "HOST GAME", new Vector2(0, yPos), OnHostClicked);
            yPos -= 50f;
            
            // Join Code Input
            joinCodeInput = CreateInputField(connectionPanel.transform, "Enter Join Code...", new Vector2(0, yPos));
            yPos -= 50f;
            
            // Join Button
            joinButton = CreateButton(connectionPanel.transform, "JOIN GAME", new Vector2(0, yPos), OnJoinClicked);
            yPos -= 50f;
            
            // Disconnect Button
            disconnectButton = CreateButton(connectionPanel.transform, "DISCONNECT", new Vector2(0, yPos), OnDisconnectClicked);
            disconnectButton.GetComponent<Image>().color = new Color(0.8f, 0.3f, 0.3f);
        }

        private GameObject CreatePanel(Transform parent)
        {
            GameObject panel = new GameObject("ConnectionPanel");
            panel.transform.SetParent(parent, false);
            
            RectTransform rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0.5f);
            rect.anchorMax = new Vector2(0, 0.5f);
            rect.pivot = new Vector2(0, 0.5f);
            rect.anchoredPosition = new Vector2(20, 0);
            rect.sizeDelta = new Vector2(320, 400);
            
            Image bg = panel.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.85f);
            
            return panel;
        }

        private TextMeshProUGUI CreateText(Transform parent, string text, Vector2 position)
        {
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(parent, false);
            
            RectTransform rect = textObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(280, 30);
            
            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 18;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            
            return tmp;
        }

        private Button CreateButton(Transform parent, string text, Vector2 position, UnityEngine.Events.UnityAction action)
        {
            GameObject btnObj = new GameObject("Button");
            btnObj.transform.SetParent(parent, false);
            
            RectTransform rect = btnObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(250, 40);
            
            Image img = btnObj.AddComponent<Image>();
            img.color = new Color(0.2f, 0.5f, 0.8f);
            
            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(action);
            
            // Button Text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            
            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 20;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            
            return btn;
        }

        private TMP_InputField CreateInputField(Transform parent, string placeholder, Vector2 position)
        {
            GameObject inputObj = new GameObject("InputField");
            inputObj.transform.SetParent(parent, false);
            
            RectTransform rect = inputObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(250, 40);
            
            Image img = inputObj.AddComponent<Image>();
            img.color = new Color(0.15f, 0.15f, 0.15f);
            
            // Text Area
            GameObject textArea = new GameObject("TextArea");
            textArea.transform.SetParent(inputObj.transform, false);
            RectTransform textAreaRect = textArea.AddComponent<RectTransform>();
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.offsetMin = new Vector2(10, 5);
            textAreaRect.offsetMax = new Vector2(-10, -5);
            textArea.AddComponent<RectMask2D>();
            
            // Placeholder
            GameObject placeholderObj = new GameObject("Placeholder");
            placeholderObj.transform.SetParent(textArea.transform, false);
            RectTransform phRect = placeholderObj.AddComponent<RectTransform>();
            phRect.anchorMin = Vector2.zero;
            phRect.anchorMax = Vector2.one;
            phRect.sizeDelta = Vector2.zero;
            TextMeshProUGUI phText = placeholderObj.AddComponent<TextMeshProUGUI>();
            phText.text = placeholder;
            phText.fontSize = 18;
            phText.fontStyle = FontStyles.Italic;
            phText.alignment = TextAlignmentOptions.MidlineLeft;
            phText.color = new Color(0.5f, 0.5f, 0.5f);
            
            // Input Text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(textArea.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            TextMeshProUGUI inputText = textObj.AddComponent<TextMeshProUGUI>();
            inputText.fontSize = 18;
            inputText.alignment = TextAlignmentOptions.MidlineLeft;
            inputText.color = Color.white;
            
            TMP_InputField input = inputObj.AddComponent<TMP_InputField>();
            input.textViewport = textAreaRect;
            input.textComponent = inputText;
            input.placeholder = phText;
            input.characterLimit = 6;
            input.contentType = TMP_InputField.ContentType.Alphanumeric;
            
            return input;
        }

        private async void OnHostClicked()
        {
            if (_relayManager == null)
            {
                _relayManager = RelayManager.Instance;
            }
            
            if (_relayManager != null)
            {
                hostButton.interactable = false;
                joinButton.interactable = false;
                await _relayManager.StartHostWithRelay();
            }
        }

        private async void OnJoinClicked()
        {
            if (_relayManager == null)
            {
                _relayManager = RelayManager.Instance;
            }
            
            if (_relayManager != null && joinCodeInput != null)
            {
                string code = joinCodeInput.text.Trim().ToUpper();
                if (string.IsNullOrEmpty(code))
                {
                    OnError("Please enter a join code");
                    return;
                }
                
                hostButton.interactable = false;
                joinButton.interactable = false;
                await _relayManager.JoinWithRelay(code);
            }
        }

        private void OnDisconnectClicked()
        {
            if (_relayManager != null)
            {
                _relayManager.Disconnect();
            }
            
            hostButton.interactable = true;
            joinButton.interactable = true;
            joinCodeDisplay.text = "Join Code: ---";
        }

        private void OnJoinCodeGenerated(string code)
        {
            if (joinCodeDisplay != null)
            {
                joinCodeDisplay.text = $"Join Code: {code}";
            }
        }

        private void OnStatusChanged(string status)
        {
            if (statusText != null)
            {
                statusText.text = $"Status: {status}";
            }
        }

        private void OnError(string error)
        {
            if (statusText != null)
            {
                statusText.text = $"<color=red>Error: {error}</color>";
            }
            
            hostButton.interactable = true;
            joinButton.interactable = true;
        }

        private void Update()
        {
            // Toggle UI visibility with Escape key
            if (UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (connectionPanel != null)
                {
                    connectionPanel.SetActive(!connectionPanel.activeSelf);
                    
                    // Toggle cursor lock
                    if (connectionPanel.activeSelf)
                    {
                        Cursor.lockState = CursorLockMode.None;
                        Cursor.visible = true;
                    }
                    else
                    {
                        Cursor.lockState = CursorLockMode.Locked;
                        Cursor.visible = false;
                    }
                }
            }
        }
    }
}