using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class mageBasicAbility1 : Ability
{
    [SerializeField]
    private Animator animator;
    private Rigidbody rb;
    private Transform attackPoint;

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
    }

    private void OnEnable()
    {
        
    }

    private void OnDisable()
    {
        
    }

    public override void Init(PlayerController characterController)
    {
        base.Init(characterController);

        MageBasicRebroadcastAnimEvent.ability1 += CastBeam;

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

        Debug.Log("Casting Ability 1");

        CastBeam(p.GetComponent<MageBasic>());

        DisableActions();

        
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
        // Shoot
        EnableActions();
        isCasting = false;

        MageBasicRebroadcastAnimEvent.ability2 -= CastBeam;

        StartCoroutine(Cooldown());
    }

    private void CastBeam(MageBasic mage)
    {
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
        GameObject prefab = bulletPrefab = Resources.Load<GameObject>("Prefabs/Laser");

        Instantiate(prefab, attackPoint.position, attackPoint.rotation);

        CancelAction();

        // Shoot
        

        //if (this == mage) {
            
        //}
        

    }
}
