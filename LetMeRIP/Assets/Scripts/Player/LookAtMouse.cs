using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class LookAtMouse : MonoBehaviourPun
{
    public LayerMask groundMask;
    [SerializeField] private float rotationSmoothing = 1000f;
    
    private PlayerInputActions playerInputActions;
    private PlayerInput playerInput;
    private Camera playerCamera;
    private Vector3 directionToLook;
    private bool isEnabled = true;
    private string currentControlScheme;

    public bool isGamepad = true;

    private void Start()
    {
        playerInputActions = new PlayerInputActions();
        playerInputActions.Player.Enable();

        playerCamera = Camera.main;
        playerInput = transform.GetComponent<PlayerInput>();

        currentControlScheme = playerInput.currentControlScheme;
    }
    
    private void Update()
    {
        if (!photonView.IsMine) return;

        directionToLook = GatherDirectionInput();
    }

	private void FixedUpdate()
	{
        if (!photonView.IsMine || !isEnabled) return;
        Rotate();
    }
    
    public Vector3 GatherDirectionInput()
    {
        Vector2 direction = playerInputActions.Player.LookAt.ReadValue<Vector2>();

        return direction;
    }


    private void Rotate()
    {
        if (this.directionToLook == Vector3.zero) {
            Debug.Log("Vector3 Zero");
            return; 
        }

        if (!isGamepad)
        {
            // KBM
            Ray ray = playerCamera.ScreenPointToRay(playerInputActions.Player.LookAt.ReadValue<Vector2>());

            Vector3 direction = Physics.Raycast(ray, out var hitInfo, Mathf.Infinity, groundMask)
                ? hitInfo.point - transform.position
                : Vector3.zero;
            direction.y = 0;

            directionToLook = direction.normalized;

            this.directionToLook.y = 0;
            transform.forward = this.directionToLook;
        }
        else
        {
            // Gamepad
            Vector3 playerDiredction =
                Vector3.right * directionToLook.x +
                Vector3.forward * directionToLook.y;

            
            Quaternion newRot = Quaternion.LookRotation(playerDiredction.ToIso(), Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, newRot, rotationSmoothing * Time.deltaTime);
        }
    }

    public void EnableLookAround()
    {
        isEnabled = true;
    }

    public void DisableLookAround()
    {
        Debug.LogError("Disable Look Around");
        isEnabled = false;
    }

    public void OnDeviceChange(PlayerInput pi)
    {
        if (pi.currentControlScheme.Equals("Gamepad"))
        {
            isGamepad = true;
        }
        else
        {
            isGamepad = false;
        }

        Debug.Log("Gamepad : " + isGamepad);
    }
}
