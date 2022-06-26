using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Act : Ability
{
    private Animator animator;

    public override void Init(PlayerController characterController)
    {
        base.Init(characterController);
        animator = GetComponentInChildren<Animator>(false);
    }

    public override void StartedAction()
	{
		base.StartedAction();

        animator.SetTrigger("Act");

    }
}
