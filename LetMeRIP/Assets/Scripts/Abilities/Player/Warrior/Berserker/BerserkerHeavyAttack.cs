using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BerserkerHeavyAttack : Ability
{
    private Animator animator;
    private Transform attackPoint;

    private float attackRange = 6f;
    private float damage;
    private bool isCharged = false;
    private float timeToCharge = 1f;
    private int hits = 2;
    private float timeOffset = 0.1f;
    private bool isEnraged = false;
    private Berserker berserker;

    private void Start()
    {
        cooldown = 4f;
        SPCost = 16f;
    }

    public override void Init(PlayerController characterController)
    {
        base.Init(characterController);
        attackPoint = transform.Find("AttackPoint");
        animator = GetComponentInChildren<Animator>(false);
        damage = 15 + characterController.spiritStats.strength * 0.3f + characterController.spiritStats.dexterity * 0.3f;
    }

    public override void StartedAction()
    {
        isReady = false;
    }

    public override void PerformedAction()
    {
        //animator.SetTrigger("StartChargeHeavyAttack");
        //animator.SetTrigger("Charge");

        StartCoroutine(Charge());
        StartCoroutine(Cooldown());
    }

    public override void CancelAction()
    {
        if (isCharged)
        {
            isCharged = false;
            StartCoroutine(MultipleHits(hits, timeOffset));
        }
    }

    private IEnumerator Charge()
    {
        if(!gameObject.TryGetComponent<RagePE>(out RagePE rage))
        {
            DisableActions();
            yield return new WaitForSeconds(timeToCharge);
        }
        EnableActions();

        isCharged = true;
        CancelAction();
    }

    private void Hit() 
    {
        Utilities.SpawnHitSphere(attackRange, transform.position, 3f);
        Collider[] hitEnemies = Physics.OverlapSphere(attackPoint.position, attackRange);
        foreach (Collider enemyHit in hitEnemies)
        {
            if (enemyHit.CompareTag("Enemy"))
            {
                EnemyForm eform = enemyHit.GetComponent<EnemyForm>();
                eform.TakeDamage(damage);
            }
        }
    }

    private IEnumerator MultipleHits(float amount, float timeOffset)
    {
        for(int i = 0; i < amount; i++)
        {
            // animation here
            Hit();
            yield return new WaitForSeconds(timeOffset);
        }
    }
}
