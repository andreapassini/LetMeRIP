using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class Boss : EnemyForm
{
    private FSM fsmPhase1;
    private FSM fsmPhase2;
    private FSM fsmPhase3;

    public EnemyAbility dashForward;

    [SerializeField] private string targetTag = "Player";

    public float runningFotTooLongCooldown = 5f;
    public float cooldownActionOverTime = 5f;

    public bool lateStart = false;

    private int woundCount = 0;
    private bool signBroken = false;
    private bool runningForTooLong = false;
    private bool cooldownActionOver = false;
    private bool foundNewTarget = false;

    private float woundLevel;

    // Start is called before the first frame update
    void Start()
    {
        // Gather Stats
        health = enemyStats.maxHealth;
        woundLevel = enemyStats.maxHealth / 7;

        rb = GetComponent<Rigidbody>();

        animator = GetComponent<Animator>();

        navMeshAgent = GetComponent<NavMeshAgent>();

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
        phase3.AddTransition(t3, phase3);

        fsmPhase1 = new FSM(phase1);

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

		#endregion

		StartCoroutine(PatrolPhase1());
    }

	#region Conditions
	public bool After3WoundRecevied()
	{
        if(woundCount >= 3) {
            woundCount = 0;
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
            return true;
		}

        return false;
	}

    public bool TargetFound()
	{
        Vector3 ray = target.position - transform.position;
        RaycastHit hit;
        if (Physics.Raycast(transform.position, ray, out hit, whatICanSeeThrough)) {
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

	}

    public void SearchNewTarget()
	{
		while (true) {
			Transform newTarget = targets[Random.Range(0, targets.Length - 1)].transform;

			Vector3 ray = newTarget.position - transform.position;
			RaycastHit hit;
			if (Physics.Raycast(transform.position, ray, out hit, whatICanSeeThrough)) {
				if (hit.transform == newTarget && newTarget != target) {
					target = newTarget;
                    foundNewTarget = true;
				}
			}
		}
	}


    #endregion

    #region Coroutines
    public IEnumerator PatrolPhase1()
	{
		while (!After3WoundRecevied()) 
        {
            fsmPhase1.Update();
            yield return new WaitForSeconds(AiFrameRate);
		}
	}

    public IEnumerator PatrolPhase2()
    {
        while (!BrokenSign() && !HealthUnder50Perc()) {
            fsmPhase2.Update();
            yield return new WaitForSeconds(AiFrameRate);
        }
    }

    public IEnumerator PatrolPhase3()
    {
        while (true) {
            fsmPhase3.Update();
            yield return new WaitForSeconds(AiFrameRate);
        }
    }

    public IEnumerator LateStart()
    {
        yield return new WaitForSeconds(1f);
        targets = GameObject.FindGameObjectsWithTag(targetTag);
        target = targets[0].transform;
    }

    public IEnumerator runningTimer()
	{
        yield return new WaitForSeconds(runningFotTooLongCooldown);
        runningForTooLong = true;
    }

    public IEnumerator WaitForCooldownActionOver()
	{
        yield return new WaitForSeconds(cooldownActionOverTime);
        cooldownActionOver = true;
	}

    public IEnumerator PatrolAttackTree()
	{
		while (true) 
        {
            // Patrol Attack Tree
            yield return new WaitForSeconds(AiFrameRate);
		}
	}
    #endregion
}
