using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Act : Ability
{
    private Animator animator;

    private GameObject prefab;

    public override void Init(PlayerController characterController)
    {
        base.Init(characterController);
        animator = GetComponentInChildren<Animator>(false);
        prefab = Resources.Load<GameObject>("Particles/SpiritRise");
    }

    public override void StartedAction()
	{
		base.StartedAction();

        animator.SetTrigger("Act");

        prefab ??= Resources.Load<GameObject>("Particles/SpiritRise");
        Instantiate(prefab, transform.parent);
    }
}
