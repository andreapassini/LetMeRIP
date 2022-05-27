using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SampleHeavyAttack : Ability
{
    private LayerMask enemyLayer;
    private Animator animator;
    private Transform attackPoint;

    private GameObject bulletPrefab;
    private float bulletForce = 10f;

    // things that do not change within a change of forms
    private void Start()
    {
        cooldown = 1f;
        enemyLayer = LayerMask.NameToLayer("Enemy");
        bulletPrefab = Resources.Load<GameObject>("Prefabs/Bullet");
    }

    public override void Init()
    {
        animator = GetComponentInChildren<Animator>();
        attackPoint = transform.Find("AttackPoint");
    }

    public override void StartedAction()
    {
        isReady = false;
        animator.SetTrigger("HeavyAttack");
    }

    public override void PerformedAction()
    {
        // networking to be done here
        GameObject bullet = Instantiate(bulletPrefab, attackPoint.position, attackPoint.rotation);
        Rigidbody bulletRb = bullet.GetComponent<Rigidbody>();
        bulletRb.AddForce(attackPoint.forward * bulletForce, ForceMode.Impulse);

        StartCoroutine(Cooldown());
    }

    public override void CancelAction() { }
}
