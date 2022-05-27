using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

public class LookAtMouse : MonoBehaviour
{
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private Camera camera;

    private Vector3 directionToLook;
    private Rigidbody rb;

    private Quaternion rot;

    private PlayerInputActions playerInputActions;
    public PhotonView view;

    

    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponent<Rigidbody>();

        playerInputActions = new PlayerInputActions();
        playerInputActions.Player.Enable();

        camera = Camera.main;
        view = GetComponent<PhotonView>();

    }

    // Update is called once per frame
    void Update()
    {
        if (!view.IsMine) return;
        
        CalcolateAngle();
    }

	private void FixedUpdate()
    {
        if (!view.IsMine) return;
        
        Rotate();
    }

    private (bool success, Vector3 position) GetMousePosition()
    {
        Ray ray = camera.ScreenPointToRay(playerInputActions.Player.LookAt.ReadValue<Vector2>());

        if (Physics.Raycast(ray, out var hitInfo, Mathf.Infinity, groundMask))
        {
            // If hit something return the pos
            return (success: true, position: hitInfo.point);
        }
        else
        {
            // If hit something return the pos
            return (success: false, position: Vector3.zero);
        }
    }

    private void CalcolateAngle()
    {
        var (success, position) = GetMousePosition();
        if (success)
        {
            // Calculate direction
            directionToLook = position - transform.position;
        }

    }

    void Rotate()
	{
        // to keep the same hight
        directionToLook.y = 0;
        transform.forward = directionToLook;
    }
}
