using Photon.Pun;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemyRanged : EnemyForm
{
    private FSM fsm;
    private FSM fightFSM;

    public float tooNearRange = 2f;

    public EnemyAbility dashAction;

    public bool lateStart = false;

    protected override void Awake()
    {
        base.Awake();
    }

    // Start is called before the first frame update
    void Start()
    {
        //if (!PhotonNetwork.IsMasterClient) return;

        // Gather Stats
        health = enemyStats.maxHealth;
        // Debug.Log("Start Health " + health);

        rb = transform.GetComponent<Rigidbody>();

        animator = transform.GetComponent<Animator>();

        navMeshAgent = transform.GetComponent<NavMeshAgent>();

        reactionReference = AiFrameRate;

        if (lateStart) {
            StartCoroutine(LateStart());
        } else {
            targets = GameObject.FindGameObjectsWithTag(targetTag);
            if(targets.Length != 0)
                target = targets[0].transform;
        }

        Init();

        navMeshAgent = transform.GetComponent<NavMeshAgent>();

        FSMState search = new FSMState();
        search.stayActions.Add(Search);

        List<FSMAction> listActions = new List<FSMAction>();
        FSMAction a1 = new FSMAction(GoToLastSeenPos);
        listActions.Add(a1);

        FSMState fight = new FSMState();
        fight.stayActions.Add(RunFightFSM);

        //// FIGHT FSM

        FSMState chase = new FSMState();
        chase.stayActions.Add(Chase);

        FSMState attack = new FSMState();
        attack.stayActions.Add(Attack);

        FSMState escape = new FSMState();
        escape.stayActions.Add(Dash);

        FSMTransition t1 = new FSMTransition(TargetVisible);
        FSMTransition t2 = new FSMTransition(TargetInRange);
        FSMTransition t3 = new FSMTransition(TargetNotVisible, listActions.ToArray());
        FSMTransition t4 = new FSMTransition(TargetNotInRange);
        FSMTransition t5 = new FSMTransition(TargetTooNear);
        FSMTransition t6 = new FSMTransition(TargetNotTooNear);

        //// Search
        ////  out: TargetVisible()
        search.AddTransition(t1, fight);
        ////  in: TargetNotVisible()
        fight.AddTransition(t3, search);
        ////      action: GoTo(lastSeenPos)

        //// Fight
        //// out: TargetNotVisible()

        //// Chase
        ////  out: TargetInRange
        chase.AddTransition(t2, attack);
        
        //// Attack
        //// out: TargetTooNear
        attack.AddTransition(t5, escape);
        ////  in: TargetInRange()
        ////  in: TargetTargetNotTooNear()

        //// Escape
        //// out: TargetNotInRange()
        escape.AddTransition(t4, chase);
        //// out: TargetTargetNotTooNear()
        escape.AddTransition(t6, attack);

        fsm = new FSM(search);

        fightFSM = new FSM(chase);

        StartCoroutine(Patrol());
    }

    private void OnEnable()
    {
        
    }

    private void OnDisable()
    {
        
    }

    #region Conditions
    // Target Visible
    public bool TargetVisible()
    {
        Vector3 ray = target.position - transform.position;
        RaycastHit hit;
        if (Physics.Raycast(transform.position, ray, out hit, Mathf.Infinity, ~whatRayHit))
        {
            if (hit.transform == null)
                return false;

            if (hit.transform == target)
            {
                return true;
            }
        }
        return false;
    }

    public bool TargetInRange()
    {
        float distance = (target.position - transform.position).magnitude;
        //Debug.Log("Ref " + attackRange + " - " + distance);
        if (distance <= attackRange)
        {
            return true;
        }
        return false;
    }

    public bool TargetNotVisible()
    {
        return !TargetVisible();
    }

    public bool TargetNotInRange()
    {
        return !TargetInRange();
    }

    private bool TargetTooNear()
    {
        float distance = (target.position - transform.position).magnitude;
        if (distance <= tooNearRange)
        {
            return true;
        }
        return false;
    }

    private bool TargetNotTooNear()
    {
        return !TargetTooNear();
    }
    #endregion

    #region Actions
    public void Search()
    {
        searchAction.StartAbility(this);
        animator.SetBool("run", true);
    }

    public void Chase()
    {
        if (target == null) {
            targets = GameObject.FindGameObjectsWithTag(targetTag);
            if (targets.Length != 0)
                target = targets[0].transform;
        }

        chaseAction.StartAbility(this);
        animator.SetBool("run", true);
    }

    public void Attack()
    {
        if (target == null) {
            targets = GameObject.FindGameObjectsWithTag(targetTag);
            if (targets.Length != 0)
                target = targets[0].transform;
        }

        animator.SetBool("run", false);

        attackAction.StartAbility(this);

        //StartCoroutine(StopAI());
    }

    public void GoToLastSeenPos()
    {
        if (target == null) {
            targets = GameObject.FindGameObjectsWithTag(targetTag);
            if (targets.Length != 0)
                target = targets[0].transform;
        }

        lastSeenPos = new Vector3(target.position.x, target.position.y, target.position.z);
        GetComponent<NavMeshAgent>().destination = lastSeenPos;
        animator.SetBool("run", true);
    }

    public void RunFightFSM()
    {
        StartCoroutine(PatrolFight());
    }

    public void Dash()
    {
        if (target == null) {
            targets = GameObject.FindGameObjectsWithTag(targetTag);
            if (targets.Length != 0)
                target = targets[0].transform;
        }

        dashAction.StartAbility(this);
        
    }
    #endregion
    
    #region Coroutines
    public IEnumerator Patrol()
    {
        while (true)
        {
            if (!stopAI)
            {
                navMeshAgent.speed = enemyStats.swiftness;
                fsm.Update();
            }

            yield return new WaitForSeconds(AiFrameRate);
        }
    }

    public IEnumerator PatrolFight()
    {
        while (TargetVisible())
        {
            if (!stopAI)
            {
                navMeshAgent.speed = enemyStats.swiftness;
                fightFSM.Update();
            }
            
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
    #endregion

	public override void Init()
	{
		base.Init();

        abilites.Add(dashAction.abilityName, dashAction);
    }

	private void OnDrawGizmosSelected()
	{
        Gizmos.color = Color.yellow;

        Gizmos.DrawWireSphere(transform.position, attackRange);
	}

    public override void RestartAI()
    {
        base.RestartAI();

        fsm.Update();
    }

    public override void OnAttack()
	{
        base.OnAttack();
        StartCoroutine(waitToShoot(.05f, attackAction));
    }

}
