using UnityEngine;

public abstract class Billboard : MonoBehaviour
{
    private Transform camera;
    protected HudFillingBar healthBar;
    
    void Awake()
    {
        camera = Camera.main.transform;
        healthBar = this.GetComponentInChildren<HudFillingBar>();
    }

    void LateUpdate()
    {
        transform.LookAt(transform.position + camera.forward);
    }
}