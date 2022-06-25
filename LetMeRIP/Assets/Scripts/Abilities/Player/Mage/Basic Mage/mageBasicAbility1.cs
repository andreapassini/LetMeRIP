using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class mageBasicAbility1 : Ability
{
    [SerializeField]
    private Animator animator;
    private Rigidbody rb;
    private Transform attackPoint;

    private GameObject prefab;
    private GameObject prefabCharge;
    private GameObject refPrefabChargeInst;

    // prevents the cancel action to start too soon
    private bool isCasting = false;
    private float minDamage;
    private float maxDamage;
    //private float tickRate = .1f;

    private GameObject bulletPrefab;

    [SerializeField]
    private float bulletForce = 15f;

    [SerializeField]
    private float chargeTime = 0.5f;

    private float startTime;

    private PlayerController p;

    // Start is called before the first frame update
    void Start()
    {
        cooldown = 5f;
        rb = GetComponent<Rigidbody>();

        prefab = Resources.Load<GameObject>("Particles/LaserCore");
        prefabCharge = Resources.Load<GameObject>("Particles/mageCharge");
    }

    private void OnEnable()
    {
        //MageBasicRebroadcastAnimEvent.ability1 += CastBeam;
    }

    private void OnDisable()
    {
        //MageBasicRebroadcastAnimEvent.ability2 -= CastBeam;
    }

    public override void Init(PlayerController characterController)
    {
        base.Init(characterController);

        

        attackPoint = transform.Find("AttackPoint");
        animator = GetComponentInChildren<Animator>(false);

        minDamage = 15 + characterController.stats.intelligence * 0.1f +
            characterController.stats.strength * 0.1f;

        maxDamage = 35 + characterController.stats.intelligence * 0.3f +
            characterController.stats.strength * 0.2f;

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
        animator.SetTrigger("Ability1Charge");

        prefabCharge = Resources.Load<GameObject>("Particles/mageCharge");
        refPrefabChargeInst = Instantiate(prefabCharge, transform);

        DisableMovement();
        StartCoroutine(Cooldown());
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
        CastBeam(p.GetComponent<MageBasic>());

        // Shoot
        EnableMovement();
        EnableActions();
        isCasting = false;
    }

    private void CastBeam(MageBasic mage)
    {
        Destroy(refPrefabChargeInst);
        float difTime = Time.time - startTime;

        // Trigger Casting animation
        animator.SetTrigger("Ability1Cast");

        // Damage
        float damage = Mathf.Clamp(minDamage + difTime, minDamage, maxDamage);

        RaycastHit hit;
        // Cast beam
        if (Physics.Raycast(attackPoint.position, attackPoint.forward, out hit, 1000)) {
            if (hit.transform.tag == "Enemy") {
                hit.transform.GetComponent<EnemyForm>().TakeDamage(damage);
            }
        }

        // Instantiate Laser
        prefab ??= Resources.Load<GameObject>("Particles/LaserCore");

        Instantiate(prefab, attackPoint.position, attackPoint.rotation);

    }

}
