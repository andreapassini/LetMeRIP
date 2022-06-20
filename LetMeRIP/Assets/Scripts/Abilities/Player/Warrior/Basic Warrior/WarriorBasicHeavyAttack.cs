using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WarriorBasicHeavyAttack : Ability
{
    [SerializeField]
    private Animator animator;
    private Rigidbody rb;
    private Transform attackPoint;

    private float attackRange = 1f;
    private readonly float time = 0.35f;
    private float currentTime;
    private float speed = 13f;
    private Vector3 direction;
    // prevents the cancel action to start too soon
    private bool isDashing = false;
    private float damage;

    private Coroutine damageCoroutine;
    private float tickRate = .1f;
    private GameObject vfx;
    private void Start()
    {
        cooldown = 3f;
        rb = GetComponent<Rigidbody>();
        vfx = Resources.Load<GameObject>("Particles/WarriorBasicHeavyAttack");

    }

    public override void Init(PlayerController characterController)
    {
        base.Init(characterController);
        attackPoint = transform.Find("AttackPoint");
        animator = GetComponentInChildren<Animator>(false);

        damage = 15 + characterController.stats.strength * 0.4f;
    }

    /**
     * Marks the ability as on-cooldown, registers the direction and disables the movement if the latter is different from 0
     */
    public override void StartedAction()
    {
        animator ??= GetComponentInChildren<Animator>(false);
        isReady = false;

        direction = attackPoint.forward;

        // you can't move while dashing
        if (!direction.Equals(Vector3.zero))
        {
            currentTime = time;
            isDashing = true;
        } // prevents unresponsive movement if the player tries to dash when standing and moving right after


        // dash animation
        animator.SetTrigger("HeavyAttack");
    }

    /**
     * Starts dashing if the recorded direction is different from 0
     */
    public override void PerformedAction()
    {
        Debug.Log("Dashing");
        if (isDashing)
        {
            //animator.SetTrigger("Dash");
            StartCoroutine(DashAction());
            StartCoroutine(Cooldown());
            damageCoroutine = StartCoroutine(Damage());
        }
        else
        {
            Debug.Log("Missing direction");
            isReady = true;
        }
    }

    /**
     * if the dash action has finished, ri-enables movement
     */
    public override void CancelAction()
    {
        if (!isDashing)
        {
            Debug.Log("Dash finished");
        }
    }

    /**
     * moves the player in the recorded direction for time seconds
     */
    private IEnumerator DashAction()
    {
        DisableActions();
        vfx ??= Resources.Load<GameObject>("Particles/WarriorBasicHeavyAttack");
        Destroy(Instantiate(vfx, transform), 3f);

        while (currentTime > 0)
        {
            if (Physics.Raycast(transform.position + direction * 0.1f, direction, out RaycastHit info, 50f))
            {
                if (info.collider.CompareTag("Obstacle") && (transform.position - info.transform.position).magnitude < 4f)
                {
                    isDashing = false;
                    EnableActions();

                    CancelAction();
                    yield break;
                }
            }
            currentTime -= Time.deltaTime;
            rb.MovePosition(transform.position + this.direction * speed * Time.deltaTime);
            yield return new WaitForFixedUpdate();
        }
        isDashing = false;

        EnableActions();
        StopCoroutine(damageCoroutine);
        CancelAction();
    }

    private IEnumerator Damage()
    {
        for (;;)
        {
            Utilities.SpawnHitSphere(attackRange, transform.position, 3f);
            Collider[] hitEnemies = Physics.OverlapSphere(attackPoint.position, attackRange);
            foreach (Collider enemyHit in hitEnemies)
            {
                if (enemyHit.CompareTag("Enemy"))
                {
                    EnemyForm eform = enemyHit.GetComponent<EnemyForm>();
                    eform.TakeDamage(damage);
                }
            }
            yield return new WaitForSeconds(tickRate);
        }
    }


}
