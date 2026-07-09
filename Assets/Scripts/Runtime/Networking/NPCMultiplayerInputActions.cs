using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NPCSystem
{
    /// <summary>
    /// Clean event-driven wrapper around the InputSystem_Actions input action asset.
    /// Provides cached action references and clean C# events \u2014 no string lookups, no keyboard fallback.
    /// Designed for multiplayer: EnableForOwner/Disable based on NetworkBehaviour ownership.
    /// </summary>
    public sealed class NPCMultiplayerInputActions : MonoBehaviour
    {
        [Header("Input Asset")]
        public InputActionAsset inputActions;

        public string actionMapName = "Player";
        public string uiActionMapName = "UI";

        // Cached action references \u2014 set once in Awake, never string-looked-up again.
        InputAction _moveAction;
        InputAction _lookAction;
        InputAction _jumpAction;
        InputAction _sprintAction;
        InputAction _interactAction;
        InputAction _crouchAction;
        InputAction _previousAction;
        InputAction _nextAction;
        InputAction _attackAction;

        InputActionMap _playerMap;
        InputActionMap _uiMap;

        bool _enabled;

        // \u2500\u2500\u2500 Continuous state (polled each frame) \u2500\u2500\u2500
        public Vector2 MoveInput { get; private set; }
        public Vector2 LookInput { get; private set; }
        public bool SprintHeld { get; private set; }

        // \u2500\u2500\u2500 One-shot events \u2500\u2500\u2500
        public event Action OnJump;
        public event Action OnInteract;
        public event Action OnCrouch;
        public event Action OnPrevious;
        public event Action OnNext;
        public event Action OnAttack;

        void Awake()
        {
            ResolveActions();
        }

        void OnDestroy()
        {
            DisableAll();
        }

        void Update()
        {
            if (!_enabled)
            {
                MoveInput = Vector2.zero;
                LookInput = Vector2.zero;
                SprintHeld = false;
                return;
            }

            // Continuous polling \u2014 standard pattern for movement/look
            MoveInput = _moveAction?.ReadValue<Vector2>() ?? Vector2.zero;
            LookInput = _lookAction?.ReadValue<Vector2>() ?? Vector2.zero;
            SprintHeld = _sprintAction?.IsPressed() ?? false;

            // Clamp move input to unit circle
            if (MoveInput.sqrMagnitude > 1f)
                MoveInput = MoveInput.normalized;

            // One-shot checks (events fire once per press)
            if (_jumpAction != null && _jumpAction.WasPressedThisFrame())
                OnJump?.Invoke();
            if (_interactAction != null && _interactAction.WasPressedThisFrame())
                OnInteract?.Invoke();
            if (_crouchAction != null && _crouchAction.WasPressedThisFrame())
                OnCrouch?.Invoke();
            if (_previousAction != null && _previousAction.WasPressedThisFrame())
                OnPrevious?.Invoke();
            if (_nextAction != null && _nextAction.WasPressedThisFrame())
                OnNext?.Invoke();
            if (_attackAction != null && _attackAction.WasPressedThisFrame())
                OnAttack?.Invoke();
        }

        public void EnableActions()
        {
            if (_playerMap != null)
                _playerMap.Enable();
            _enabled = true;
        }

        public void DisableActions()
        {
            if (_playerMap != null)
                _playerMap.Disable();
            _enabled = false;
            MoveInput = Vector2.zero;
            LookInput = Vector2.zero;
            SprintHeld = false;
        }

        public void EnableUIActions()
        {
            if (_uiMap != null)
                _uiMap.Enable();
        }

        public void DisableUIActions()
        {
            if (_uiMap != null)
                _uiMap.Disable();
        }

        public void EnableAll()
        {
            if (inputActions != null)
                inputActions.Enable();
            EnableActions();
            EnableUIActions();
        }

        public void DisableAll()
        {
            _enabled = false;
            if (inputActions != null)
                inputActions.Disable();
        }

        void ResolveActions()
        {
            if (inputActions == null)
                return;

            _playerMap = inputActions.FindActionMap(actionMapName, false);
            _uiMap = inputActions.FindActionMap(uiActionMapName, false);

            if (_playerMap == null)
            {
                Debug.LogError(
                    $"[{nameof(NPCMultiplayerInputActions)}] Action map '{actionMapName}' not found in {inputActions.name}."
                );
                return;
            }

            _moveAction = _playerMap.FindAction("Move", false);
            _lookAction = _playerMap.FindAction("Look", false);
            _jumpAction = _playerMap.FindAction("Jump", false);
            _sprintAction = _playerMap.FindAction("Sprint", false);
            _interactAction = _playerMap.FindAction("Interact", false);
            _crouchAction = _playerMap.FindAction("Crouch", false);
            _previousAction = _playerMap.FindAction("Previous", false);
            _nextAction = _playerMap.FindAction("Next", false);
            _attackAction = _playerMap.FindAction("Attack", false);

            // Optional: log missing non-critical actions
            if (_moveAction == null)
                Debug.LogWarning(
                    $"[{nameof(NPCMultiplayerInputActions)}] 'Move' action not found."
                );
            if (_jumpAction == null)
                Debug.LogWarning(
                    $"[{nameof(NPCMultiplayerInputActions)}] 'Jump' action not found."
                );
        }

        public void SetActionMap(string mapName)
        {
            if (inputActions == null)
                return;
            var map = inputActions.FindActionMap(mapName, false);
            if (map != null)
                map.Enable();
        }
    }
}
