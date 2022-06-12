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

    // Start is called before the first frame update
    void Start()
    {
        cooldown = 5f;
        rb = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        MageBasicRebroadcastAnimEvent.ability1 += CastBeam;
    }

    private void OnDisable()
    {
        MageBasicRebroadcastAnimEvent.ability2 -= CastBeam;
    }

    public override void Init(PlayerController characterController)
    {
        base.Init(characterController);
        attackPoint = transform.Find("AttackPoint");
        animator = GetComponentInChildren<Animator>(false);

        minDamage = 15 + characterController.currentStats.intelligence * 0.1f +
            characterController.currentStats.strength * 0.1f;

        maxDamage = 35 + characterController.currentStats.intelligence * 0.3f +
            characterController.currentStats.strength * 0.2f;

        // Get the prefab
        bulletPrefab = Resources.Load("Prebas/BulletTrapassing") as GameObject;
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

        Debug.Log("Casting");

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

        StartCoroutine(Cooldown());
    }

    private void CastBeam(MageBasic mage)
    {
        if(this == mage) {
            float difTime = Time.time - startTime;

            // Trigger Casting animation
            animator.SetTrigger("Ability1Cast");

            // Damage
            float damage = Mathf.Clamp(minDamage + difTime, minDamage, maxDamage);

            float angle = 0f;
            float angleWork;

            for (int i = 0; i < 11; i++) {
                // Calcolate angle
                angle += i * 2;
                angleWork = angle;

                if (i % 2 < 1) {
                    angleWork = angleWork * -1;
                }

                // Instantiate spheres
                GameObject bulletFired = Instantiate(bulletPrefab, attackPoint.position, attackPoint.rotation * Quaternion.Euler(0, angleWork, 0));

                bulletFired.GetComponent<BulletTrapassing>().damage = damage;
                bulletFired.layer = gameObject.layer;
                Rigidbody rbBullet = bulletFired.GetComponent<Rigidbody>();
                rbBullet.AddForce(attackPoint.forward * bulletForce, ForceMode.Impulse);
            }

            CancelAction();
        }
        

    }
}
