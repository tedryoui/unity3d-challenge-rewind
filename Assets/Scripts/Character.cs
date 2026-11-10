using System;
using DefaultNamespace;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterInputManager), typeof(CharacterController))]
public class Character : MonoBehaviour
{
    [SerializeField] private bool                   _isDebug;
    [SerializeField] private CharacterConfiguration _config;
    [SerializeField] private Transform              _modelTransform;

    private CharacterInputManager _inputManager;
    private CharacterController   _characterController;
    private Animator              _animator;

    private float2  _moveInputDelta  = new float2(0.0f, 1.0f);
    private bool    _moveInputActive = false;
    private Vector3 _moveRatio       = Vector3.zero;
    private bool    _runInputActive  = false;

    private int Animator_Boolean_IsMove           => Animator.StringToHash("isWalk");
    private int Animator_Float_WalkAnimationSpeed => Animator.StringToHash("walkAnimationMultiplier");
    private int Animator_Boolean_IsRun            => Animator.StringToHash("isRun");
    private int Animator_Float_RunAnimationSpeed  => Animator.StringToHash("runAnimationMultiplier");

    private void Start()
    {
        _inputManager        = GetComponent<CharacterInputManager>();
        _characterController = GetComponent<CharacterController>();
        _animator            = GetComponentInChildren<Animator>();

        _inputManager.Player.Move.performed += OnMoveInput;
        _inputManager.Player.Move.canceled  += OnMoveInput;

        _inputManager.Player.Sprint.started  += OnRunInput;
        _inputManager.Player.Sprint.canceled += OnRunInput;
    }

    private void OnDestroy()
    {
        _inputManager.Player.Move.performed -= OnMoveInput;
        _inputManager.Player.Move.canceled  -= OnMoveInput;

        _inputManager.Player.Sprint.started  -= OnRunInput;
        _inputManager.Player.Sprint.canceled -= OnRunInput;
    }

    private void OnRunInput(InputAction.CallbackContext callbackContext)
    {
        _runInputActive = callbackContext.phase is InputActionPhase.Started;
    }

    private void OnMoveInput(InputAction.CallbackContext callbackContext)
    {
        var delta    = callbackContext.ReadValue<Vector2>();
        var deltaAbs = math.abs(delta);

        if (deltaAbs is { x: > 0 } or { y: > 0 })
        {
            _moveInputActive = true;
            _moveInputDelta  = delta;
        }
        else
        {
            _moveInputActive = false;
        }
    }

    private void Update()
    {
        TryMoveCharacter();
        TryRotateCharacter();
        TryUpdateCharacterAnimator();
    }

    private void TryUpdateCharacterAnimator()
    {
        _animator.SetBool(Animator_Boolean_IsMove, _moveInputActive);
        _animator.SetFloat(Animator_Float_WalkAnimationSpeed, math.abs(_moveRatio.magnitude) * _config.Walk_Animation_Speed);
        
        _animator.SetBool(Animator_Boolean_IsRun, _runInputActive);
        _animator.SetFloat(Animator_Float_RunAnimationSpeed, math.abs(_moveRatio.magnitude) * _config.Run_Animation_Speed);
    }

    private void TryRotateCharacter()
    {
        var threeDimensionalMoveDirection = new Vector3(_moveInputDelta.x, 0, _moveInputDelta.y);

        var rotation     = Quaternion.LookRotation(threeDimensionalMoveDirection);
        var nextRotation = Quaternion.Slerp(_modelTransform.rotation, rotation, Time.deltaTime * _config.Angular_Speed);
        
        _modelTransform.rotation = nextRotation;
    }

    private void TryMoveCharacter()
    {
        if (!_moveInputActive)
        {
            _moveRatio = Vector2.zero;
            return;
        }
        
        var threeDimensionalMoveDirection = new Vector3(_moveInputDelta.x, 0, _moveInputDelta.y);
        var speed                         = _runInputActive ? _config.Run_Speed : _config.Move_Speed;

        _moveRatio           = threeDimensionalMoveDirection * speed;

        _characterController.SimpleMove(_moveRatio);
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody body = hit.collider.attachedRigidbody;

        if (body == null || body.isKinematic)
            return;

        Vector3 pushDirection = new Vector3(hit.moveDirection.x, 0.1f, hit.moveDirection.z);

        body.linearVelocity = pushDirection * _config.Push_Force;
    }

    private void OnGUI()
    {
        if (!_isDebug) return;

        var yPosition  = 10;
        var lineHeight = 60;

        {
            //
            // Draw player delta
            //

            var rect  = new Rect(10, yPosition, 1080, lineHeight);
            var style = new GUIStyle(GUI.skin.label);

            style.fontSize = 32;

            GUI.Label(rect, $"Move Input Delta: {_moveInputDelta}", style);

            yPosition += lineHeight;
        }

        {
            //
            // Draw player move ratio
            //
            
            var rect  = new Rect(10, yPosition, 1080, lineHeight);
            var style = new GUIStyle(GUI.skin.label);
            
            style.fontSize = 32;
            
            GUI.Label(rect, $"Move Ratio: {_moveRatio}", style);
            
            yPosition += lineHeight;
        }

        {
            //
            // Draw player move speed
            //
            
            var rect  = new Rect(10, yPosition, 1080, lineHeight);
            var style = new GUIStyle(GUI.skin.label);
            
            style.fontSize = 32;
            
            GUI.Label(rect, $"Move Speed: {math.abs(_moveRatio.magnitude):00}", style);
            
            yPosition += lineHeight;
        }
    }
}
