using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Bolt;

public class NetPlayerMovement : EntityBehaviour<ICustomCubeState>
{
    public Animator animator;
    public PlayerInputActions playerInputActions;

    private Vector3 direction;
    float speed = 4f;

    // Start() but when entity load into the game
    public override void Attached()
    {
        playerInputActions = new PlayerInputActions();
        playerInputActions.Player.Enable();
        state.SetTransforms(state.CustomCubeTransform, transform);

        state.SetAnimator(animator);
    }

    // Update() on owner pc
    public override void SimulateOwner()
    {
        GatherInputs();
        Move();
    }

    // For every PC, not only owner
    private void Update()
    {
        if (state.IsMoving)
        {
            state.Animator.Play("NetMove");
        }
        else
        {
            state.Animator.Play("NetIdle");
        }
    }

    public void Move()
    {
        //rb.velocity = (transform.position + direction.ToIso() * characterController.currentStats.swiftness/* * Time.deltaTime*/);
        //rb.velocity = direction.ToIso() * speed;
        Vector3 movement = Vector3.zero;

        movement.x += direction.x;
        movement.y += direction.y;
        movement.z += direction.z;

        if(movement != Vector3.zero)
        {
            transform.position = transform.position + (movement.normalized * speed * BoltNetwork.FrameDeltaTime);
            
            state.IsMoving = true;
        }
        else
        {
            state.IsMoving = false;
        }
    }

    public void GatherInputs()
    {
        direction = playerInputActions.Player.Movement.ReadValue<Vector3>();
    }
}
