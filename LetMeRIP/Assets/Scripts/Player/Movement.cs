using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Movement : MonoBehaviourPun
{
    [SerializeField] private float speed = 5f;
	private Rigidbody rb;
    private Vector3 direction;

    private PlayerInputActions playerInputActions;
    private Animator animator;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponentInChildren<Animator>(false);
        
        playerInputActions = new PlayerInputActions();
        playerInputActions.Player.Enable();
        FormManager.OnFormChanged += RefreshAnimator;
    }
    private void OnDestroy()
    {
        FormManager.OnFormChanged -= RefreshAnimator;
    }

    private void Update()
	{
        if (!photonView.IsMine) return;
        GatherInputs();
	}

	private void FixedUpdate()
    {
        if (!photonView.IsMine) return;
        Move();
    }

    public void GatherInputs()
    {
        this.direction = playerInputActions.Player.Movement.ReadValue<Vector3>();
        if (animator != null)
        {
            if (!direction.Equals(Vector3.zero))
            {
                //animator.SetBool("isRunning", true);
                //animator.SetBool("isIdle", false);
            }
            else
            {
                //animator.SetBool("isIdle", true);
                //animator.SetBool("isRunning", false);
            }
        }
    }

    public void Move()
    {
        rb.MovePosition(transform.position + this.direction.ToIso() * speed * Time.deltaTime);
    }

    private void RefreshAnimator(FormManager fm)
    {
        animator = GetComponentInChildren<Animator>(false);
    }
}
