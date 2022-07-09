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

    private GameObject prefab;
    private GameObject chargePrefab;
    private GameObject refCharge;

    [SerializeField]
    private float bulletForce = 7f;

    [SerializeField]
    private float chargeTime = 1.5f;

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

        damage = 15 + characterController.stats.intelligence * 0.3f +
            characterController.stats.dexterity * 0.1f;
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

        isCasting = true;
        prefab = Resources.Load<GameObject>("Prefabs/BulletTrapassing");
        chargePrefab = Resources.Load<GameObject>("Particles/mageChargeHeavyAttack");

        refCharge = Instantiate(chargePrefab, attackPoint);
        Destroy(refCharge, chargeTime);

        StartCoroutine(CastAction());
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
    }

    private IEnumerator CastAction()
    {
        DisableMovement();
        yield return new WaitForSeconds(chargeTime);

        CastTempest();
    }

    private void CastTempest()
	{
        Destroy(refCharge);

        // Trigger Casting animation
        animator.SetTrigger("HeavyAttackCast");

        float offSet = 0f;
        float offsetWork = 0f;

        // Get the prefab
        prefab ??= Resources.Load<GameObject>("Prefabs/BulletTrapassing");
        chargePrefab ??= Resources.Load<GameObject>("Particles/mageChargeHeavyAttack");

        for (int i = 0; i < 5; i++) {
			// Calcolate angle
			offSet += (i * 0.5f);
			offsetWork = offSet;

			Vector3 v = new Vector3(attackPoint.position.x + (offsetWork/2), attackPoint.position.y, attackPoint.position.z);

			// Instantiate spheres
			GameObject bulletFired = Instantiate(prefab, v, attackPoint.rotation);

            bulletFired.GetComponent<Bullet>().damage = damage;
            bulletFired.transform.localScale = new Vector3(
                bulletFired.transform.localScale.x * 0.75f,
                bulletFired.transform.localScale.y * 0.75f,
                bulletFired.transform.localScale.z * 0.75f);

            Rigidbody rbBullet = bulletFired.GetComponent<Rigidbody>();
			rbBullet.AddForce(attackPoint.forward * bulletForce, ForceMode.Impulse);
        }

        RestEnable();

    }

    private void RestEnable()
	{
        EnableMovement();
        StartCoroutine(Cooldown());
    }

}
