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
    private Animator animator;
    private PlayerController characterController;
    private LookAtMouse lam;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponentInChildren<Animator>(false);
        characterController = gameObject.GetComponent<PlayerController>();
        lam = GetComponent<LookAtMouse>();    
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
        direction = playerInputActions.Player.Movement.ReadValue<Vector3>();
        //Vector3 walkingDirection = playerInputActions.Player.Movement.ReadValue<Vector3>();
        //Debug.Log($"Walking direction {walkingDirection}");
        //Vector3 lookDirection = lam.GatherDirectionInput();
        //Debug.Log($"look direction {lookDirection}");
        //float rotationAngle = Mathf.Acos(Vector3.Dot(walkingDirection, lookDirection));
        //Debug.Log($"rotation angle {rotationAngle}");
        //Vector3 animationDirection = RotateVector(walkingDirection, rotationAngle);
        //Debug.Log($"final direction {animationDirection}");
        //animator.SetFloat("VelocityZ", animationDirection.z);
        //animator.SetFloat("VelocityX", animationDirection.x);

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
        rb.MovePosition(transform.position + direction.ToIso() * characterController.currentStats.swiftness * Time.deltaTime);
    }

    private void RefreshAnimator(FormManager fm)
    {
        animator = GetComponentInChildren<Animator>(false);
    }
}
