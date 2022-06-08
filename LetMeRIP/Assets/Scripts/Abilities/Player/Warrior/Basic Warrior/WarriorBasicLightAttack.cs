using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WarriorBasicLightAttack : Ability
{
    private LayerMask enemyLayer;
    private Animator animator;
    private Transform attackPoint;

    private float attackRange = 1f;
    private float damage;
    private void Start()
    {
        cooldown = .86f;
        enemyLayer = LayerMask.NameToLayer("Enemy");
    }
    public override void Init(PlayerController characterController)
    {
        base.Init(characterController);
        attackPoint = transform.Find("AttackPoint");
        animator = GetComponentInChildren<Animator>(false);
        damage = 10 + characterController.bodyStats.strength * 0.1f + characterController.bodyStats.dexterity * 0.1f;
    }

    public override void StartedAction()
    {
        isReady = false;
        animator.SetTrigger("LightAttack");
    }

    public override void PerformedAction()
    {
        // Create Collider
        Collider[] hitEnemies = Physics.OverlapSphere(attackPoint.position, attackRange);
        foreach(Collider enemyHit in hitEnemies)
        {
            if (enemyHit.CompareTag("Enemy"))
            {
                EnemyForm eform = enemyHit.GetComponent<EnemyForm>();
                eform.TakeDamage(damage);
            }
        }

        StartCoroutine(Cooldown());
    }

    public override void CancelAction()
    {
        /* nothing to see here */
    }

}
