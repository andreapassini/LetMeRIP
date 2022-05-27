using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SampleLightAttack : Ability
{
    private LayerMask enemyLayer;
    [SerializeField]private Animator animator;
    private Transform attackPoint;

    private float attackRange = 1f;

    private void Start()
    {
        cooldown = 1f;
        enemyLayer = LayerMask.NameToLayer("Enemy");
    }

    public override void Init()
    {
        attackPoint = transform.Find("AttackPoint");
        animator = GetComponentInChildren<Animator>();
        Debug.Log(animator.name);
    }

    public override void StartedAction()
    {
        isReady = false;
        animator.SetTrigger("LightAttack");
    }

    public override void PerformedAction()
    {
        // Create Collider
        Collider[] hitEnemies = Physics.OverlapSphere(attackPoint.position, attackRange, enemyLayer);

        StartCoroutine(Cooldown());
    }

    public override void CancelAction()
    {
        /* nothing to see here */
    }
}
