using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ClericAbility2 : Ability
{
    [SerializeField]
    private Animator animator;
    private Rigidbody rb;
    private Transform attackPoint;

    // prevents the cancel action to start too soon
    private bool isCasting = false;

    private GameObject prefab;

    PlayerController p;

    // Start is called before the first frame update
    void Start()
    {
        cooldown = 12f;
        SPCost = 48f;
    }

    public override void Init(PlayerController characterController)
    {
        base.Init(characterController);
        attackPoint = transform.Find("AttackPoint");
        rb = GetComponent<Rigidbody>();
        animator = GetComponentInChildren<Animator>(false);

        prefab = Resources.Load("Prebas/BeaconOfHope") as GameObject;

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
        animator.SetTrigger("Ability2");

        DisableActions();
    }

    /**
     * Starts dashing if the recorded direction is different from 0
     */
    public override void PerformedAction()
    {
        // Calcolate position as Look at mouse 
        Vector3 v = GatherDirectionInput();

        GameObject beaconOfHope =  Instantiate(prefab, v, transform.rotation);
        beaconOfHope.GetComponent<BeaconOfHope>().Init(p.currentStats.intelligence);

        CancelAction();
    }

    /**
     * if the dash action has finished, ri-enables movement
     */
    public override void CancelAction()
    {
        EnableActions();
        StartCoroutine(Cooldown());
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
