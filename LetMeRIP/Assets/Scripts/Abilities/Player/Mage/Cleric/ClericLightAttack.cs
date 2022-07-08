using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

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
        bulletPrefab = Resources.Load<GameObject>("Prefabs/LightSphere");
        cooldown = 1f;
        SPCost = 4f;
    }

    private void OnEnable()
    {
        ClericRebroadcastAnimEvent.lightAttack += Fire;
    }

    private void OnDisable()
    {
        ClericRebroadcastAnimEvent.lightAttack -= Fire;
    }

    public override void Init(PlayerController characterController)
    {
        base.Init(characterController);
        attackPoint = transform.Find("AttackPoint");
        rb = GetComponent<Rigidbody>();
        animator = GetComponentInChildren<Animator>(false);

        damage = 15 + characterController.stats.intelligence * 0.3f;
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

        DisableMovement();
    }

    /**
     * Starts dashing if the recorded direction is different from 0
     */
    public override void PerformedAction()
    {
    }

    public void Fire(Cleric c)
	{
        if (photonView.GetComponent<Cleric>() != c)
            return;

        GameObject bulletFired = Instantiate(bulletPrefab, attackPoint.position, attackPoint.rotation);

        // Fire Bullet
        bulletFired.GetComponent<LightSphere>().damage = damage;
        Rigidbody rbBullet = bulletFired.GetComponent<Rigidbody>();
        rbBullet.AddForce(attackPoint.forward * bulletForce, ForceMode.Impulse);

        RestEnable();
    }

    private void RestEnable()
	{
        isCasting = false;
        EnableMovement();
        StartCoroutine(Cooldown());

    }

    /**
     * if the dash action has finished, re-enables movement
     */
    public override void CancelAction()
    {
    }
}
