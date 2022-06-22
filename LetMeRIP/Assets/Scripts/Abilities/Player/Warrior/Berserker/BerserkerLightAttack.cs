using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BerserkerLightAttack : Ability
{
    private Animator animator;
    private Transform attackPoint;

    private float attackRange = 1f;
    private float damage;
    private void Start()
    {
        cooldown = .8f;
        SPCost = 3.2f;
    }
    public override void Init(PlayerController characterController)
    {
        base.Init(characterController);
        attackPoint = transform.Find("AttackPoint");
        animator = GetComponentInChildren<Animator>(false);
        damage = 10 + characterController.stats.strength * 0.35f + characterController.stats.dexterity * 0.1f;
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
            // Create Collider
            Utilities.SpawnHitSphere(attackRange, attackPoint.position, 3f);
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
    }

    public void CancelAction(Berserker b)
    {
        if (characterController == b.GetComponent<PlayerController>())
        {
            StartCoroutine(Cooldown());
            EnableMovement();
            EnableActions();
        }
    }

    public override void StartedAction()
    {
        isReady = false;
        animator.SetTrigger("LightAttack");
        DisableMovement();
    }

    public override void PerformedAction()
    {
        //// Create Collider
        //Utilities.SpawnHitSphere(attackRange, attackPoint.position, 3f);
        //Collider[] hitEnemies = Physics.OverlapSphere(attackPoint.position, attackRange);
        //foreach (Collider enemyHit in hitEnemies)
        //{
        //    if (enemyHit.CompareTag("Enemy"))
        //    {
        //        EnemyForm eform = enemyHit.GetComponent<EnemyForm>();
        //        eform.TakeDamage(damage);
        //    }
        //}

        //StartCoroutine(Cooldown());
    }

    public override void CancelAction()
    {
        /* nothing to see here */
    }
}
