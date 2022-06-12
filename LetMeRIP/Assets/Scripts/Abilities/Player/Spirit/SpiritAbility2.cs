using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpiritAbility2 : Ability
{
    private Animator animator;

    private float attackRange = 4f;
    private float damage;
    private void Start()
    {
        cooldown = 0f;
    }

    public override void Init(PlayerController characterController)
    {
        base.Init(characterController);
        animator = GetComponentInChildren<Animator>(false);
        damage = 10 + characterController.stats.intelligence * 0.3f;
    }

    public override void StartedAction()
    {
        isReady = false;
        //animator.SetTrigger("Ability2");
    }

    public override void PerformedAction()
    {
        // Create Collider
        Collider[] hitEnemies = Physics.OverlapSphere(transform.position, attackRange);
        foreach (Collider enemyHit in hitEnemies)
        {
            if (enemyHit.CompareTag("Enemy"))
            {
                EnemyForm eform = enemyHit.GetComponent<EnemyForm>();
                eform.TakeDamage(damage);

                StunEE stunEffect = eform.gameObject.AddComponent<StunEE>(); // standard duration is 1.5f
                stunEffect.StartEffect();
                Debug.Log($"{enemyHit.name} stunned");
            }
        }

        StartCoroutine(Cooldown());
    }
    
    public override void CancelAction()
    {
        /* nothing to see here */
    }

}
