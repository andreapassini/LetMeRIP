using UnityEngine;


public class Billboard : MonoBehaviour
{
    public Transform camera;

    void Start()
    {
        camera = Camera.main.transform;
    }

    void LateUpdate()
    {
        transform.LookAt(transform.position + camera.forward);
    }
}