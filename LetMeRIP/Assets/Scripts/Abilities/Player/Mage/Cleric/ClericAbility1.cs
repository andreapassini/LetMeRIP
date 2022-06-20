using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class ClericAbility1 : Ability
{
    [SerializeField]
    private Animator animator;
    private Rigidbody rb;
    private Transform attackPoint;

    // prevents the cancel action to start too soon
    private bool isCasting = false;
    private float minHeal;
    private float maxHeal;

    private float maxChargeTime = 3f;
    private Coroutine chargeCor;

    private float startTime;

    private GameObject bulletPrefab;

    PlayerController p;

    // Start is called before the first frame update
    void Start()
    {
        cooldown = 9f;
        SPCost = 36f;

        bulletPrefab = Resources.Load<GameObject>($"Prefabs/{nameof(ClericAbility1)}Bullet");
    }

    public override void Init(PlayerController characterController)
    {
        base.Init(characterController);
        attackPoint = transform.Find("AttackPoint");
        rb = GetComponent<Rigidbody>();
        animator = GetComponentInChildren<Animator>(false);

        minHeal = (float)(200 + characterController.stats.intelligence * 0.4f);

        maxHeal = (float)(500 + characterController.stats.intelligence * 0.8f);

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
        animator.SetTrigger("Ability1Charge");

        startTime = Time.time;
        //chargeCor = StartCoroutine(ChargeHealingPot());
        DisableMovement();
    }

    /**
     * Starts dashing if the recorded direction is different from 0
     */
    public override void PerformedAction()
    {
    }

    /**
     * if the dash action has finished, ri-enables movement
     */
    public override void CancelAction()
    {
        //StopCoroutine(chargeCor);
        animator.SetTrigger("Ability1Cast");
        EnableMovement();

        PotDown();
        StartCoroutine(Cooldown());
    }

    public void PotDown()
    {
        StartCoroutine(Cooldown());

        float difTime = Time.time - startTime;

        float heal = Mathf.Lerp(minHeal, maxHeal, Mathf.Clamp(difTime, 0, maxChargeTime) / maxChargeTime);

        bulletPrefab ??= Resources.Load<GameObject>($"Prefabs/{nameof(ClericAbility1)}Bullet"); // safety first :)

        StartCoroutine(HitWait(heal));
    }

    public Vector3 GatherDirectionInput()
    {
        Camera c = FindObjectOfType<Camera>();

        Ray ray = c.ScreenPointToRay(playerInputActions.Player.LookAt.ReadValue<Vector2>());

        Vector3 direction = Physics.Raycast(ray, out var hitInfo, Mathf.Infinity, p.GetComponent<LookAtMouse>().groundMask)
            ? hitInfo.point - transform.position
            : Vector3.zero;
        direction.y = 0;
        return direction.normalized;
    }

    private IEnumerator HitWait(float healAmount)
    {
        GameObject bulletInstance = Instantiate(bulletPrefab, transform.position, transform.rotation) as GameObject;
        yield return new WaitForSeconds(0.1f);
        bulletInstance.GetComponentInChildren<ClericAbility1Bullet>().Init(healAmount);
    }
}
