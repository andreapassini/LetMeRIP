using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class mageBasicLightAttack : Ability
{
    [SerializeField]
    private Animator animator;
    private Rigidbody rb;
    private Transform attackPoint;

    // prevents the cancel action to start too soon
    private bool isCasting = false;
    private float damage;

    private Coroutine damageCoroutine;
    private float tickRate = .1f;

    private GameObject bulletPrefab;

    [SerializeField]
    private float bulletForce = 15f;

    private PlayerController p;

    // Start is called before the first frame update
    void Start()
    {
        cooldown = 1.2f;
        rb = GetComponent<Rigidbody>();
        bulletPrefab = Resources.Load<GameObject>("Prefabs/Juice/PlayerBulletJuice");
    }

	private void OnEnable()
	{
        MageBasicRebroadcastAnimEvent.lightAttack += Cast;
	}

	private void OnDisable()
	{
        MageBasicRebroadcastAnimEvent.lightAttack -= Cast;
    }

	public override void Init(PlayerController characterController)
    {
        base.Init(characterController);

        attackPoint = transform.Find("AttackPoint");
        animator = GetComponentInChildren<Animator>(false);

        damage = 10 + characterController.stats.strength * 0.2f;

        
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
        animator.SetTrigger("LightAttack");

        isCasting = true;
        StartCoroutine(Cooldown());

        DisableMovement();
    }

    /**
     * Starts dashing if the recorded direction is different from 0
     */
    public override void PerformedAction()
    {
        
    }

    public void Cast(MageBasic m)
	{
        if(p == m.GetComponent<PlayerController>()) {
            Debug.Log("Casting");
            // Get the prefab
            GameObject bulletFired = Instantiate(bulletPrefab, attackPoint.position, attackPoint.rotation);

            // Fire Bullet
            bulletFired.GetComponent<Bullet>().damage = damage;
            bulletFired.layer = gameObject.layer;
            Rigidbody rbBullet = bulletFired.GetComponent<Rigidbody>();
            rbBullet.AddForce(attackPoint.forward * bulletForce, ForceMode.Impulse);

            // isCasting = false;

            EnableActions();
            EnableMovement();
        }

    }

    private void RestEnable()
	{
        
        EnableMovement();

    }

    /**
     * if the dash action has finished, ri-enables movement
     */
    public override void CancelAction()
    {
        
    }

}
