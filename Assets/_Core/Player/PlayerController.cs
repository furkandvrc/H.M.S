using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

namespace HMS.Player
{
    /// <summary>
    /// Server-authoritative player controller.
    /// Client sends input via ServerRpc, Server calculates movement.
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
        
        private CharacterController _characterController;
        private Transform _cameraHolder;
        private Camera _playerCamera;
        
        private Vector3 _velocity;
        private float _verticalRotation;
        
        // Input System references
        private Keyboard _keyboard;
        private Mouse _mouse;
        
        // Network synced rotation for smooth interpolation
        private NetworkVariable<float> _networkYRotation = new NetworkVariable<float>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        
        private NetworkVariable<float> _networkCameraPitch = new NetworkVariable<float>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _keyboard = Keyboard.current;
            _mouse = Mouse.current;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            
            if (IsOwner)
            {
                SetupCamera();
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
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
            
            // Send input to server
            SendInputServerRpc(horizontal, vertical, mouseX, mouseY);
            
            // Client-side camera prediction for smooth feel
            if (_cameraHolder != null)
            {
                _verticalRotation -= mouseY;
                _verticalRotation = Mathf.Clamp(_verticalRotation, -maxLookAngle, maxLookAngle);
                _cameraHolder.localRotation = Quaternion.Euler(_verticalRotation, 0f, 0f);
            }
        }

        [ServerRpc]
        private void SendInputServerRpc(float horizontal, float vertical, float mouseX, float mouseY)
        {
            // Server calculates movement
            ProcessMovement(horizontal, vertical, mouseX, mouseY);
        }

        private void ProcessMovement(float horizontal, float vertical, float mouseX, float mouseY)
        {
            // Only server executes this
            if (!IsServer) return;
            
            // Horizontal rotation (Y axis)
            float newYRotation = transform.eulerAngles.y + mouseX;
            transform.rotation = Quaternion.Euler(0f, newYRotation, 0f);
            _networkYRotation.Value = newYRotation;
            
            // Camera pitch
            float currentPitch = _networkCameraPitch.Value;
            currentPitch -= mouseY;
            currentPitch = Mathf.Clamp(currentPitch, -maxLookAngle, maxLookAngle);
            _networkCameraPitch.Value = currentPitch;
            
            // Movement direction based on player rotation
            Vector3 moveDirection = transform.right * horizontal + transform.forward * vertical;
            moveDirection = moveDirection.normalized * moveSpeed;
            
            // Apply gravity
            if (_characterController.isGrounded)
            {
                _velocity.y = -2f;
            }
            else
            {
                _velocity.y += gravity * Time.deltaTime;
            }
            
            // Final movement
            Vector3 finalMove = moveDirection * Time.deltaTime + _velocity * Time.deltaTime;
            _characterController.Move(finalMove);
        }
    }
}