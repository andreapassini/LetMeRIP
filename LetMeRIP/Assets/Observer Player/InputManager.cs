using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
public class InputManager : MonoBehaviour
{
    private static InputManager _instance;
    public static  InputManager Instance
    {
        get => _instance;
    }

    private PlayerInputActions playerActions;

    private void Awake()
    {
        if (_instance != null && _instance != this) Destroy(this.gameObject);
        else _instance = this;

        playerActions = new PlayerInputActions();
    }

    private void OnEnable()
    {
        playerActions.Enable();
    }

    private void OnDisable()
    {
        playerActions.Disable();
    }

    public Vector3 GetMovement()
    {
        return playerActions.Player.Movement.ReadValue<Vector3>();
    }
    public Vector2 GetMouseDelta()
    {
        return playerActions.Player.Look.ReadValue<Vector2>();
    }
}
