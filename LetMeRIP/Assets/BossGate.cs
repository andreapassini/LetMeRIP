using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class BossGate : MonoBehaviour
{
    private Animator animator;
    [SerializeField] private InputAction action;
    [SerializeField] private GameObject winUI;
    private void OnEnable()
    {
        action.Enable();
    }

    private void OnDisable()
    {
        action.Disable();
    }

    // Start is called before the first frame update
    void Start()
    {
        animator = GetComponent<Animator>();
        action.performed += _ => OpenGate();
    }

    public void OpenGate()
    {
        animator.SetTrigger("open");
    }

    private void OnTriggerEnter(Collider other)
    {
        if(other.CompareTag("Player"))
        winUI.SetActive(true);
    }
}
