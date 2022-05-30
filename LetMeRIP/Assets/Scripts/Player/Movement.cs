using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Movement : MonoBehaviour
{
	[SerializeField] private float speed = 5f;
	private Rigidbody rb;
    private Vector3 direction;

    private PlayerInputActions playerInputActions;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        playerInputActions = new PlayerInputActions();
        playerInputActions.Player.Enable();
    }

	private void Update()
	{
        GatherInputs();
	}

	private void FixedUpdate()
    {
        Move();
    }

    public void GatherInputs()
    {
        this.direction = playerInputActions.Player.Movement.ReadValue<Vector3>();
    }

    public void Move()
    {
        rb.MovePosition(transform.position + this.direction.ToIso() * speed * Time.deltaTime);
    }


}
