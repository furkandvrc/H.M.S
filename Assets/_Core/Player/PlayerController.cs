using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

namespace HMS.Player
{
    /// <summary>
    /// Server-authoritative player controller with client-side prediction.
    /// Client predicts movement locally, Server validates and corrects.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : NetworkBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float gravity = -9.81f;
        
        [Header("Mouse Look Settings")]
        [SerializeField] private float mouseSensitivity = 0.1f;
        [SerializeField] private float maxLookAngle = 85f;
        
        [Header("Network Settings")]
        [SerializeField] private float positionCorrectionThreshold = 2.0f;
        
        private CharacterController _characterController;
        private Transform _cameraHolder;
        private Camera _playerCamera;
        
        private Vector3 _velocity;
        private float _verticalRotation;
        private float _currentYRotation;
        private Vector3 _targetPosition;
        
        // Input System references
        private Keyboard _keyboard;
        private Mouse _mouse;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _keyboard = Keyboard.current;
            _mouse = Mouse.current;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            
            _currentYRotation = transform.eulerAngles.y;
            _targetPosition = transform.position;
            
            if (IsOwner)
            {
                SetupCamera();
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            
            // NetworkTransform handles sync for all players
            // We just need to handle input and server-side validation
        }
        
        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
        }

        private void SetupCamera()
        {
            // Find or create camera holder
            _cameraHolder = transform.Find("CameraHolder");
            if (_cameraHolder == null)
            {
                GameObject holder = new GameObject("CameraHolder");
                holder.transform.SetParent(transform);
                holder.transform.localPosition = new Vector3(0f, 0.6f, 0f);
                holder.transform.localRotation = Quaternion.identity;
                _cameraHolder = holder.transform;
            }
            
            // Create camera for owner only
            _playerCamera = _cameraHolder.GetComponentInChildren<Camera>();
            if (_playerCamera == null)
            {
                GameObject camObj = new GameObject("PlayerCamera");
                camObj.transform.SetParent(_cameraHolder);
                camObj.transform.localPosition = Vector3.zero;
                camObj.transform.localRotation = Quaternion.identity;
                _playerCamera = camObj.AddComponent<Camera>();
                camObj.AddComponent<AudioListener>();
            }
            
            _playerCamera.enabled = true;
        }

        private void Update()
        {
            if (!IsOwner) return;
            
            HandleInput();
        }

        private void HandleInput()
        {
            if (_keyboard == null || _mouse == null) return;
            
            // Gather input on client using new Input System
            float horizontal = 0f;
            float vertical = 0f;
            
            if (_keyboard.aKey.isPressed || _keyboard.leftArrowKey.isPressed) horizontal -= 1f;
            if (_keyboard.dKey.isPressed || _keyboard.rightArrowKey.isPressed) horizontal += 1f;
            if (_keyboard.wKey.isPressed || _keyboard.upArrowKey.isPressed) vertical += 1f;
            if (_keyboard.sKey.isPressed || _keyboard.downArrowKey.isPressed) vertical -= 1f;
            
            Vector2 mouseDelta = _mouse.delta.ReadValue();
            float mouseX = mouseDelta.x * mouseSensitivity;
            float mouseY = mouseDelta.y * mouseSensitivity;
            
            // === CLIENT-SIDE PREDICTION ===
            // Apply rotation locally for instant response
            _currentYRotation += mouseX;
            transform.rotation = Quaternion.Euler(0f, _currentYRotation, 0f);
            
            // Apply camera pitch locally
            _verticalRotation -= mouseY;
            _verticalRotation = Mathf.Clamp(_verticalRotation, -maxLookAngle, maxLookAngle);
            if (_cameraHolder != null)
            {
                _cameraHolder.localRotation = Quaternion.Euler(_verticalRotation, 0f, 0f);
            }
            
            // Apply movement locally for instant response
            Vector3 moveDirection = transform.right * horizontal + transform.forward * vertical;
            moveDirection = moveDirection.normalized * moveSpeed;
            
            // Apply gravity locally
            if (_characterController.isGrounded)
            {
                _velocity.y = -2f;
            }
            else
            {
                _velocity.y += gravity * Time.deltaTime;
            }
            
            Vector3 finalMove = moveDirection * Time.deltaTime + _velocity * Time.deltaTime;
            _characterController.Move(finalMove);
            
            // === SEND TO SERVER FOR VALIDATION ===
            SendInputServerRpc(horizontal, vertical, mouseX, mouseY, transform.position);
        }

        [ServerRpc]
        private void SendInputServerRpc(float horizontal, float vertical, float mouseX, float mouseY, Vector3 clientPosition)
        {
            // Server validates and processes movement
            ProcessMovementOnServer(horizontal, vertical, mouseX, mouseY, clientPosition);
        }

        private void ProcessMovementOnServer(float horizontal, float vertical, float mouseX, float mouseY, Vector3 clientPosition)
        {
            // Only server executes this
            if (!IsServer) return;
            
            // Apply rotation on server
            _currentYRotation += mouseX;
            transform.rotation = Quaternion.Euler(0f, _currentYRotation, 0f);
            
            // Calculate expected server position
            Vector3 moveDirection = transform.right * horizontal + transform.forward * vertical;
            moveDirection = moveDirection.normalized * moveSpeed;
            
            if (_characterController.isGrounded)
            {
                _velocity.y = -2f;
            }
            else
            {
                _velocity.y += gravity * Time.deltaTime;
            }
            
            Vector3 finalMove = moveDirection * Time.deltaTime + _velocity * Time.deltaTime;
            _characterController.Move(finalMove);
            
            // Validate client position - if too far, server position wins
            float clientServerDistance = Vector3.Distance(transform.position, clientPosition);
            if (clientServerDistance > positionCorrectionThreshold)
            {
                // Server position is authoritative - NetworkTransform will sync it
                Debug.Log($"[PlayerController] Position corrected. Distance: {clientServerDistance:F2}m");
            }
            
            // NetworkTransform automatically syncs transform to all clients
        }
    }
}