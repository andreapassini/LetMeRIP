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

    private GameObject prefabSpinEffect;
    private GameObject vfxCharge;

    private void Start()
    {
        cooldown = 4f;
        SPCost = 16f;

        // get the prefabSpinEffect from Resources
        prefabSpinEffect = Resources.Load<GameObject>("Prefabs/SpinEffect");
        vfxCharge = Resources.Load<GameObject>($"Particles/{nameof(SpiritHeavyAttack)}Charge2");
    }

    public override void Init(PlayerController characterController)
    {
        base.Init(characterController);
        attackPoint = transform.Find("AttackPoint");
        animator = GetComponentInChildren<Animator>(false);
        damage = 15 + characterController.stats.strength * 0.3f + characterController.stats.dexterity * 0.3f;
    }

    private void OnEnable()
    {
        BerserkRebroadcastAnimEvent.heavyAttack += PerformedAction;
        BerserkRebroadcastAnimEvent.heavyAttackEnd += CancelAction;
    }

    private void OnDisable()
    {
        BerserkRebroadcastAnimEvent.heavyAttack -= PerformedAction;
        BerserkRebroadcastAnimEvent.heavyAttackEnd -= CancelAction;
    }

    public override void StartedAction()
    {
        isReady = false;
        animator.SetTrigger("HeavyAttackCharge");
        characterController.lam.DisableLookAround();
        DisableMovement();
        //DisableActions();
        StartCoroutine(Charge());
    }

    public override void PerformedAction()
    {
        //animator.SetTrigger("StartChargeHeavyAttack");
        //animator.SetTrigger("Charge");
        //animator.SetTrigger("HeavyAttack");
        //StartCoroutine(Charge());
        //StartCoroutine(Cooldown());
    }

    public void PerformedAction(Berserker b)
    {
        if (characterController == b.GetComponent<PlayerController>())
        {
            StartCoroutine(Cooldown());

            if (isCharged)
            {
                isCharged = false;
                StartCoroutine(MultipleHits(hits, timeOffset));
            }
        }
    }

    public override void CancelAction()
    {
        //if (isCharged)
        //{
        //    isCharged = false;
        //    StartCoroutine(MultipleHits(hits, timeOffset));
        //}
    }

    public void CancelAction(Berserker b)
    {
        if (characterController == b.GetComponent<PlayerController>())
        {
            EnableActions();
            EnableMovement();
            characterController.lam.EnableLookAround();
        }
    }

    private IEnumerator Charge()
    {
        if(!gameObject.TryGetComponent<RagePE>(out RagePE rage))
        {
            //DisableActions();
            DisableMovement();

            // Instantiate ChargeEffect
            vfxCharge ??= Resources.Load<GameObject>($"Particles/{nameof(SpiritHeavyAttack)}Charge2");

            Destroy(Instantiate(vfxCharge, transform), 1.1f);

            yield return new WaitForSeconds(timeToCharge);
        }
        //EnableActions();

        isCharged = true;
        animator.SetTrigger("HeavyAttack");
        //CancelAction();
    }

    private void Hit() 
    {
        //Utilities.SpawnHitSphere(attackRange, transform.position, 3f);
        prefabSpinEffect ??= Resources.Load<GameObject>("Prefabs/SpinEffect");
        Destroy(Instantiate(prefabSpinEffect, transform), .5f);

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
            // Instantiate effect here
            Hit();
            yield return new WaitForSeconds(timeOffset);
        }
    }
}
