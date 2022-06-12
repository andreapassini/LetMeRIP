using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class mageBasicHeavyAttack : Ability
{
    [SerializeField]
    private Animator animator;
    private Rigidbody rb;
    private Transform attackPoint;

    private readonly float time = 0.25f;
    private Vector3 direction;
    // prevents the cancel action to start too soon
    private bool isCasting = false;
    private float damage;

    private GameObject bulletPrefab;

    [SerializeField]
    private float bulletForce = 15f;

    [SerializeField]
    private float chargeTime = 0.5f;

    void Start()
    {
        cooldown = 3f;
        rb = GetComponent<Rigidbody>();
    }

    public override void Init(PlayerController characterController)
    {
        base.Init(characterController);
        attackPoint = transform.Find("AttackPoint");
        animator = GetComponentInChildren<Animator>(false);

        damage = 15 + characterController.currentStats.intelligence * 0.3f +
            characterController.currentStats.dexterity * 0.1f;

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

        direction = attackPoint.forward;

        // charge casting animation
        animator.SetTrigger("HeavyAttackCharge");
    }

    /**
     * Starts dashing if the recorded direction is different from 0
     */
    public override void PerformedAction()
    {
        Debug.Log("Casting");

        if (isCasting) {
            //animator.SetTrigger("Dash");
            StartCoroutine(CastAction());
        } else {
            Debug.Log("Missing direction");
            isReady = true;
        }
    }

    /**
     * if the dash action has finished, ri-enables movement
     */
    public override void CancelAction()
    {

    }

    private IEnumerator CastAction()
    {
        DisableActions();
        yield return new WaitForSeconds(chargeTime);

        CastTempest();

        EnableActions();
        StartCoroutine(Cooldown());
        isCasting = false;
    }

    private void CastTempest()
	{
        // Trigger Casting animation
        animator.SetTrigger("HeavyAttackCast");

        float angle = 0f;
        float angleWork;

        for(int i=0; i<11; i++) 
        {
            // Calcolate angle
            angle += i * 2;
            angleWork = angle;

            if (i%2 < 1) {
                angleWork = angleWork * -1;
			}

            // Instantiate spheres
            GameObject bulletFired = Instantiate(bulletPrefab, attackPoint.position, attackPoint.rotation * Quaternion.Euler(0, angleWork, 0));

            bulletFired.GetComponent<BulletTrapassing>().damage = damage;
            bulletFired.layer = gameObject.layer;
            Rigidbody rbBullet = bulletFired.GetComponent<Rigidbody>();
            rbBullet.AddForce(attackPoint.forward * bulletForce, ForceMode.Impulse);
        }

    }

}
