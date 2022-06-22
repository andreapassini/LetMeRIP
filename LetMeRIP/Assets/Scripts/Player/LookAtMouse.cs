using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LookAtMouse : MonoBehaviourPun
{
    public LayerMask groundMask;
    
    private PlayerInputActions playerInputActions;
    private Camera playerCamera;
    private Vector3 directionToLook;
    private bool isEnabled = true;

    private void Start()
    {
        playerInputActions = new PlayerInputActions();
        playerInputActions.Player.Enable();

        playerCamera = Camera.main;
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
        Ray ray = playerCamera.ScreenPointToRay(playerInputActions.Player.LookAt.ReadValue<Vector2>());

        Vector3 direction = Physics.Raycast(ray, out var hitInfo, Mathf.Infinity, groundMask)
            ? hitInfo.point - transform.position
            : Vector3.zero;
        direction.y = 0;
        return direction.normalized;
    }
    
    private void Rotate()
    {
        if (this.directionToLook == Vector3.zero) return;

        this.directionToLook.y = 0;
        transform.forward = this.directionToLook;
    }

    public void EnableLookAround()
    {
        isEnabled = true;
    }

    public void DisableLookAround()
    {
        isEnabled = false;
    }
}
