using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class Boss : EnemyForm
{
    public static event Action<EnemyForm> OnEnemyLightAttack1Phase1;
    public static event Action<EnemyForm> OnEnemyLightAttack2Phase1;
    public static event Action<EnemyForm> OnEnemyHeavyAttackPhase1;

    public static event Action<EnemyForm> OnEnemyLightAttack1Phase3;
    public static event Action<EnemyForm> OnEnemyLightAttack2Phase3;
    public static event Action<EnemyForm> OnEnemyHeavyAttackPhase3;

    private FSM fsmOverlay;

    private FSM fsmPhase1;
    private FSM fsmPhase2;
    private FSM fsmPhase3;

    private DecisionTree dt_attackPhase1;
    private DecisionTree dt_attackPhase3;

    public EnemyAbility dashForward;
    public EnemyAbility heavyAttack;
    public EnemyAbility lightAttack1;
    public EnemyAbility lightAttack2;
    public EnemyAbility summonTentacles;
    public EnemyAbility fall;
    public EnemyAbility summonMinions;
    public EnemyAbility createVulneableSign;
    public EnemyAbility heavyAttackPhase3;
    public EnemyAbility lightAttack1Phase3;
    public EnemyAbility lightAttack2Phase3;
    public EnemyAbility riseUp;

    [SerializeField] private string targetTag = "Player";

    public float runningFotTooLongCooldown = 5f;
    public float cooldownActionOverTime = 5f;

    public bool lateStart = false;

    private int woundCount = 0;
    private bool signBroken = false;
    private bool runningForTooLong = false;
    private bool cooldownActionOver = false;
    private bool foundNewTarget = false;
    private bool isHAinCooldown = false;
    private bool isLA1Cooldown = false;
    private bool isCooldwonOver = false;
    private bool isInPhase2 = false;

    private float woundLevel;

    // Start is called before the first frame update
    void Start()
    {
        Init(); 

        // Gather Stats
        health = enemyStats.maxHealth;
        woundLevel = enemyStats.maxHealth / 7;

        rb = transform.GetComponent<Rigidbody>();

        animator = transform.GetComponent<Animator>();

        navMeshAgent = transform.GetComponent<NavMeshAgent>();

        reactionReference = AiFrameRate;

        if (lateStart) {
            StartCoroutine(LateStart());
        } else {
            targets = GameObject.FindGameObjectsWithTag(targetTag);
            target = targets[0].transform;
        }

		#region FSM Phase Overlay
		FSMState phase1 = new FSMState();
        phase1.stayActions.Add(RunFSMBossPhase1);

        FSMState phase2 = new FSMState();
        phase2.stayActions.Add(RunFSMBossPhase2);

        FSMState phase3 = new FSMState();
        phase3.stayActions.Add(RunFSMBossPhase3);

        FSMTransition t1 = new FSMTransition(After3WoundRecevied);
        FSMTransition t2 = new FSMTransition(BrokenSign);
        FSMTransition t3 = new FSMTransition(HealthUnder50Perc);

        // Phase 1 to Phase 2
        phase1.AddTransition(t1, phase2);

        // Phase 2 to 1
        phase2.AddTransition(t2, phase1);

        // Phase 2 to 3
        phase2.AddTransition(t3, phase3);

        fsmOverlay = new FSM(phase1);

        #endregion

        #region Phase 1
        FSMState searchPhase1 = new FSMState();
        searchPhase1.stayActions.Add(Search);

        FSMState chasePhase1 = new FSMState();
        chasePhase1.stayActions.Add(Chase);

        FSMState attackPhase1 = new FSMState();
        attackPhase1.stayActions.Add(RunAttackTree);

        FSMState repositionPhase1 = new FSMState();
        repositionPhase1.stayActions.Add(SearchNewTarget);
        repositionPhase1.exitActions.Add(DashForward);

        FSMTransition t1Phase1 = new FSMTransition(TargetFound);
        FSMTransition t2Phase1 = new FSMTransition(TargetNotFound);
        FSMTransition t3Phase1 = new FSMTransition(InRange);
        FSMTransition t4Phase1 = new FSMTransition(TargetNotInRange);
        FSMTransition t5Phase1 = new FSMTransition(RunningForTooLong);
        FSMTransition t6Phase1 = new FSMTransition(Wound);
        FSMTransition t7Phase1 = new FSMTransition(FoundNewTarget);
        FSMTransition t8Phase1 = new FSMTransition(CooldownActionOver);

        searchPhase1.AddTransition(t1Phase1, chasePhase1);

        chasePhase1.AddTransition(t2Phase1, searchPhase1);
        chasePhase1.AddTransition(t3Phase1, attackPhase1);
        chasePhase1.AddTransition(t5Phase1, repositionPhase1);

        attackPhase1.AddTransition(t6Phase1, repositionPhase1);
        attackPhase1.AddTransition(t4Phase1, chasePhase1);

        repositionPhase1.AddTransition(t7Phase1, attackPhase1);
        repositionPhase1.AddTransition(t8Phase1, searchPhase1);

        fsmPhase1 = new FSM(searchPhase1);
        #endregion

        #region DT Attack Phase 1
        DTAction heavyAttack = new DTAction(HeavyAttack);
        DTAction lightAttack1 = new DTAction(LightAttack1);
        DTAction lightAttack2 = new DTAction(LightAttack2);

        DTDecision d1AttackPhase1 = new DTDecision(IsHAPhase1inCooldown);
        DTDecision d2AttackPhase1 = new DTDecision(IsLA1Phase1inCooldown);

        d1AttackPhase1.AddLink(false, heavyAttack);
        d1AttackPhase1.AddLink(true, d2AttackPhase1);
        d2AttackPhase1.AddLink(false, lightAttack1);
        d2AttackPhase1.AddLink(true, lightAttack2);

        dt_attackPhase1 = new DecisionTree(d1AttackPhase1);
        #endregion

        #region Phase 2
        // THE DOWN PHASE
        FSMState idlePhase2 = new FSMState();
        idlePhase2.enterActions.Add(Fall);
        idlePhase2.enterActions.Add(SummonTentacles);
        idlePhase2.enterActions.Add(SummonMinions);
        idlePhase2.enterActions.Add(CreateVulnerabilitySign);
        idlePhase2.exitActions.Add(RiseUp);

        fsmPhase2 = new FSM(idlePhase2);
        #endregion

        #region Phase 3
        FSMState searchPhase3 = new FSMState();
        searchPhase3.stayActions.Add(Search);

        FSMState chasePhase3 = new FSMState();
        chasePhase3.stayActions.Add(Chase);

        FSMState attackPhase3 = new FSMState();
        attackPhase3.stayActions.Add(RunAttackTree2);

        FSMState repositionPhase3 = new FSMState();
        repositionPhase3.enterActions.Add(SummonMinions);
        repositionPhase3.stayActions.Add(SearchNewTarget);
        repositionPhase3.exitActions.Add(DashForward);

        FSMTransition t1Phase3 = new FSMTransition(TargetFound);
        FSMTransition t2Phase3 = new FSMTransition(TargetNotFound);
        FSMTransition t3Phase3 = new FSMTransition(InRange);
        FSMTransition t4Phase3 = new FSMTransition(TargetNotInRange);
        FSMTransition t5Phase3 = new FSMTransition(RunningForTooLong);
        FSMTransition t6Phase3 = new FSMTransition(CooldownOver);
        FSMTransition t7Phase3 = new FSMTransition(FoundNewTarget);
        FSMTransition t8Phase3 = new FSMTransition(CooldownActionOver);

        searchPhase3.AddTransition(t1Phase3, chasePhase3);

        chasePhase3.AddTransition(t2Phase3, searchPhase3);
        chasePhase3.AddTransition(t3Phase3, attackPhase3);
        chasePhase3.AddTransition(t5Phase3, repositionPhase3);

        attackPhase3.AddTransition(t6Phase3, repositionPhase3);
        attackPhase3.AddTransition(t4Phase3, chasePhase3);

        repositionPhase3.AddTransition(t7Phase3, attackPhase3);
        repositionPhase3.AddTransition(t8Phase3, searchPhase3);

        fsmPhase3 = new FSM(searchPhase3);
        #endregion

        #region DT Attack 2
        DTAction heavyAttackPhase3 = new DTAction(HeavyAttackPhase3);
        DTAction lightAttack1Phase3 = new DTAction(LightAttack1Phase3);
        DTAction lightAttack2Phase3 = new DTAction(LightAttack2Phase3);

        DTDecision d1AttackPhase3 = new DTDecision(IsHAPhase1inCooldown);
        DTDecision d2AttackPhase3 = new DTDecision(IsLA1Phase1inCooldown);

        d1AttackPhase3.AddLink(false, heavyAttackPhase3);
        d1AttackPhase3.AddLink(true, d2AttackPhase3);
        d2AttackPhase3.AddLink(false, lightAttack1Phase3);
        d2AttackPhase3.AddLink(true, lightAttack2Phase3);

        dt_attackPhase3 = new DecisionTree(d1AttackPhase3);
        #endregion

        StartCoroutine(PatrolOverlay());
    }

	private void OnEnable()
	{
        Sign.OnSignBroken += OnBrokeSign;
	}

	private void OnDisable()
	{
        Sign.OnSignBroken -= OnBrokeSign;
    }

    #region Conditions
    public bool CooldownOver()
    {
        if (!isCooldwonOver)
        {
            isCooldwonOver = true;
            StartCoroutine(RepositionCooldown());
            return false;
        }

        return true;
    }

    public object IsHAPhase1inCooldown(object o)
    {
        if (!isHAinCooldown)
        {
            isHAinCooldown = true;
            StartCoroutine(HACooldown());
            return false;
        }

        return true;
    }

    public object IsLA1Phase1inCooldown(object o)
    {
        if (!isLA1Cooldown)
        {
            isLA1Cooldown = true;
            StartCoroutine(LA1Cooldown());
            return false;
        }

        return true;
    }

    public object IsHAPhase3inCooldown(object o)
    {
        if (!isHAinCooldown) {
            isHAinCooldown = true;
            StartCoroutine(HACooldown());
            return false;
        }

        return true;
    }

    public object IsLA1Phase3inCooldown(object o)
    {
        if (!isLA1Cooldown) {
            isLA1Cooldown = true;
            StartCoroutine(LA1Cooldown());
            return false;
        }

        return true;
    }

    public bool After3WoundRecevied()
	{
        if(woundCount >= 3) {
            woundCount = 0;
            isInPhase2 = true;
            return true;
		}
        return false;
	}

    public bool BrokenSign()
	{
		if (signBroken) {
            signBroken = false;
            return true;
		}

        return false;
	}

    public bool HealthUnder50Perc()
	{
        if(health < (enemyStats.maxHealth / 2)) {
            isInPhase2 = false;
            return true;
		}

        return false;
	}

    public bool TargetFound()
	{
        Vector3 ray = target.position - transform.position;
        RaycastHit hit;
        if (Physics.Raycast(transform.position, ray, out hit, Mathf.Infinity, ~whatRayHit)) {
            if (hit.transform == target) {
                return true;
            }
        }

        return false;
    }

    public bool TargetNotFound()
	{
        return !TargetFound();
	}

    public bool InRange()
	{
        float distance = Vector3.Distance(transform.position, target.position);
        if (distance <= attackRange) {
            return true;
        }
        return false;
    }

    public bool TargetNotInRange()
	{
        return !InRange();
	}

    public bool RunningForTooLong()
	{
		if (runningForTooLong) {
            runningForTooLong = false;
            return true;
        }
        return false;
	}

    public bool Wound()
	{
        if(health <= woundLevel) {
            woundLevel = health - (enemyStats.maxHealth / 7);
            woundCount++;
            return true;
		}
        return false;
	}

    public bool CooldownActionOver()
	{
        // Remember to start coroutine inside Reposition
		if (cooldownActionOver) {
            cooldownActionOver = false;
            return true;
		}

        return false;
	}

    public bool FoundNewTarget()
	{
		if (foundNewTarget) {
            foundNewTarget = false;
            return true;
		}

        return false;
	}
    #endregion

    #region Actions

    public void RiseUp()
	{
        riseUp.StartAbility(this);
	}

    public object HeavyAttackPhase3(object o)
    {
        heavyAttackPhase3.StartAbility(this);
        return null;
    }

    private object LightAttack1Phase3(object o)
    {
        lightAttack1Phase3.StartAbility(this);
        return null;
    }

    private object LightAttack2Phase3(object o)
    {
        lightAttack2Phase3.StartAbility(this);
        return null;
    }

    public void CreateVulnerabilitySign()
    {
        if(isInPhase2)
            createVulneableSign.StartAbility(this);
    }

    public void SummonMinions()
    {
        if(isInPhase2)
            summonMinions.StartAbility(this);
    }

    public void Fall()
    {
        animator.SetFloat("speed", 0);
        fall.StartAbility(this);
    }

    public void SummonTentacles()
    {
        if(isInPhase2)
            summonTentacles.StartAbility(this);
    }

    private object LightAttack2(object o)
    {
        lightAttack2.StartAbility(this);
        return null;
    }

    private object LightAttack1(object o)
    {
        lightAttack1.StartAbility(this);
        return null;
    }

    public object HeavyAttack(object o)
    {
        heavyAttack.StartAbility(this);
        return null;
    }

	public void RunFSMBossPhase1()
	{
        StartCoroutine(PatrolPhase1());
	}

    public void RunFSMBossPhase2()
    {
        StartCoroutine(PatrolPhase2());
    }

    public void RunFSMBossPhase3()
    {
        StartCoroutine(PatrolPhase3());
    }

    public void Search()
	{
        searchAction.StartAbility(this);
        animator.SetFloat("speed", navMeshAgent.velocity.magnitude);
    }

    public void Chase()
    {
        chaseAction.StartAbility(this);
        animator.SetFloat("speed", navMeshAgent.velocity.magnitude);
    }

    public void DashForward()
	{
        dashForward.StartAbility(this);
        animator.SetFloat("speed", navMeshAgent.velocity.magnitude);
    }

    public void RunAttackTree()
	{
        StartCoroutine(PatrolAttackTree());
	}

    public void RunAttackTree2()
    {
        StartCoroutine(PatrolAttackTree2());
    }

    public void SearchNewTarget()
	{
		while (true) {
			Transform newTarget = targets[UnityEngine.Random.Range(0, targets.Length - 1)].transform;

			Vector3 ray = newTarget.position - transform.position;
			RaycastHit hit;
			if (Physics.Raycast(transform.position, ray, out hit, whatRayHit)) {
				if (hit.transform == newTarget && newTarget != target) {
					target = newTarget;
                    foundNewTarget = true;
				}
			}
		}
	}


    #endregion

    #region Coroutines
    public IEnumerator PatrolAttackTree2()
    {
        while (true)
        {
            navMeshAgent.speed = enemyStats.swiftness;
            dt_attackPhase3.walk();
            yield return new WaitForSeconds(AiFrameRate);
        }
    }

    public IEnumerator RepositionCooldown()
    {
        yield return new WaitForSeconds(lightAttack1.abilityDurtation);
        navMeshAgent.speed = enemyStats.swiftness;
        isHAinCooldown = false;
    }

    public IEnumerator LA1Cooldown()
    {
        yield return new WaitForSeconds(lightAttack1.abilityDurtation);
        navMeshAgent.speed = enemyStats.swiftness;
        isHAinCooldown = false;
    }

    public IEnumerator HACooldown()
    {
        yield return new WaitForSeconds(heavyAttack.abilityDurtation);
        navMeshAgent.speed = enemyStats.swiftness;
        isHAinCooldown = false;
    }

    public IEnumerator PatrolOverlay()
    {
        while (true)
        {
            navMeshAgent.speed = enemyStats.swiftness;
            fsmOverlay.Update();
            yield return new WaitForSeconds(AiFrameRate);
        }
    }

    public IEnumerator PatrolPhase1()
	{
        Debug.Log("Phase 1");

		while (!After3WoundRecevied()) 
        {
            navMeshAgent.speed = enemyStats.swiftness;
            fsmPhase1.Update();
            yield return new WaitForSeconds(AiFrameRate);
		}
	}

    public IEnumerator PatrolPhase2()
    {
        Debug.Log("Phase 2");

        while (!BrokenSign() && !HealthUnder50Perc()) {
            navMeshAgent.speed = enemyStats.swiftness;
            fsmPhase2.Update();
            yield return new WaitForSeconds(AiFrameRate);
        }
    }

    public IEnumerator PatrolPhase3()
    {
        Debug.Log("Phase 3");

        while (true) {
            navMeshAgent.speed = enemyStats.swiftness;
            fsmPhase3.Update();
            yield return new WaitForSeconds(AiFrameRate);
        }
    }

    public IEnumerator LateStart()
    {
        yield return new WaitForSeconds(1f);
        navMeshAgent.speed = enemyStats.swiftness;
        targets = GameObject.FindGameObjectsWithTag(targetTag);
        target = targets[0].transform;
    }

    public IEnumerator runningTimer()
	{
        yield return new WaitForSeconds(runningFotTooLongCooldown);
        navMeshAgent.speed = enemyStats.swiftness;
        runningForTooLong = true;
    }

    public IEnumerator WaitForCooldownActionOver()
	{
        yield return new WaitForSeconds(cooldownActionOverTime);
        navMeshAgent.speed = enemyStats.swiftness;
        cooldownActionOver = true;
	}

    public IEnumerator PatrolAttackTree()
	{
		while (true) 
        {
            navMeshAgent.speed = enemyStats.swiftness;
            dt_attackPhase1.walk();
            yield return new WaitForSeconds(AiFrameRate);
		}
	}
    #endregion

    public override void Init()
    {
        base.Init();

        abilites.Add(dashForward.abilityName, dashForward);
        abilites.Add(heavyAttack.abilityName, heavyAttack);
        abilites.Add(lightAttack1.abilityName, lightAttack1);
        abilites.Add(lightAttack2.abilityName, lightAttack2);

        abilites.Add(heavyAttackPhase3.abilityName, heavyAttackPhase3);
        abilites.Add(lightAttack1Phase3.abilityName, lightAttack1Phase3);
        abilites.Add(lightAttack2Phase3.abilityName, lightAttack2Phase3);

        abilites.Add(summonMinions.abilityName, summonMinions);
        abilites.Add(summonTentacles.abilityName, summonTentacles);
        abilites.Add(fall.abilityName, fall);
        abilites.Add(createVulneableSign.abilityName, createVulneableSign);

        abilites.Add(riseUp.abilityName, riseUp);
    }

	#region Animation Event propagation for Scriptable Objects
	public void OnLightAttack1Phase1()
	{
        OnEnemyLightAttack1Phase1?.Invoke(this);
	}

    public void OnLightAttack2Phase1()
    {
        OnEnemyLightAttack2Phase1?.Invoke(this);
    }

    public void OnHeavyAttackPhase1()
    {
        OnEnemyHeavyAttackPhase1?.Invoke(this);
    }

    public void OnLightAttack1Phase3()
    {
        OnEnemyLightAttack1Phase3?.Invoke(this);
    }

    public void OnLightAttack2Phase3()
    {
        OnEnemyLightAttack2Phase3?.Invoke(this);
    }

    public void OnHeavyAttackPhase3()
    {
        OnEnemyHeavyAttackPhase3?.Invoke(this);
    }

	public void OnBrokeSign(EnemyForm e)
	{
        signBroken = true;
	}
    #endregion
}
