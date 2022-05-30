using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Dash : Ability
{
    private PlayerInputActions playerInputActions;
    private Animator animator;
    private Rigidbody rb;
    
    private readonly float time = 0.1f;
    private float currentTime;
    private float speed = 30f;
    private Vector3 direction;

    // prevents the cancel action to start too soon
    private bool isDashing = false;

    private void Start()
    {
        cooldown = .7f;
        rb = GetComponent<Rigidbody>();
        playerInputActions = new PlayerInputActions();
    }

    public override void Init(PlayerController characterController)
    {
        base.Init(characterController);
        animator = GetComponentInChildren<Animator>(false);
    }

    /**
     * Marks the ability as on-cooldown, registers the direction and disables the movement if the latter is different from 0
     */
    public override void StartedAction()
    {
        Debug.Log("Dash starting");
        isReady = false;

        direction = playerInputActions.Player.Movement.ReadValue<Vector3>();

        // you can't move while dashing
        if (!direction.Equals(Vector3.zero)) // prevents unresponsive movement if the player tries to dash when standing and moving right after
            playerInputActions.Player.Movement.Disable(); 

        currentTime = time;
        isDashing = true;

        // dash animation
    }

    /**
     * Starts dashing if the recorded direction is different from 0
     */
    public override void PerformedAction()
    {
        Debug.Log("Dashing");
        if (!direction.Equals(Vector3.zero))
        {
            StartCoroutine(DashAction());
            StartCoroutine(Cooldown());
        } else
        {
            Debug.Log("Missing direction");
            isReady = true;
            isDashing = false;
        }
    }

    /**
     * if the dash action has finished, ri-enables movement
     */
    public override void CancelAction()
    {
        if (!isDashing)
        {
            Debug.Log("Dash finished");
            playerInputActions.Player.Movement.Enable(); // you can't move while dashing
        }
    }

    /**
     * moves the player in the recorded direction for time seconds
     */
    private IEnumerator DashAction()
    {
        if(currentTime > 0)
        {
            currentTime -= Time.deltaTime;
            rb.MovePosition(transform.position + this.direction.ToIso() * speed * Time.deltaTime);
            yield return new WaitForFixedUpdate();
            StartCoroutine(DashAction());
        } else
        {
            isDashing = false;
            CancelAction();
        }
    }
}
