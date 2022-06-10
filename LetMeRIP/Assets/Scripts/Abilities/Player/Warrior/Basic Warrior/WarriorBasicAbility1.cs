using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WarriorBasicAbility1 : Ability
{

    private Animator animator;
    private Transform attackPoint;

    private float attackRange = 3f;
    private float damage;
    private float heal;
    private float healDecayTime; // in seconds
    private float coneAngle = 70f; // in degrees
    private float DegToRad(float deg) => deg * 0.01745f;

    private void Start()
    {
        cooldown = 6f;
    }

    public override void Init(PlayerController characterController)
    {
        base.Init(characterController);
        attackPoint = transform.Find("AttackPoint");
        animator = GetComponentInChildren<Animator>(false);
        damage = 10 + characterController.bodyStats.strength * 0.4f;
        heal = 20 + characterController.bodyStats.strength * .3f + characterController.bodyStats.intelligence * .2f;
        healDecayTime = 4f;
    }

    public override void StartedAction()
    {
        isReady = false;
        animator.SetTrigger("Ability1");
    }

    public override void PerformedAction()
    {
        // Create Collider
        PerformCoroutine(0.4f);
        StartCoroutine(Cooldown());
    }

    public override void CancelAction()
    {
        /* nothing to see here */
    }

    private IEnumerator PerformCoroutine(float castingTime)
    {
        //animation
        DisableActions();
        yield return new WaitForSeconds(castingTime / 2);

        float rad = DegToRad(coneAngle) * .5f;

        Vector3 rbound = new Matrix4x4(
                new Vector4(Mathf.Cos(rad), 0, Mathf.Sin(rad), 0),
                new Vector4(0, 1, 0, 0),
                new Vector4(-Mathf.Sin(rad), 0, Mathf.Cos(rad), 0),
                Vector4.zero
            ) * transform.forward;

        Vector3 lbound = new Matrix4x4(
                new Vector4(Mathf.Cos(rad), 0, -Mathf.Sin(rad), 0),
                new Vector4(0, 1, 0, 0),
                new Vector4(Mathf.Sin(rad), 0, Mathf.Cos(rad), 0),
                Vector4.zero
            ) * transform.forward;

        Collider[] hitEnemies = Physics.OverlapSphere(attackPoint.position, attackRange);
        int hits = 0;
        foreach (Collider enemyHit in hitEnemies)
        {
            if (enemyHit.CompareTag("Enemy"))
            {
                Vector3 enemyDirection = enemyHit.transform.position - transform.position;
                if (Vector3.Dot(enemyDirection, lbound) > 0 && Vector3.Dot(enemyDirection, rbound) > 0)
                {
                    EnemyForm eform = enemyHit.GetComponent<EnemyForm>();
                    eform.TakeDamage(damage);
                    hits++;
                }
            }
        }

        if (hits > 0 && photonView.IsMine) characterController.HPManager.DecayingHeal(heal * hits, healDecayTime);
        
        yield return new WaitForSeconds(castingTime / 2);
        EnableActions();
    }
}
