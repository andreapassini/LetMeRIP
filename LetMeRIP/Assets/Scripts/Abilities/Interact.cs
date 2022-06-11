using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Interact : Ability
{
    private Transform direction;
    private float range = 2.5f;

    private void Start()
    {
        SPCost = 0f;
        cooldown = .1f;
    }

    public override void Init(PlayerController characterController)
    {
        base.Init(characterController);
        direction = transform.Find("AttackPoint");
    }

    public override void StartedAction()
    {
        isReady = false;
    }

    public override void PerformedAction()
    {
        Utilities.SpawnHitSphere(range, direction.position, 3f);

        Collider[] hits = Physics.OverlapSphere(direction.position, range);
        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Interactable"))
            {
                Debug.Log("INTERACTABLE FOUND");
                Interactable obj = hit.GetComponent<Interactable>();
                obj.Effect(characterController);
                break; // interact once
            }
        }
        StartCoroutine(Cooldown());
    }

    public override void CancelAction()
    {
        /* nothing to see here */
    }
}
