using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GatesUnlocker : Interactable
{
    [SerializeField] private List<Gate> gates;
    private bool isUsed = false;
    private Animator animator;

    protected override void Start()
    {
        base.Start();
        animator = gameObject.GetComponentInChildren<Animator>();
    }

    public override void Effect(PlayerController characterController)
    {
        base.Effect(characterController);

        if (!isUsed)
        {
            animator.SetBool("isLeverPulled", true);
            foreach(Gate gate in gates)
            {
                gate.isBlocked = false;
                Debug.Log($"Gate {gate.name} unlocked!");
            }
        } else
        {
            Debug.Log("It doesn't seem to move...");
        }
    }

}
