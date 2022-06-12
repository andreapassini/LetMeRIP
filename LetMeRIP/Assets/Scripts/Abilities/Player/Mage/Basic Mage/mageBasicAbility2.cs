using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class mageBasicAbility2 : Ability
{
    [SerializeField]
    private Animator animator;
    private Rigidbody rb;
    private Transform attackPoint;

    private readonly float time = 0.25f;
    // prevents the cancel action to start too soon
    private bool isCasting = false;

    private GameObject prefab;

    [SerializeField]
    private float castTime = 0.5f;

    private float startTime;

    // Start is called before the first frame update
    void Start()
    {
        cooldown = 7f;
        castTime = 4f;
        SPCost = 15f;
        rb = GetComponent<Rigidbody>();
    }

    public override void Init(PlayerController characterController)
    {
        base.Init(characterController);
        attackPoint = transform.Find("AttackPoint");
        animator = GetComponentInChildren<Animator>(false);
    }

    /**
     * Marks the ability as on-cooldown, registers the direction and disables the movement if the latter is different from 0
     */
    public override void StartedAction()
    {
        animator ??= GetComponentInChildren<Animator>(false);
        isReady = false;
        startTime = Time.time;

        // charge casting animation
        animator.SetTrigger("Ability2");
    }

    /**
     * Starts dashing if the recorded direction is different from 0
     */
    public override void PerformedAction()
    {
        Debug.Log("Casting");

        // Consume SG

        // Summon Healing Pool

        // Start Casting 
        StartCoroutine(CastingTime());

        DisableActions();
    }

    /**
     * if the dash action has finished, ri-enables movement
     */
    public override void CancelAction()
    {
        EnableActions();
        isCasting = false;

        StartCoroutine(Cooldown());
    }

    private IEnumerator CastingTime()
	{
        yield return new WaitForSeconds(castTime);

        CancelAction();
	}

    private void SummonHealingPool()
	{
        // Get the prefab
        prefab = Resources.Load("Prebas/HealingPoolVampire") as GameObject;

        Vector3 v = new Vector3(transform.position.x, 0, transform.position.z);

        GameObject healingPool = Instantiate(prefab, v,
            attackPoint.rotation);
    }
}
