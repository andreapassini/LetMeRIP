using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SyncedMovement : MonoBehaviour
{
    private PhotonView PV;

    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float speed = 5f;

    private Camera playerCamera;
    private Rigidbody rb;
    private Vector3 movementDirection;
    private Vector3 directionToLook;

    private PlayerInputActions playerInputActions;

    private void Awake()
    {
        PV = GetComponentInParent<PhotonView>();
        rb = GetComponent<Rigidbody>();

        playerInputActions = new PlayerInputActions();
        playerInputActions.Player.Enable();

        playerCamera = Camera.main;
    }

    private void Start()
    {
        if (!PV.IsMine) Destroy(rb);
    }

    private void Update()
    {
        if (!PV.IsMine) return;

        GatherInputs();
    }

    private void FixedUpdate()
    {
        if (!PV.IsMine) return;

        Move();
        Rotate();
    }

    private void GatherInputs()
    {
        GatherMovementInput();
        GatherDirectionInput();
    }

    private void GatherMovementInput()
    {
        this.movementDirection = playerInputActions.Player.Movement.ReadValue<Vector3>();
    }

    private void GatherDirectionInput()
    {
        Ray ray = playerCamera.ScreenPointToRay(playerInputActions.Player.LookAt.ReadValue<Vector2>());

        this.directionToLook = Physics.Raycast(ray, out var hitInfo, Mathf.Infinity, groundMask)
            ? hitInfo.point - transform.position
            : Vector3.zero;
    }

    private void Move()
    {
        rb.MovePosition(transform.position + this.movementDirection.ToIso() * speed * Time.deltaTime);
    }

    private void Rotate()
    {
        if (this.directionToLook == Vector3.zero) return;

        this.directionToLook.y = 0;
        transform.forward = this.directionToLook;
    }

    //public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    //{
        
    //}
}
