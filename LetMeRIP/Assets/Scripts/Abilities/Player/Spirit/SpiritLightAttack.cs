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
        bulletPrefab = Resources.Load<GameObject>("Prefabs/Juice/PlayerBulletJuice");
    }

    private void OnEnable()
    {
        SpiritFormRebroadcastAnimEvent.lightAttack += Cast;
        SpiritFormRebroadcastAnimEvent.lightAttackEnd += EndCast;
    }

    private void OnDisable()
    {
        SpiritFormRebroadcastAnimEvent.lightAttack -= Cast;
        SpiritFormRebroadcastAnimEvent.lightAttackEnd -= EndCast;
    }

    public override void Init(PlayerController characterController)
    {
        base.Init(characterController);
        animator = GetComponentInChildren<Animator>(false);
        attackPoint = transform.Find("AttackPoint");
        damage = 10 * characterController.stats.intelligence * 0.1f;
    }

    public override void StartedAction()
    {
        isReady = false;
        StartCoroutine(Cooldown());
        animator.SetTrigger("LightAttack");

        DisableMovement();
    }

    public void Cast(SpiritForm spiritForm)
    {
        if (!spiritForm.CharacterController.Equals(characterController)) return;
        
        Debug.Log($"APPARENTLY {name} IS MINE {spiritForm.photonView.IsMine}");
        bulletPrefab ??= Resources.Load<GameObject>("Prefabs/Juice/PlayerBulletJuice");


        GameObject bullet = Instantiate(bulletPrefab, attackPoint.position, attackPoint.rotation);
        bullet.GetComponent<Bullet>().damage = damage;
        Rigidbody bulletRb = bullet.GetComponent<Rigidbody>();
        bulletRb.AddForce(attackPoint.forward * bulletForce, ForceMode.Impulse);

        EnableActions();
        EnableMovement();
        characterController.lam.EnableLookAround();
    }


    public void EndCast(SpiritForm spiritForm)
    {
        if (!spiritForm.photonView.IsMine) return;
        Debug.Log($"APPARENTLY {name} IS MINE {spiritForm.photonView.IsMine}");

        EnableActions();
        EnableMovement();
        characterController.lam.EnableLookAround();
    }

}
