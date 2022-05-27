using Photon.Pun;
using UnityEngine;

public class MovementWASD : MonoBehaviour
{
    public PhotonView view;
    
    [SerializeField] private float speed = 5f;
	private Rigidbody rb;
    private Vector3 direction;

    private PlayerInputActions playerInputActions;
    
    

    private void Start()
    {
        rb = GetComponent<Rigidbody>();

        playerInputActions = new PlayerInputActions();
        playerInputActions.Player.Enable();


        view = GetComponent<PhotonView>();
    }

	private void Update()
	{
        if (view.IsMine) GatherInputs();
	}

	private void FixedUpdate()
    {
        if (view.IsMine) Movement();
    }

    public void GatherInputs()
	{
        this.direction = playerInputActions.Player.Movement.ReadValue<Vector3>();
    }


    public void Movement()
    {
        rb.MovePosition(transform.position + this.direction.ToIso() * speed * Time.deltaTime);
    }
}
