using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LookAtMouse : MonoBehaviourPun
{
    [SerializeField] private LayerMask groundMask;
    
    private PlayerInputActions playerInputActions;
    private Camera playerCamera;
    private Rigidbody rb;
    
    private Vector3 directionToLook;
    
    private void Start()
    {
        rb = GetComponent<Rigidbody>();

        playerInputActions = new PlayerInputActions();
        playerInputActions.Player.Enable();

        playerCamera = Camera.main;
    }
    
    private void Update()
    {
        if (!photonView.IsMine) return;
        
        GatherDirectionInput();
    }

	private void FixedUpdate()
	{
        if (!photonView.IsMine) return;
        
        Rotate();
	}
    
    private void GatherDirectionInput()
    {
        Ray ray = playerCamera.ScreenPointToRay(playerInputActions.Player.LookAt.ReadValue<Vector2>());

        this.directionToLook = Physics.Raycast(ray, out var hitInfo, Mathf.Infinity, groundMask)
            ? hitInfo.point - transform.position
            : Vector3.zero;
    }
    
    private void Rotate()
    {
        if (this.directionToLook == Vector3.zero) return;

        this.directionToLook.y = 0;
        transform.forward = this.directionToLook;
    }
}
