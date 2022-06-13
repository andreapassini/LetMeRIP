using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpiritAbility1 : Ability
{
    private Transform attackPoint;
    private Animator animator;

    private float attackRange = 4f;
    private float coneAngle = 70; // in degrees
    private Coroutine drainPoolCoroutine;
    private float drainRate = 2f;
    private float timeStep = 0.1f; // 20SP/sec
    private bool isDraining;
    private GameObject drainEffect;

    private void Start()
    {
        cooldown = 0.1f;
    }

    public override void Init(PlayerController characterController)
    {
        base.Init(characterController);
        attackPoint = transform.Find("AttackPoint");
        animator = GetComponentInChildren<Animator>(false);
    }

    public override void StartedAction()
    {
        isReady = false;
        //animator.SetTrigger("Ability2");
    }

    public override void PerformedAction()
    {
        // Create Collider
        //float rad = Utilities.DegToRad(coneAngle) * .5f;

        //Vector3 rbound = new Matrix4x4(
        //        new Vector4(Mathf.Cos(rad), 0, Mathf.Sin(rad), 0),
        //        new Vector4(0, 1, 0, 0),
        //        new Vector4(-Mathf.Sin(rad), 0, Mathf.Cos(rad), 0),
        //        Vector4.zero
        //    ) * transform.forward;

        //Vector3 lbound = new Matrix4x4(
        //        new Vector4(Mathf.Cos(rad), 0, -Mathf.Sin(rad), 0),
        //        new Vector4(0, 1, 0, 0),
        //        new Vector4(Mathf.Sin(rad), 0, Mathf.Cos(rad), 0),
        //        Vector4.zero
        //    ) * transform.forward;

        SPPool pool = null;

        Collider[] hitPools = Physics.OverlapSphere(attackPoint.position, attackRange);
        foreach (Collider hitPool in hitPools)
        {
            if (hitPool.CompareTag("Pool"))
            {
                Vector3 poolDirection = hitPool.transform.position - transform.position;
                pool = hitPool.GetComponent<SPPool>();
                isDraining = true;
                DisableMovement();
                drainEffect = Instantiate(Resources.Load<GameObject>("Particles/Recharge"), transform.position, transform.rotation);
                drainEffect.GetComponent<ParticleSystem>().Play();
                drainPoolCoroutine = StartCoroutine(DrainPool(pool));
                StartCoroutine(Cooldown());
                return;
            }
        }

        if (pool == null) // should be always true if this line is reached, but just in case ¯\_(?)_/¯
        {
            isDraining = false;
            isReady = true;
            CancelAction();
            return;
        }
    }

    public override void CancelAction()
    {
        EnableMovement();
        if(drainEffect != null) Destroy(drainEffect);
        if (isDraining) // it wont execute twice if the pool finishes and then receives a button up
        {
            isDraining = false;
            if(drainPoolCoroutine != null) StopCoroutine(drainPoolCoroutine);
        }
    }

    private IEnumerator DrainPool(SPPool pool)
    {
        for(; ; )
        {
            if(pool == null) // aka is destroyed or you simply fucked up
            {
                Debug.Log("POOL IS NULL");
                CancelAction();
                yield break;
            }
            pool.DrainPool(drainRate, characterController);
            Debug.Log($"{name} SG: {characterController.SGManager.SpiritGauge}");
            yield return new WaitForSeconds(timeStep);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.DrawWireSphere(transform.position, attackRange);
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

        Gizmos.DrawRay(transform.position, rbound);
        Gizmos.DrawRay(transform.position, lbound);
    }
}
