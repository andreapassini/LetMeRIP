using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ClericLightAttack : Ability
{
    [SerializeField]
    private Animator animator;
    private Rigidbody rb;
    private Transform attackPoint;

    // prevents the cancel action to start too soon
    private bool isCasting = false;
    private float damage;

    private GameObject bulletPrefab;

    [SerializeField]
    private float bulletForce = 15f;

    // Start is called before the first frame update
    void Start()
    {
        cooldown = 1f;
        SPCost = 4f;
    }

    public override void Init(PlayerController characterController)
    {
        base.Init(characterController);
        attackPoint = transform.Find("AttackPoint");
        rb = GetComponent<Rigidbody>();
        animator = GetComponentInChildren<Animator>(false);

        damage = 15 + characterController.currentStats.intelligence * 0.3f;

        // Get the prefab
        bulletPrefab = Resources.Load("Prebas/LightSphere") as GameObject;
    }

    /**
     * Marks the ability as on-cooldown, registers the direction and disables the movement if the latter is different from 0
     */
    public override void StartedAction()
    {
        animator ??= GetComponentInChildren<Animator>(false);
        isReady = false;

        // dash animation
        animator.SetTrigger("LightAttack");
    }

    /**
     * Starts dashing if the recorded direction is different from 0
     */
    public override void PerformedAction()
    {
        Debug.Log("Casting");

        Fire();
    }

    public void Fire()
	{
        for(int i = 0; i<3; i++) {
            // Fire Bullet
            GameObject bulletFired = Instantiate(bulletPrefab, attackPoint.position, attackPoint.rotation);

            bulletFired.layer = gameObject.layer;
            bulletFired.GetComponent<LightSphere>().damage = damage;
            Rigidbody rbBullet = bulletFired.GetComponent<Rigidbody>();
            rbBullet.AddForce(attackPoint.forward * bulletForce, ForceMode.Impulse);
        }

        CancelAction();
    }

    /**
     * if the dash action has finished, ri-enables movement
     */
    public override void CancelAction()
    {
        StartCoroutine(Cooldown());
    }
}
