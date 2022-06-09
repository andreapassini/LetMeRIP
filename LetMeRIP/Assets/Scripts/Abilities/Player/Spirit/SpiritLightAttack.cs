using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpiritLightAttack : Ability
{
    private Animator animator;
    private Transform attackPoint;

    private GameObject bulletPrefab;
    private float bulletForce = 10f;
    private float damage;

    // things that do not change within a change of forms
    private void Start()
    {
        cooldown = .8f;
        bulletPrefab = Resources.Load<GameObject>("Prefabs/Bullet");
    }

    public override void Init(PlayerController characterController)
    {
        base.Init(characterController);
        animator = GetComponentInChildren<Animator>(false);
        attackPoint = transform.Find("AttackPoint");
        damage = 10 * characterController.spiritStats.intelligence * 0.1f;
    }

    public override void StartedAction()
    {
        isReady = false;
        //animator.SetTrigger("LightAttack");
    }

    public override void PerformedAction()
    {
        GameObject bullet = Instantiate(bulletPrefab, attackPoint.position, attackPoint.rotation);
        bullet.GetComponent<Bullet>().damage = damage;
        Rigidbody bulletRb = bullet.GetComponent<Rigidbody>();
        bulletRb.AddForce(attackPoint.forward * bulletForce, ForceMode.Impulse);

        StartCoroutine(Cooldown());
    }

    public override void CancelAction() { }

}
