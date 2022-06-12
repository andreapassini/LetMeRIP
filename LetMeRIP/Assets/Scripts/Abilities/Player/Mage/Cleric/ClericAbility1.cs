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

    private GameObject prefab;

    PlayerController p;

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

        minHeal = (float)(35 + characterController.stats.intelligence * 0.4f);

        maxHeal = (float)(50 + characterController.stats.intelligence * 0.8f);

        prefab = Resources.Load("Prebas/Pot") as GameObject;

        p = characterController;
    }

    /**
     * Marks the ability as on-cooldown, registers the direction and disables the movement if the latter is different from 0
     */
    public override void StartedAction()
    {
        animator ??= GetComponentInChildren<Animator>(false);
        isReady = false;

        // dash animation
        animator.SetTrigger("Ability1Charge");
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
        animator.SetTrigger("Ability1");

        PotDown();

        EnableActions();
        StartCoroutine(Cooldown());
    }

    public void PotDown()
    {
        float difTime = Time.time - startTime;

        // Calculate damage
        float heal = Mathf.Clamp(minHeal + difTime, minHeal, maxHeal);

        float dim = Mathf.Clamp(3 + difTime, 3, 7);

        // Calcolate position as Look at mouse 
        Vector3 v = GatherDirectionInput();

        GameObject pool = Instantiate(prefab, v, transform.rotation);

        pool.GetComponent<Pot>().Init(heal, dim);
    }

    private IEnumerator ChargeHealingPot()
    {
        yield return new WaitForSeconds(maxChargeTime);
        CancelAction();
    }

    public Vector3 GatherDirectionInput()
    {
        Ray ray = p.GetComponent<Camera>().ScreenPointToRay(playerInputActions.Player.LookAt.ReadValue<Vector2>());

        Vector3 direction = Physics.Raycast(ray, out var hitInfo, Mathf.Infinity, p.GetComponent<LookAtMouse>().groundMask)
            ? hitInfo.point - transform.position
            : Vector3.zero;
        direction.y = 0;
        return direction.normalized;
    }
}
