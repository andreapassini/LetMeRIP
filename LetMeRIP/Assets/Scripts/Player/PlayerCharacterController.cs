using UnityEngine;

public class PlayerCharacterController : MonoBehaviour
{
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float speed = 5f;


    private Camera playerCamera;
    private Rigidbody rb;
    private Vector3 movementDirection;
    private Vector3 directionToLook;

    private PlayerInputActions playerInputActions;

    private void Awake()
    {
        playerInputActions = new PlayerInputActions();
        playerInputActions.Player.Enable();
    }

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        playerCamera = Camera.main;
    }

    private void Update()
    {
        GatherInputs();
    }

    private void FixedUpdate()
    {
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

        if (Physics.Raycast(ray, out var hitInfo, Mathf.Infinity, groundMask))
            this.directionToLook = hitInfo.point - transform.position;
        else this.directionToLook = Vector3.zero;
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
}