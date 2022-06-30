using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class DisappointedTrigger : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] InputAction enterDisappointed;
    [SerializeField] InputAction exitDisappointed;

    private void OnEnable()
    {
        enterDisappointed.Enable();
        exitDisappointed.Enable();
    }

    private void OnDisable()
    {
        enterDisappointed.Disable();
        exitDisappointed.Disable();
    }

    void Start()
    {
        animator = GetComponent<Animator>();
        enterDisappointed.performed += _ => DisappointedAction();
        exitDisappointed.performed += _ => ExitDisappointedAction();
        GetComponent<FormManager>().OnFormChanged += _ => RefreshAnimator();
    }

    private void ExitDisappointedAction()
    {
        animator.SetTrigger("Disappointed");
    }

    private void DisappointedAction()
    {
        if (animator != null) animator.SetTrigger("DisappointedEnter");
        else Debug.LogError("Disappointed action failed, animator is null");
    }

    [ContextMenu("Refresh animator")]
    public void RefreshAnimator()
    {
        animator = GetComponentInChildren<Animator>();
    }
}
