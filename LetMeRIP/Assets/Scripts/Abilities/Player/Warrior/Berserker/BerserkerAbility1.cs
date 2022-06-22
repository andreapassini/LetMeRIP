using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BerserkerAbility1 : Ability
{
    private Animator animator;
    // scalings are in RagePE

    private void Start()
    {
        cooldown = 0.2f;
        SPCost = 36f;
    }

    public override void Init(PlayerController characterController)
    {
        base.Init(characterController);
        animator = GetComponentInChildren<Animator>(false);
    }

    public override void StartedAction()
    {
        isReady = false;
        //animator.SetTrigger("Ability2");
    }

    private void OnEnable()
    {
        BerserkRebroadcastAnimEvent.ability2 += PerformedAction;
        BerserkRebroadcastAnimEvent.ability2End += CancelAction;
    }

    private void OnDisable()
    {
        BerserkRebroadcastAnimEvent.ability2 -= PerformedAction;
        BerserkRebroadcastAnimEvent.ability2End -= CancelAction;
    }

    public void PerformedAction(Berserker b)
    {
        if (characterController == b.GetComponent<PlayerController>())
        {

        }
    }

    public void CancelAction(Berserker b)
    {
        if (characterController == b.GetComponent<PlayerController>())
        {

        }
    }

    public override void PerformedAction()
    {
        // Create Collider
        animator.SetTrigger("Ability1");
        StartCoroutine(PerformCoroutine(.4f));
        StartCoroutine(Cooldown());
    }

    public override void CancelAction()
    {
        /* nothing to see here */
    }



    private IEnumerator PerformCoroutine(float waitTime)
    {
        DisableActions();
        yield return new WaitForSeconds(waitTime); // let the animation play
        RagePE rage = gameObject.AddComponent<RagePE>();
        rage.StartEffect();
        Utilities.SpawnHitSphere(0.5f, transform.position, 8f);
        EnableActions();
    }
}
