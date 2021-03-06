using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BerserkerAbility2 : Ability
{
    [SerializeField]
    private Animator animator;
    private Rigidbody rb;
    private Transform attackPoint;

    private float attackRange = 3f;
    private readonly float leapTime = .5f;
    private float speed = 15f;
    private float currentTime;
    // prevents the cancel action to start too soon
    private float damage;

    private float slowFactor = 0.3f;
    private float slowDuration = 2f;

    private Vector3 direction;
    private bool isDashing = false;

    private GameObject vfx;

    private void Start()
    {
        cooldown = 0f;
        SPCost = 40f;

        rb = GetComponent<Rigidbody>();
        vfx = Resources.Load<GameObject>("Particles/BerserkerAbility2");
    }

    private void OnEnable()
    {
        BerserkRebroadcastAnimEvent.ability2 += PerformedAction;
        BerserkRebroadcastAnimEvent.ability2End += CancelAction;
    }

    private void OnDisable()
    {
        BerserkRebroadcastAnimEvent.ability2 -= PerformedAction;
        BerserkRebroadcastAnimEvent.ability2End -= CancelAction;
    }

    public void PerformedAction(Berserker b)
    {
        if (characterController == b.GetComponent<PlayerController>())
        {
            if (!isDashing)
            {
                Hit();
                Debug.Log("Dash finished");
                
                characterController.lam.EnableLookAround();
                playerInputActions.Player.Movement.Enable(); // you can't move while dashing
            }
        }
    }

    public void CancelAction(Berserker b)
    {
        if (characterController == b.GetComponent<PlayerController>())
        {
            EnableMovement();
            EnableActions();
        }
    }

    public override void Init(PlayerController characterController)
    {
        base.Init(characterController);
        attackPoint = transform.Find("AttackPoint");
        animator = GetComponentInChildren<Animator>(false);

        damage = 40 + characterController.stats.strength * 0.7f;
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
            //playerInputActions.Player.Movement.Disable();
            //characterController.lam.DisableLookAround();
            //DisableMovement();
            //DisableActions();

            currentTime = leapTime;
            isDashing = true;
        } // prevents unresponsive movement if the player tries to dash when standing and moving right after


        // dash animation
        animator.SetTrigger("Ability2");
        StartCoroutine(Cooldown());

        if (isDashing)
        {
            //animator.SetTrigger("Dash");
            StartCoroutine(DashAction());
        }
        else
        {
            Debug.Log("Missing direction");
            isReady = true;
        }
    }

    /**
     * Starts dashing if the recorded direction is different from 0
     */
    public override void PerformedAction()
    {
        //Debug.Log("Dashing");
        //if (isDashing)
        //{
        //    //animator.SetTrigger("Dash");
        //    StartCoroutine(DashAction());
        //}
        //else
        //{
        //    Debug.Log("Missing direction");
        //    isReady = true;
        //}
    }

    /**
     * if the dash action has finished, ri-enables movement
     */
    public override void CancelAction()
    {
        //if (!isDashing)
        //{
        //    Hit();
        //    Debug.Log("Dash finished");
        //    EnableMovement();
        //    EnableActions();
        //    characterController.lam.EnableLookAround();
        //    playerInputActions.Player.Movement.Enable(); // you can't move while dashing
        //}
    }

    /**
     * moves the player in the recorded direction for time seconds
     */
    private IEnumerator DashAction()
    {
        characterController.lam.DisableLookAround();
        DisableMovement();
        //DisableActions();

        while (currentTime > 0)
        {
            if (Physics.Raycast(transform.position + direction * 0.1f, direction, out RaycastHit info, 50f))
            {
                if (info.collider.CompareTag("Obstacle") && (transform.position - info.transform.position).magnitude < 4f)
                {
                    isDashing = false;
                    EnableMovement();
                    EnableActions();
                    characterController.lam.EnableLookAround();

                    CancelAction();
                    yield break;
                }
            }
            currentTime -= Time.deltaTime;
            rb.MovePosition(transform.position + this.direction * speed * Time.deltaTime);
            yield return new WaitForFixedUpdate();
        }

        EnableMovement();
        EnableActions();
        characterController.lam.EnableLookAround();

        isDashing = false;
        //CancelAction();
    }

    private void Hit()
    {
        vfx ??= Resources.Load<GameObject>("Particles/BerserkerAbility2");
        Destroy(Instantiate(vfx, transform.position - new Vector3(0, 0.5f, 0), Quaternion.identity), 2f);

        //Utilities.SpawnHitSphere(attackRange, transform.position, 3f);

        Collider[] hitEnemies = Physics.OverlapSphere(attackPoint.position, attackRange);
        foreach (Collider enemyHit in hitEnemies)
        {
            if (enemyHit.CompareTag("Enemy"))
            {
                EnemyForm eform = enemyHit.GetComponent<EnemyForm>();
                eform.TakeDamage(damage);

                SlowEE slow = eform.gameObject.AddComponent<SlowEE>();
                slow.Duration = slowDuration;
                slow.SlowFactor = slowFactor;
                slow.StartEffect();
            }
        }

        EnableMovement();
        EnableActions();
        characterController.lam.EnableLookAround();
    }
}
