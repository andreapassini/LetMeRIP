using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpiritHeavyAttack : Ability
{
    private Animator animator;
    private Transform attackPoint;

    private float attackRange = 6f;
    private float damage;
    private float coneAngle = 100f; // in degrees
    private float stunDistance = 3.5f;
    private bool isCharged = false;
    private float timeToCharge = 1.5f;
    private GameObject vfx;
    private GameObject vfxCharge;
    private void Start()
    {
        cooldown = 4f;
        vfx = Resources.Load<GameObject>($"Particles/{nameof(SpiritHeavyAttack)}");
        vfxCharge = Resources.Load<GameObject>($"Particles/{nameof(SpiritHeavyAttack)}Charge");
    }

    private void OnEnable()
    {
        SpiritFormRebroadcastAnimEvent.heavyAttack += PerformedAction;
        SpiritFormRebroadcastAnimEvent.heavyAttackEnd += CancelAction;
    }

    private void OnDisable()
    {
        SpiritFormRebroadcastAnimEvent.heavyAttack -= PerformedAction;
        SpiritFormRebroadcastAnimEvent.heavyAttackEnd -= CancelAction;
    }

    public override void Init(PlayerController characterController)
    {
        base.Init(characterController);
        attackPoint = transform.Find("AttackPoint");
        animator = GetComponentInChildren<Animator>(false);
        damage = 15 + characterController.stats.intelligence * 0.2f + characterController.stats.strength * 0.1f;
    }

    public override void StartedAction()
    {
        isReady = false;
        animator.SetTrigger("HeavyAttackCharge");
        StartCoroutine(Cooldown());
    }

    public override void PerformedAction()
    {
        //disable movement while charging
        //characterController.movement.enabled = false;
        
        //animator.SetTrigger("StartChargeHeavyAttack");
        //animator.SetTrigger("Charge");
        StartCoroutine(Charge());
    }

    public void PerformedAction(SpiritForm spiritForm)
    {

    }

    public override void CancelAction()
    {
        if (isCharged)
        {
            isCharged = false;
            animator.SetTrigger("HeavyAttack");
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
            vfx ??= Resources.Load<GameObject>($"Particles/{nameof(SpiritHeavyAttack)}");
            Destroy(Instantiate(vfx, transform.position, transform.rotation), 2f);
            //Utilities.SpawnHitSphere(attackRange, transform.position, 3f);
            foreach (Collider enemyHit in hitEnemies)
            {
                if (enemyHit.CompareTag("Enemy"))
                {
                    Vector3 enemyDirection = enemyHit.transform.position - transform.position;
                    if (Vector3.Dot(enemyDirection, lbound) > 0 && Vector3.Dot(enemyDirection, rbound) > 0)
                    {
                        EnemyForm eform = enemyHit.GetComponent<EnemyForm>();
                        eform.TakeDamage(damage);

                        StunEE stunEffect = eform.gameObject.AddComponent<StunEE>();
                        stunEffect.StartEffect();
                        Debug.Log($"{enemyHit.name} stunned");
                    }
                }
            }

            // enable movement
            //characterController.movement.enabled = true;
        }
    }

    public void CancelAction(SpiritForm spiritForm)
    {

    }

    private IEnumerator Charge()
    {
        DisableActions();

        vfxCharge ??= Resources.Load<GameObject>($"Particles/{nameof(SpiritHeavyAttack)}Charge");
        Destroy(Instantiate(vfxCharge, transform.position, transform.rotation), 2f);

        yield return new WaitForSeconds(timeToCharge);
        
        EnableActions();

        isCharged = true;
        CancelAction();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
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

        Gizmos.DrawRay(transform.position, lbound);
        Gizmos.DrawRay(transform.position, rbound);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, stunDistance);
    }
}
