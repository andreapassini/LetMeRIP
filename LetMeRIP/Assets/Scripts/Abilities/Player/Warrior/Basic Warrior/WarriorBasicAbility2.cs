using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WarriorBasicAbility2 : Ability
{
    private Animator animator;
    private Transform attackPoint;

    private float attackRange = 6f;
    private float damage;
    private float coneAngle = 100f; // in degrees
    private float slowFactor = .65f;
    private float stunDistance = 3.5f;

    private void Start()
    {
        SPCost = 10f;
        cooldown = .1f;
    }

    public override void Init(PlayerController characterController)
    {
        base.Init(characterController);
        attackPoint = transform.Find("AttackPoint");
        animator = GetComponentInChildren<Animator>(false);
        damage = 10 + characterController.currentStats.strength * 0.4f;
    }

    public override void StartedAction()
    {
        isReady = false;
        animator.SetTrigger("Ability2");
    }

    public override void PerformedAction()
    {

        StartCoroutine(PerformCoroutine(.4f));
        StartCoroutine(Cooldown());
    }

    public override void CancelAction()
    {
        /* nothing to see here */
    }

    private IEnumerator PerformCoroutine(float castingTime)
    {
        DisableActions();
        yield return new WaitForSeconds(castingTime / 2);

        // Cone shape (it's more like a sphere sector)
        float rad = Utilities.DegToRad(coneAngle) * .5f;

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
        foreach (Collider enemyHit in hitEnemies)
        {
            if (enemyHit.CompareTag("Enemy"))
            {
                Vector3 enemyDirection = enemyHit.transform.position - transform.position;
                if (Vector3.Dot(enemyDirection, lbound) > 0 && Vector3.Dot(enemyDirection, rbound) > 0)
                {
                    EnemyForm eform = enemyHit.GetComponent<EnemyForm>();
                    eform.TakeDamage(damage);
                    Debug.Log($"Distance from {enemyHit.name}: {(enemyHit.transform.position - transform.position).magnitude}");
                    if ((enemyHit.transform.position - transform.position).magnitude < stunDistance)
                    {
                        StunEE stunEffect = eform.gameObject.AddComponent<StunEE>();
                        stunEffect.StartEffect();
                        Debug.Log($"{enemyHit.name} stunned");
                    }
                    else
                    {
                        SlowEE slowEffect = eform.gameObject.AddComponent<SlowEE>();
                        slowEffect.SlowFactor = slowFactor;
                        slowEffect.StartEffect();
                        Debug.Log($"{enemyHit.name} slowed");
                    }
                }
            }
        }
        yield return new WaitForSeconds(castingTime / 2);
        EnableActions();
    }

    //private void OnDrawGizmos()
    //{
    //    Gizmos.color = Color.red;
    //    Gizmos.DrawWireSphere(transform.position, attackRange);

    //    float rad = Utilities.DegToRad(coneAngle) * .5f;

    //    Vector3 rbound = new Matrix4x4(
    //            new Vector4(Mathf.Cos(rad), 0, Mathf.Sin(rad), 0),
    //            new Vector4(0, 1, 0, 0),
    //            new Vector4(-Mathf.Sin(rad), 0, Mathf.Cos(rad), 0),
    //            Vector4.zero
    //        ) * transform.forward;

    //    Vector3 lbound = new Matrix4x4(
    //            new Vector4(Mathf.Cos(rad), 0, -Mathf.Sin(rad), 0),
    //            new Vector4(0, 1, 0, 0),
    //            new Vector4(Mathf.Sin(rad), 0, Mathf.Cos(rad), 0),
    //            Vector4.zero
    //        ) * transform.forward;

    //    Gizmos.DrawRay(transform.position, lbound);
    //    Gizmos.DrawRay(transform.position, rbound);

    //    Gizmos.color = Color.yellow;
    //    Gizmos.DrawWireSphere(transform.position, stunDistance);
    //}
}
