using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class ClericHeavyAttack : Ability
{
    [SerializeField]
    private Animator animator;
    private Rigidbody rb;
    private Transform attackPoint;
    private Transform lightDownPoint;

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
        lightDownPoint = transform.Find("LightDown");
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

        startTime = Time.time;
        //chargeCor = StartCoroutine(ChargeHammer());
        DisableMovement();
    }

    /**
     * Starts dashing if the recorded direction is different from 0
     */
    public override void PerformedAction()
    {
        
    }

    /**
     * if the dash action has finished, ri-enables movement
     */
    public override void CancelAction()
    {
        //StopCoroutine(chargeCor);
        animator.SetTrigger("HeavyAttackCast");
        //HammerDown(); Call this from the animation event

        EnableMovement();
        StartCoroutine(Cooldown());
    }

    public void HammerDown()
	{
        float difTime = Time.time - startTime;

        // Calculate damage
        float damage = Mathf.Clamp(minDamage + difTime, minDamage, maxDamage);

        // Spawn Particellar effects
        LightDown lightDown = GetComponentInChildren<LightDown>();
        PhotonNetwork.Instantiate("Prefabs/LightDown", lightDown.transform.position, lightDown.transform.rotation);

        // Calcolate position
        Vector3 pos = new Vector3(lightDown.transform.position.x, lightDown.transform.position.y, lightDown.transform.position.y);

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

 //   private IEnumerator ChargeHammer()
	//{
 //       yield return new WaitForSeconds(maxChargeTime);
 //       CancelAction();
	//}
}
