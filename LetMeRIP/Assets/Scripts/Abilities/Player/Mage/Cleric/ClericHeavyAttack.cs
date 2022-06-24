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
    private float minArea = 1.8f;

    [SerializeField]
    private float maxArea = 2.45f;

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
    private float maxSizeMultiplier = .75f;
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

        minDamage = (float)(50 + characterController.stats.intelligence * 0.2f +
            0.1 * characterController.stats.strength);

        maxDamage = (float)(160 + characterController.stats.intelligence * 0.3f +
            0.3 * characterController.stats.strength);
        hammerContainer = Instantiate(new GameObject(), transform);

    }

    /**
     * Marks the ability as on-cooldown, registers the direction and disables the movement if the latter is different from 0
     */
    public override void StartedAction()
    {
        Debug.Log("Started");
        animator ??= GetComponentInChildren<Animator>(false);
        isReady = false;
        // spawn hammer with visual effect
        animator.SetTrigger("HeavyAttackCharge");

        startTime = Time.time;
        hammerInstance = Instantiate(hammerPrefab, transform);
        hammerInstance.transform.SetParent(hammerContainer.transform);
        hammerAnimator = hammerInstance.GetComponentInChildren<Animator>();

        hammerContainer.transform.localScale = Vector3.one;
        Debug.Log("enlarge coroutine started");
        enlargeCoroutine = StartCoroutine(Enlarge());
        DisableMovement();
    }

    private IEnumerator Enlarge()
    {
        sizeMultiplier = 0;
        float currentTime = 0f;
        while(currentTime <= maxChargeTime) 
        {
            currentTime += timeStep;
            sizeMultiplier += (maxSizeMultiplier / maxChargeTime )* timeStep;
            Debug.Log($"Size multiplier {sizeMultiplier}");
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
        if (enlargeCoroutine != null) 
            StopCoroutine(enlargeCoroutine);

        animator.SetTrigger("HeavyAttackCast");
        hammerAnimator.SetTrigger("Release");

        EnableMovement();
        StartCoroutine(Cooldown());
    }

    public void HammerDown(Cleric cleric)
	{
        float difTime = Time.time - startTime;
        float damage = Mathf.Lerp(minDamage, maxDamage, Mathf.Clamp(difTime, 0, maxChargeTime) / maxChargeTime);
        
        float areaOfImpact = Mathf.Lerp(minArea, maxArea, Mathf.Clamp(difTime, 0, maxChargeTime) / maxChargeTime);
        attackPoint = new Vector3(0, 0, 2f * hammerContainer.transform.localScale.x);
       
        Collider[] hitEnemies = Physics.OverlapSphere(hammerContainer.transform.TransformPoint(attackPoint), areaOfImpact);

        foreach (Collider other in hitEnemies) {
            Debug.Log($"Hit: {other.name}");
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
