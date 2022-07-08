using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

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
    PlayerController p;

    // Start is called before the first frame update
    void Start()
    {
        cooldown = 7f;
        SPCost = 15f;
        rb = GetComponent<Rigidbody>();
    }

    public override void Init(PlayerController characterController)
    {
        base.Init(characterController);
        attackPoint = transform.Find("AttackPoint");
        animator = GetComponentInChildren<Animator>(false);

        p = characterController;
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

        prefab = Resources.Load<GameObject>("Prefabs/HealingPoolVampire");

        DisableMovement();

        // Summon Healing Pool
        SummonHealingPool();
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
    }


    private void SummonHealingPool()
	{
        //Vector3 castingPos = GatherDirectionInput();
        Vector3 castingPos = new Vector3(
            transform.position.x,
            transform.position.y - 0.5f,
            transform.position.z);

        prefab ??= Resources.Load<GameObject>("Prefabs/HealingPoolVampire");
        
        GameObject healingPool = Instantiate(prefab, castingPos, Quaternion.identity);

        healingPool.GetComponent<HealingPoolVampire>().Init();

        RestEnable();
    }

    private void RestEnable()
	{
        EnableMovement();
        EnableActions();
        isCasting = false;

        StartCoroutine(Cooldown());
    }

    public Vector3 GatherDirectionInput()
    {
        Camera c = FindObjectOfType<Camera>();

        Ray ray = Camera.main.ScreenPointToRay(playerInputActions.Player.LookAt.ReadValue<Vector2>());

        Vector3 position = Physics.Raycast(ray, out var hitInfo, Mathf.Infinity, p.GetComponent<LookAtMouse>().groundMask)
            ? hitInfo.point
            : Vector3.zero;
        position.y = transform.position.y;
        return position;
    }
}
