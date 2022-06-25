using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraMovement : MonoBehaviour
{
    [SerializeField] private AnimationCurve speedCurve;
    [SerializeField] private float movementSpeed = 5f;

    [SerializeField] private List<Transform> roomSpawns;

    [SerializeField] private InputAction switchToRoom1; 
    [SerializeField] private InputAction switchToRoom2; 
    [SerializeField] private InputAction switchToRoom3; 
    [SerializeField] private InputAction switchToRoom4;


    private float time;
    private Rigidbody rb;
    private Vector3 direction;
    
    private Transform cameraTransform;
    private InputManager inputManager;

    private void OnEnable()
    {
        switchToRoom1.Enable();
        switchToRoom2.Enable();
        switchToRoom3.Enable();
        switchToRoom4.Enable();
    }

    private void OnDisable()
    {
        switchToRoom1.Disable();
        switchToRoom2.Disable();
        switchToRoom3.Disable();
        switchToRoom4.Disable();
    }

    private void Start()
    {
        inputManager = InputManager.Instance;
        rb = GetComponent<Rigidbody>();

        cameraTransform = Camera.main.transform;

        switchToRoom1.performed += _ => SwitchRoom(0);
        switchToRoom2.performed += _ => SwitchRoom(1);
        switchToRoom3.performed += _ => SwitchRoom(2);
        switchToRoom4.performed += _ => SwitchRoom(3);
    }

    private void SwitchRoom(int room)
    {
        Debug.Log("HELLO");
        transform.position = roomSpawns[room].position;
    }

    private void Update()
    {
        GatherInputs();
    }

    private void FixedUpdate()
    {
        Move();
    }

    private void GatherInputs()
    {
        direction = inputManager.GetMovement();

        if (!direction.Equals(Vector3.zero)) time += Time.deltaTime;
        else time = 0;
    }

    private void Move()
    {
        rb.velocity = (cameraTransform.forward * direction.z + cameraTransform.right * direction.x) * movementSpeed * speedCurve.Evaluate(time);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.forward);
    }
}
