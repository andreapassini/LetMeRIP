using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ClericAbility1 : Ability
{
    [SerializeField]
    private Animator animator;
    private Rigidbody rb;
    private Transform attackPoint;

    // prevents the cancel action to start too soon
    private bool isCasting = false;
    private float minHeal;
    private float maxHeal;

    private float maxChargeTime = 3f;
    private Coroutine chargeCor;

    private float startTime;

    // Start is called before the first frame update
    void Start()
    {
        cooldown = 9f;
        SPCost = 36f;
    }

    public override void Init(PlayerController characterController)
    {
        base.Init(characterController);
        attackPoint = transform.Find("AttackPoint");
        rb = GetComponent<Rigidbody>();
        animator = GetComponentInChildren<Animator>(false);

        minHeal = (float)(35 + characterController.currentStats.intelligence * 0.4f);

        maxHeal = (float)(50 + characterController.currentStats.intelligence * 0.8f);
    }

    /**
     * Marks the ability as on-cooldown, registers the direction and disables the movement if the latter is different from 0
     */
    public override void StartedAction()
    {
        animator ??= GetComponentInChildren<Animator>(false);
        isReady = false;

        // dash animation
        animator.SetTrigger("HeavyAttack");


    }

    /**
     * Starts dashing if the recorded direction is different from 0
     */
    public override void PerformedAction()
    {
        startTime = Time.time;
        chargeCor = StartCoroutine(ChargeHealingPot());
        DisableActions();
    }

    /**
     * if the dash action has finished, ri-enables movement
     */
    public override void CancelAction()
    {
        StopCoroutine(chargeCor);
        animator.SetTrigger("HeayAttack");
        //HammerDown(); Call this from the animation event

        EnableActions();
        StartCoroutine(Cooldown());
    }

    public void HammerDown()
    {
        float difTime = Time.time - startTime;

        // Calculate damage
        float damage = Mathf.Clamp(minHeal + difTime, minHeal, maxHeal);

        // Calcolate position

        // Create AOE
    }

    private IEnumerator ChargeHealingPot()
    {
        yield return new WaitForSeconds(maxChargeTime);
        CancelAction();
    }
}
