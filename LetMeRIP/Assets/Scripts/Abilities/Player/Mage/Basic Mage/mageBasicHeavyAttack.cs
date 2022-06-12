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

        StartCoroutine(CastAction());
    }

    /**
     * Starts dashing if the recorded direction is different from 0
     */
    public override void PerformedAction()
    {
        //Debug.Log("Casting");

        //if (isCasting) {
        //    //animator.SetTrigger("Dash");
            
        //} else {
        //    Debug.Log("Missing direction");
        //    isReady = true;
        //}
    }

    /**
     * if the dash action has finished, ri-enables movement
     */
    public override void CancelAction()
    {
        if (isCasting)
            return;

        EnableActions();
        StartCoroutine(Cooldown());
    }

    private IEnumerator CastAction()
    {
        DisableActions();
        yield return new WaitForSeconds(chargeTime);

        CastTempest();

        
        isCasting = false;

        CancelAction();
    }

    private void CastTempest()
	{
        // Trigger Casting animation
        animator.SetTrigger("HeavyAttackCast");

        float offSet = 0f;
        float offsetWork = 0f;

        // Get the prefab
        prefab = Resources.Load<GameObject>("Prefabs/BulletTrapassing");

		for (int i = 0; i < 5; i++) {
			// Calcolate angle
			offSet += (i * 0.5f);
			offsetWork = offSet;

			//         if (i%2 == 0) {
			//             offsetWork = offsetWork * -1;
			//}

			Vector3 v = new Vector3(attackPoint.position.x + (offsetWork/2), attackPoint.position.y, attackPoint.position.z);

			// Instantiate spheres
			GameObject bulletFired = Instantiate(prefab, v, attackPoint.rotation);

			bulletFired.GetComponent<BulletTrapassing>().damage = damage;
			bulletFired.layer = gameObject.layer;
			Rigidbody rbBullet = bulletFired.GetComponent<Rigidbody>();
			rbBullet.AddForce(attackPoint.forward * bulletForce, ForceMode.Impulse);

        }

    }

}
