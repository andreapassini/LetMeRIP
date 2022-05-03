using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovementWASD : MonoBehaviour
{
	[SerializeField] private Rigidbody2D rb;
	[SerializeField] private float speed = 5f;


	private Vector3 input;

    void Update()
    {
        
    }

	private void FixedUpdate()
	{
		
	}

	private void GatherInput()
	{
		input = new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical"));
	}

	private void Move()
	{
		rb.MovePosition(transform.position + input * speed * Time.deltaTime);
	}
}
