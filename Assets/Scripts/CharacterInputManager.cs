using System;
using UnityEngine;

public class CharacterInputManager : MonoBehaviour
{
    private InputSystem_Actions _actions;

    public InputSystem_Actions.PlayerActions Player => _actions.Player;
    
    private void Awake()
    {
        _actions = new InputSystem_Actions();
    }

    private void OnEnable()
    {
        _actions.Enable();
    }

    private void OnDisable()
    {
        _actions.Disable();
    }

    private void OnDestroy()
    {
        _actions.Disable();
        _actions.Dispose();
    }
}
