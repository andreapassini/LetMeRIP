using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ClericHeavyAttack : Ability
{
    [SerializeField]
    private Animator animator;
    private Rigidbody rb;
    private Transform attackPoint;

    // prevents the cancel action to start too soon
    private bool isCasting = false;
    private float minDamage;
    private float maxDamage;

    [SerializeField]
    private float minArea = 2f;

    [SerializeField]
    private float maxArea = 4f;

    private float maxChargeTime = 2.5f;
    private Coroutine chargeCor;

    private float startTime;

    // Start is called before the first frame update
    void Start()
    {
        cooldown = 3.5f;
        SPCost = 14f;
    }

    public override void Init(PlayerController characterController)
    {
        base.Init(characterController);
        attackPoint = transform.Find("AttackPoint");
        rb = GetComponent<Rigidbody>();
        animator = GetComponentInChildren<Animator>(false);

        minDamage = (float)(15 + characterController.stats.intelligence * 0.2f +
            0.1 * characterController.stats.strength);

        maxDamage = (float)(40 + characterController.stats.intelligence * 0.3f +
            0.3 * characterController.stats.strength);
    }

    /**
     * Marks the ability as on-cooldown, registers the direction and disables the movement if the latter is different from 0
     */
    public override void StartedAction()
    {
        animator ??= GetComponentInChildren<Animator>(false);
        isReady = false;

        // dash animation
        animator.SetTrigger("HeavyAttackCharge");
    }

    /**
     * Starts dashing if the recorded direction is different from 0
     */
    public override void PerformedAction()
    {
        startTime = Time.time;
        chargeCor = StartCoroutine(ChargeHammer());
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
        float damage = Mathf.Clamp(minDamage + difTime, minDamage, maxDamage);
        

        // Calcolate position
        Vector3 pos = new Vector3(transform.position.x, 0, transform.position.y);

        // Create AOE
        float areaOfImpact = Mathf.Clamp(minArea + difTime, minArea, maxArea);

        // Create Collider
        Collider[] hitEnemies = Physics.OverlapSphere(pos, areaOfImpact);

        // Check for collision
        foreach (Collider e in hitEnemies) {
            if (e.CompareTag("Enemy")) {

                EnemyForm enemyForm = e.transform.GetComponent<EnemyForm>();

                if (enemyForm != null) {
                    enemyForm.TakeDamage(damage);
                }
            }
        }
    }

    private IEnumerator ChargeHammer()
	{
        yield return new WaitForSeconds(maxChargeTime);
        CancelAction();
	}
}
