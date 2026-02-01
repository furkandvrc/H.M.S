using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using System.Collections.Generic;

namespace HMS.Player
{
    /// <summary>
    /// Server-Authoritative + Client-Side Prediction + Light Reconciliation
    /// With SmoothDamp, Input Buffer, Max Correction Distance
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
        [SerializeField] private float reconciliationThreshold = 0.1f;  // Start correcting above this
        [SerializeField] private float maxCorrectionDistance = 0.3f;   // Snap if above this
        [SerializeField] private float smoothTime = 0.1f;              // SmoothDamp time
        [SerializeField] private float otherPlayerSmoothTime = 0.15f;  // Other players smooth time
        [SerializeField] private int inputBufferSize = 5;              // Input history for reconciliation
        
        private CharacterController _characterController;
        private Transform _cameraHolder;
        private Camera _playerCamera;
        
        // Local state
        private Vector3 _velocity;
        private float _verticalRotation;
        private float _predictedYRotation;
        
        // Server state
        private Vector3 _serverVelocity;
        private float _serverYRotation;
        
        // SmoothDamp velocities
        private Vector3 _positionSmoothVelocity;
        private Vector3 _otherPlayerSmoothVelocity;
        private float _rotationSmoothVelocity;
        
        // Input buffer for reconciliation
        private Queue<InputSnapshot> _inputBuffer = new Queue<InputSnapshot>();
        private uint _inputSequence;
        
        // Input System
        private Keyboard _keyboard;
        private Mouse _mouse;
        
        // Network state
        private NetworkVariable<PlayerNetworkState> _networkState = new NetworkVariable<PlayerNetworkState>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private struct InputSnapshot
        {
            public uint Sequence;
            public float Horizontal;
            public float Vertical;
            public float MouseX;
            public float DeltaTime;
            public Vector3 PredictedPosition;
        }

        private struct PlayerNetworkState : INetworkSerializable
        {
            public Vector3 Position;
            public float YRotation;
            public float VelocityY;
            public uint LastProcessedInput;
            
            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref Position);
                serializer.SerializeValue(ref YRotation);
                serializer.SerializeValue(ref VelocityY);
                serializer.SerializeValue(ref LastProcessedInput);
            }
        }

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _keyboard = Keyboard.current;
            _mouse = Mouse.current;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            
            _predictedYRotation = transform.eulerAngles.y;
            _serverYRotation = transform.eulerAngles.y;
            
            if (IsOwner)
            {
                SetupCamera();
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            
            _networkState.OnValueChanged += OnNetworkStateChanged;
        }
        
        public override void OnNetworkDespawn()
        {
            _networkState.OnValueChanged -= OnNetworkStateChanged;
            base.OnNetworkDespawn();
        }
        
        private void OnNetworkStateChanged(PlayerNetworkState oldState, PlayerNetworkState newState)
        {
            if (!IsOwner) return;
            
            // Sync gravity velocity
            _velocity.y = newState.VelocityY;
            
            // Remove processed inputs from buffer
            while (_inputBuffer.Count > 0 && _inputBuffer.Peek().Sequence <= newState.LastProcessedInput)
            {
                _inputBuffer.Dequeue();
            }
        }

        private void SetupCamera()
        {
            _cameraHolder = transform.Find("CameraHolder");
            if (_cameraHolder == null)
            {
                GameObject holder = new GameObject("CameraHolder");
                holder.transform.SetParent(transform);
                holder.transform.localPosition = new Vector3(0f, 0.6f, 0f);
                holder.transform.localRotation = Quaternion.identity;
                _cameraHolder = holder.transform;
            }
            
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
            if (!IsSpawned) return;
            
            if (IsOwner)
            {
                HandleOwnerUpdate();
            }
            else
            {
                HandleOtherPlayerUpdate();
            }
        }
        
        private void HandleOwnerUpdate()
        {
            if (_keyboard == null) _keyboard = Keyboard.current;
            if (_mouse == null) _mouse = Mouse.current;
            if (_keyboard == null || _mouse == null) return;
            
            // === GATHER INPUT ===
            float horizontal = 0f;
            float vertical = 0f;
            
            if (_keyboard.aKey.isPressed || _keyboard.leftArrowKey.isPressed) horizontal -= 1f;
            if (_keyboard.dKey.isPressed || _keyboard.rightArrowKey.isPressed) horizontal += 1f;
            if (_keyboard.wKey.isPressed || _keyboard.upArrowKey.isPressed) vertical += 1f;
            if (_keyboard.sKey.isPressed || _keyboard.downArrowKey.isPressed) vertical -= 1f;
            
            Vector2 mouseDelta = _mouse.delta.ReadValue();
            float mouseX = mouseDelta.x * mouseSensitivity;
            float mouseY = mouseDelta.y * mouseSensitivity;
            
            // === CLIENT-SIDE PREDICTION (Instant) ===
            
            // Rotation
            _predictedYRotation += mouseX;
            transform.rotation = Quaternion.Euler(0f, _predictedYRotation, 0f);
            
            // Camera pitch (purely local)
            _verticalRotation -= mouseY;
            _verticalRotation = Mathf.Clamp(_verticalRotation, -maxLookAngle, maxLookAngle);
            if (_cameraHolder != null)
            {
                _cameraHolder.localRotation = Quaternion.Euler(_verticalRotation, 0f, 0f);
            }
            
            // Movement
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
            
            Vector3 finalMove = (moveDirection + _velocity) * Time.deltaTime;
            _characterController.Move(finalMove);
            
            // === RECONCILIATION (SmoothDamp) ===
            if (_networkState.Value.Position != Vector3.zero)
            {
                float error = Vector3.Distance(transform.position, _networkState.Value.Position);
                
                if (error > maxCorrectionDistance)
                {
                    // Large error = SNAP (possible teleport/respawn)
                    transform.position = _networkState.Value.Position;
                    _positionSmoothVelocity = Vector3.zero;
                }
                else if (error > reconciliationThreshold)
                {
                    // Small error = SmoothDamp correction
                    Vector3 targetPos = Vector3.SmoothDamp(
                        transform.position, 
                        _networkState.Value.Position, 
                        ref _positionSmoothVelocity, 
                        smoothTime
                    );
                    
                    Vector3 correction = targetPos - transform.position;
                    _characterController.Move(correction);
                }
            }
            
            // === STORE INPUT IN BUFFER ===
            _inputSequence++;
            var snapshot = new InputSnapshot
            {
                Sequence = _inputSequence,
                Horizontal = horizontal,
                Vertical = vertical,
                MouseX = mouseX,
                DeltaTime = Time.deltaTime,
                PredictedPosition = transform.position
            };
            
            _inputBuffer.Enqueue(snapshot);
            
            // Keep buffer size limited
            while (_inputBuffer.Count > inputBufferSize)
            {
                _inputBuffer.Dequeue();
            }
            
            // === SEND TO SERVER ===
            SendInputServerRpc(_inputSequence, horizontal, vertical, mouseX, Time.deltaTime);
        }
        
        private void HandleOtherPlayerUpdate()
        {
            var state = _networkState.Value;
            if (state.Position == Vector3.zero) return;
            
            // SmoothDamp for other players (smoother than Lerp)
            transform.position = Vector3.SmoothDamp(
                transform.position, 
                state.Position, 
                ref _otherPlayerSmoothVelocity, 
                otherPlayerSmoothTime
            );
            
            float currentY = transform.eulerAngles.y;
            float targetY = state.YRotation;
            float smoothedY = Mathf.SmoothDampAngle(currentY, targetY, ref _rotationSmoothVelocity, otherPlayerSmoothTime);
            transform.rotation = Quaternion.Euler(0f, smoothedY, 0f);
        }

        [ServerRpc]
        private void SendInputServerRpc(uint sequence, float horizontal, float vertical, float mouseX, float deltaTime)
        {
            ProcessInputOnServer(sequence, horizontal, vertical, mouseX, deltaTime);
        }

        private void ProcessInputOnServer(uint sequence, float horizontal, float vertical, float mouseX, float deltaTime)
        {
            if (!IsServer) return;
            
            // === SERVER AUTHORITATIVE CALCULATION ===
            
            // Rotation
            _serverYRotation += mouseX;
            transform.rotation = Quaternion.Euler(0f, _serverYRotation, 0f);
            
            // Movement
            Vector3 moveDirection = transform.right * horizontal + transform.forward * vertical;
            moveDirection = moveDirection.normalized * moveSpeed;
            
            if (_characterController.isGrounded)
            {
                _serverVelocity.y = -2f;
            }
            else
            {
                _serverVelocity.y += gravity * deltaTime;
            }
            
            Vector3 finalMove = (moveDirection + _serverVelocity) * deltaTime;
            _characterController.Move(finalMove);
            
            // === SYNC STATE ===
            _networkState.Value = new PlayerNetworkState
            {
                Position = transform.position,
                YRotation = _serverYRotation,
                VelocityY = _serverVelocity.y,
                LastProcessedInput = sequence
            };
        }
    }
}
