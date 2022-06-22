using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

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
    private GameObject beaconOfHopePrefab;

    PlayerInputActions pactions;
    // Start is called before the first frame update
    void Start()
    {
        cooldown = 12f;
        SPCost = 48f;
        beaconOfHopePrefab = Resources.Load<GameObject>("Prefabs/BeaconOfHope");
        playerInputActions.Player.LookAt.Enable();
    }

    public override void Init(PlayerController characterController)
    {
        base.Init(characterController);
        attackPoint = transform.Find("AttackPoint");
        rb = GetComponent<Rigidbody>();
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

        // dash animation
        animator.SetTrigger("Ability2");

        DisableMovement();
        Cast();
    }

    /**
     * Starts dashing if the recorded direction is different from 0
     */
    public override void PerformedAction()
    {
    }

    public void Cast()
	{
        Vector2 mousePos = playerInputActions.Player.LookAt.ReadValue<Vector2>();
        Vector3 castingPos = GatherDirectionInput();
        Debug.Log($"Casting position: {castingPos}");
        beaconOfHopePrefab ??= Resources.Load<GameObject>("Prefabs/BeaconOfHope");
        GameObject beaconOfHopeInstance = Instantiate(beaconOfHopePrefab, castingPos, Quaternion.identity) ;
        beaconOfHopeInstance.GetComponent<BeaconOfHope>().Init(p.stats.intelligence);

        RestEnable();
    }

    private void RestEnable()
	{
        EnableMovement();
        EnableActions();
        StartCoroutine(Cooldown());
    }

    /**
     * if the dash action has finished, ri-enables movement
     */
    public override void CancelAction()
    {
    }


    //private void Update()
    //{
    //    Debug.Log(Camera.main.ScreenPointToRay(playerInputActions.Player.LookAt.ReadValue<Vector2>()));
    //}

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
