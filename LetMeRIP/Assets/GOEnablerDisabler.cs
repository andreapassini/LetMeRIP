using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GOEnablerDisabler : Interactable
{
    [SerializeField] private List<GameObject> gos;
    private Animator animator;

    protected override void Start()
    {
        base.Start();
        animator = gameObject.GetComponentInChildren<Animator>();
    }

    public override void Effect(PlayerController characterController)
    {
        base.Effect(characterController);

        animator.SetBool("isLeverPulled", !animator.GetBool("isLeverPulled"));
        foreach(GameObject go in gos)
        {
            go.SetActive(!go.activeSelf);
        }
    }

}
