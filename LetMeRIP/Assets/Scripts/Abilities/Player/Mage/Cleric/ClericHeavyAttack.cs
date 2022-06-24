using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class ClericHeavyAttack : Ability
{
    [SerializeField]
    private Animator animator;
    private Rigidbody rb;
    private Vector3 attackPoint;
    private Transform lightDownPoint;

    // prevents the cancel action to start too soon
    private bool isCasting = false;
    private float minDamage;
    private float maxDamage;

    [SerializeField]
    private float minArea = 2f;

    [SerializeField]
    private float maxArea = 4f;

    private float maxChargeTime = 2.5f;
    private Coroutine chargeCor;

    private float startTime;
    private GameObject vfx;
    private GameObject hammerPrefab;
    private GameObject hammerInstance;
    private Animator hammerAnimator;
    private GameObject hammerContainer;

    private float timeStep = 0.02f;
    private float sizeMultiplier = 0f;
    private Coroutine enlargeCoroutine;

    // Start is called before the first frame update

    private void OnEnable()
    {
        ClericRebroadcastAnimEvent.heavyAttack += HammerDown;
    }

    private void OnDisable()
    {
        ClericRebroadcastAnimEvent.heavyAttack -= HammerDown;
    }


    void Start()
    {
        //cooldown = 3.5f;
        //SPCost = 14f;

        cooldown = 0.1f;
        SPCost = 0;


        vfx = Resources.Load<GameObject>("Prefabs/ClericHeavyAttack");
        hammerPrefab = Resources.Load<GameObject>("Prefabs/LightHammer");
    }

    public override void Init(PlayerController characterController)
    {
        base.Init(characterController);
        attackPoint = new Vector3(0, 0, 2f);
        animator = GetComponentInChildren<Animator>(false);

        minDamage = (float)(15 + characterController.stats.intelligence * 0.2f +
            0.1 * characterController.stats.strength);

        maxDamage = (float)(40 + characterController.stats.intelligence * 0.3f +
            0.3 * characterController.stats.strength);
        hammerContainer = Instantiate(new GameObject(), transform);

    }

    /**
     * Marks the ability as on-cooldown, registers the direction and disables the movement if the latter is different from 0
     */
    public override void StartedAction()
    {
        hammerContainer.transform.localScale = Vector3.one;
        animator ??= GetComponentInChildren<Animator>(false);
        isReady = false;
        // spawn hammer with visual effect
        animator.SetTrigger("HeavyAttackCharge");

        startTime = Time.time;
        hammerInstance = Instantiate(hammerPrefab, transform);
        hammerInstance.transform.SetParent(hammerContainer.transform);
        hammerAnimator = hammerInstance.GetComponentInChildren<Animator>();

        enlargeCoroutine = StartCoroutine(Enlarge());
        DisableMovement();
    }

    private IEnumerator Enlarge()
    {
        for (; ; )
        {
            sizeMultiplier += timeStep;
            hammerContainer.transform.localScale = Vector3.one * (1 + sizeMultiplier);
            yield return new WaitForSeconds(timeStep);
        }
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
        StopCoroutine(enlargeCoroutine);

        animator.SetTrigger("HeavyAttackCast");
        hammerAnimator.SetTrigger("Release");

        EnableMovement();
        StartCoroutine(Cooldown());
    }

    public void HammerDown(Cleric cleric)
	{
        float difTime = Time.time - startTime;
        float damage = Mathf.Clamp(minDamage + difTime, minDamage, maxDamage);

        
        float areaOfImpact = Mathf.Lerp(minArea, maxArea, Mathf.Clamp(difTime, 0, maxChargeTime) / maxChargeTime);
        Utilities.SpawnHitSphere(areaOfImpact, new Vector3(0, 0, 2f * hammerContainer.transform.localScale.x), 3f);
        Collider[] hitEnemies = Physics.OverlapSphere(attackPoint, areaOfImpact);
        foreach (Collider other in hitEnemies) {
            if (other.CompareTag("Enemy") && other.TryGetComponent<EnemyForm>(out EnemyForm enemy)) {
                enemy.TakeDamage(damage);
            }
        }
    }

    private void OnDestroy()
    {
        Destroy(hammerContainer);
    }
}
