using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class mageBasicLightAttack : Ability
{
    [SerializeField]
    private Animator animator;
    private Rigidbody rb;
    private Transform attackPoint;

    private float attackRange = 1f;
    private readonly float time = 0.25f;
    private float currentTime;
    private float speed = 13f;
    private Vector3 direction;
    // prevents the cancel action to start too soon
    private bool isCasting = false;
    private float damage;

    private Coroutine damageCoroutine;
    private float tickRate = .1f;

    private GameObject bulletPrefab;

    [SerializeField]
    private float bulletForce = 15f;

    // Start is called before the first frame update
    void Start()
    {
        cooldown = 1.2f;
        rb = GetComponent<Rigidbody>();
    }

    public override void Init(PlayerController characterController)
    {
        base.Init(characterController);
        attackPoint = transform.Find("AttackPoint");
        animator = GetComponentInChildren<Animator>(false);

        damage = 10 + characterController.currentStats.strength * 0.2f;

        // Get the prefab
        bulletPrefab = Resources.Load("Prebas/Bullet") as GameObject;
    }

    /**
     * Marks the ability as on-cooldown, registers the direction and disables the movement if the latter is different from 0
     */
    public override void StartedAction()
    {
        animator ??= GetComponentInChildren<Animator>(false);
        isReady = false;

        direction = attackPoint.forward;

        // dash animation
        animator.SetTrigger("LightAttack");
    }

    /**
     * Starts dashing if the recorded direction is different from 0
     */
    public override void PerformedAction()
    {
        Debug.Log("Casting");
        // Fire Bullet
        GameObject bulletFired = Instantiate(bulletPrefab, attackPoint.position, attackPoint.rotation);

        bulletFired.layer = gameObject.layer;
        Rigidbody rbBullet = bulletFired.GetComponent<Rigidbody>();
        rbBullet.AddForce(attackPoint.forward * bulletForce, ForceMode.Impulse);

        StartCoroutine(Cooldown());
    }

    /**
     * if the dash action has finished, ri-enables movement
     */
    public override void CancelAction()
    {
        if (!isCasting) {
            Debug.Log("Casting complete");
        }
    }

}
