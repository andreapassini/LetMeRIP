using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovementWASD : MonoBehaviour
{
	[SerializeField] private Rigidbody rb;
	[SerializeField] private float speed = 5f;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    private Vector3 input;

    void Update()
    {
		GatherInput();
    }

	private void FixedUpdate()
	{
		Move();
	}

	private void GatherInput()
	{
		input = new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical"));
	}

	private void Move()
	{
		rb.MovePosition(transform.position + input.ToIso() * speed * Time.deltaTime);
		//rb.velocity = input.ToIso() * speed;
	}
}
