using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;
public class CameraRotation : MonoBehaviour
{
    PlayerInputActions playerInputActions;
    float sensitivity = 3f;
    float xRotation;
    float yRotation;
    // Start is called before the first frame update
    void Start()
    {
        playerInputActions = new PlayerInputActions();
        playerInputActions.Enable();
        playerInputActions.Player.Enable();
        playerInputActions.Player.Look.Enable();
        Camera.main.orthographic = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

    }

    // Update is called once per frame
    void Update()
    {
        Vector2 mousePos = playerInputActions.Player.Look.ReadValue<Vector2>() * Time.deltaTime * sensitivity;
        Debug.Log(mousePos);

        yRotation += mousePos.x;
        xRotation -= mousePos.y;

        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        transform.rotation = Quaternion.Euler(xRotation, yRotation, 0f);
    }
}
