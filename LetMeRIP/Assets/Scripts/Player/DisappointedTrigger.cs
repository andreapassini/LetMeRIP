using UnityEngine;
using UnityEngine.InputSystem;

public class DisappointedTrigger : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] InputAction action;

    private void OnEnable()
    {
        action.Enable();
    }

    private void OnDisable()
    {
        action.Disable();
    }

    void Start()
    {
        animator = GetComponent<Animator>();
        action.performed += _ => DisappointedAction();
        GetComponent<FormManager>().OnFormChanged += _ => RefreshAnimator();
    }

    private void DisappointedAction()
    {
        if (animator != null) animator.Play("Disappointed");
        else Debug.LogError("Disappointed action failed, animator is null");
    }

    [ContextMenu("Refresh animator")]
    public void RefreshAnimator()
    {
        animator = GetComponent<Animator>();
    }
}
