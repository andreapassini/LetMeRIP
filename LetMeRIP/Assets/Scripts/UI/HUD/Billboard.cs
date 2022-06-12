using UnityEngine;

public abstract class Billboard : MonoBehaviour
{
    protected Transform camera;
    protected HudFillingBar healthBar;

    protected virtual void Awake()
    {
        camera = Camera.main.transform;
        healthBar = this.GetComponentInChildren<HudFillingBar>();
    }

    private void LateUpdate()
    {
        transform.LookAt(transform.position + camera.forward);
    }
}