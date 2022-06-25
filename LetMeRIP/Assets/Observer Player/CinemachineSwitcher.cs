using UnityEngine;
using UnityEngine.InputSystem;
using Cinemachine;

public class CinemachineSwitcher : MonoBehaviour
{
    private static CinemachineSwitcher instance;
    public static CinemachineSwitcher Instance
    {
        get => instance;
    }

    [SerializeField]
    InputAction action;
    [SerializeField]
    private CinemachineVirtualCamera vcam1; // observer
    [SerializeField]
    private CinemachineVirtualCamera vcam2; // player

    private Animator animator;
    private bool observerCamera = false;
    
    private void Awake()
    {
        if (instance != null && instance != this) Destroy(this.gameObject);
        else instance = this;

        animator = GetComponent<Animator>();
    }

    private void OnEnable()
    {
        action.Enable();
    }

    private void OnDisable()
    {
        action.Disable();
    }

    private void Start()
    {
        action.performed += _ => SwitchPriority();
    }

    private void SwitchState()
    {
        Camera.main.orthographic = !Camera.main.orthographic;
        observerCamera = !observerCamera;

        if (observerCamera) 
        {
            animator.Play("Observer");
        }
        else
        {
            animator.Play("Player");
        }
    }

    public void SwitchPriority()
    {
        Camera.main.orthographic = !Camera.main.orthographic;
        observerCamera = !observerCamera;

        if (observerCamera)
        {
            vcam1.Priority = 1;
            vcam2.Priority = 0;
        }
        else
        {
            vcam1.Priority = 0;
            vcam2.Priority = 1;
        }
    }
}
