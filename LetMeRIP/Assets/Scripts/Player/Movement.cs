using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Movement : MonoBehaviourPun
{
	private Rigidbody rb;
    private Vector3 direction;

    public PlayerInputActions playerInputActions;
    public Animator animator;
    private PlayerController characterController;
    FormManager formManager;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponentInChildren<Animator>(false);
        playerInputActions = new PlayerInputActions();
        playerInputActions.Player.Enable();
        characterController = gameObject.GetComponent<PlayerController>();
    }

    public void Init()
    {
        formManager = GetComponent<FormManager>();
        formManager.OnFormChanged += RefreshAnimator;
    }

    private void OnDestroy()
    {
        formManager.OnFormChanged -= RefreshAnimator;
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
        direction = playerInputActions.Player.Movement.ReadValue<Vector3>();

        if (animator != null)
        {
            if (!direction.Equals(Vector3.zero))
            {
                animator.SetBool("isRunning", true);
                //animator.SetBool("isIdle", false);
            }
            else
            {

                //animator.SetBool("isIdle", true);
                animator.SetBool("isRunning", false);
            }
        }
    }

    private static Vector3 RotateVector(Vector3 vec, float rad)
    {
        return new Matrix4x4(
            new Vector4(Mathf.Cos(rad), 0, Mathf.Sin(rad), 0),
            new Vector4(0, 1, 0, 0),
            new Vector4(-Mathf.Sin(rad), 0, Mathf.Cos(rad), 0),
            Vector4.zero
        ) * vec;
    }

    public void Move()
    {
        //rb.velocity = (transform.position + direction.ToIso() * characterController.currentStats.swiftness/* * Time.deltaTime*/);
        rb.velocity = direction.ToIso() * characterController.stats.swiftness;
    }

    private void RefreshAnimator(FormManager fm)
    {
        Debug.Log("Refreshing animator");
        animator = GetComponentInChildren<Animator>(false);
    }

    public void Enable()
    {
        playerInputActions.Player.Movement.Enable();
    }

    public void Disable() 
    {
        playerInputActions.Player.Movement.Disable();
    }
}
