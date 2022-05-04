using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class MovementWASD : MonoBehaviour
{
	[SerializeField] private Rigidbody rb;
	[SerializeField] private float speed = 5f;

    private PlayerInputActions playerInputActions;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();

        playerInputActions = new PlayerInputActions();
        playerInputActions.Player.Enable();

    }

    private void FixedUpdate()
    {
        Movement();
    }


    public void Movement()
    {
        Vector3 direction = playerInputActions.Player.Movement.ReadValue<Vector3>();
        rb.MovePosition(transform.position + direction.ToIso() * speed * Time.deltaTime);
    }
}
