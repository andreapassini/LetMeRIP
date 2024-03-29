using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemySimple : EnemyForm
{
	private FSM fsm;

    public bool lateStart = false;

    protected override void Awake()
    {
        base.Awake();
    }

    private void Start()
	{
        Init();

        // Gather Stats
        //health = enemyStats.maxHealth;
        // Debug.Log("Start Health " + health);

        rb = GetComponent<Rigidbody>();

        animator = GetComponent<Animator>();

        navMeshAgent = GetComponent<NavMeshAgent>();

        reactionReference = AiFrameRate;

        if (lateStart) {
            StartCoroutine(LateStart());
        } else {
            targets = GameObject.FindGameObjectsWithTag(targetTag);
            target = targets[0].transform;
            if (targets.Length != 0)
                target = targets[0].transform;
        }

        FSMState search = new FSMState();
		search.stayActions.Add(Search);

        FSMState chase = new FSMState();
        chase.stayActions.Add(Chase);

        FSMState attack = new FSMState();
        attack.stayActions.Add(Attack);

        List<FSMAction> listActions = new List<FSMAction>();
        FSMAction a1 = new FSMAction(GoToLastSeenPos);
        listActions.Add(a1);

        FSMTransition t1 = new FSMTransition(TargetVisible);
        FSMTransition t2 = new FSMTransition(TargetInRange);
        FSMTransition t3 = new FSMTransition(TargetNotVisible, listActions.ToArray());
        FSMTransition t4 = new FSMTransition(TargetNotInRange);

        // Search
        //  out: TargetVisible()
        //search.AddTransition(t1, chase);
        //  in: TargetNotVisible()
        //chase.AddTransition(t3, search);
        //      action: GoTo(lastSeenPos)
        // Chase
        //  out: TargetInRange()
        chase.AddTransition(t2, attack);
        //  in: TargetNotInRange()
        attack.AddTransition(t4, chase);
        // Attack

        fsm = new FSM(chase);

        // Stop AI for Camera
        StartCoroutine(Patrol());
    }

    private void OnEnable()
    {

    }

    private void OnDisable()
    {

    }

	#region Actions
	// Search
	public void Search()
	{
        searchAction.StartAbility(this);
        animator.SetBool("run", true);
    }

    // Chase
    public void Chase()
    {
        target = FindNearTarget();

        if (target == null)
            return;

        chaseAction.StartAbility(this);
        animator.SetBool("run", true);
    }

    public void Attack()
    {
        //animator.SetTrigger("attack");
        animator.SetBool("run", false);
        attackAction.StartAbility(this);
    }

    public void GoToLastSeenPos()
    {
        if (target == null) {
            targets = GameObject.FindGameObjectsWithTag(targetTag);
            if(targets.Length != 0)
                target = targets[0].transform;
        }

        lastSeenPos = new Vector3(target.position.x, target.position.y, target.position.z);
        GetComponent<NavMeshAgent>().destination = lastSeenPos;
        animator.SetBool("run", true);
    }
    #endregion

    #region Conditions
    // Target Visible
    public bool TargetVisible()
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

    public bool TargetInRange()
    {
        if (target == null)
            return false;

        float distance = (target.position - transform.position).magnitude;
        if (distance <= attackRange) {
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
    #endregion

    public Vector3 RandomNavmeshLocation(float radius)
    {
        while (true) {
            Vector3 randomDirection = UnityEngine.Random.insideUnitSphere * radius;
            randomDirection = randomDirection * 10 + transform.position;
            NavMeshHit hit;

            if (NavMesh.SamplePosition(randomDirection, out hit, radius, 1)) {
                return hit.position;
            }
        }
    }

    #region Coroutines
    // Patrol coroutine
    // Periodic update, run forever
    public IEnumerator Patrol()
    {
        while (true) {
            if (!stopAI)
            {
                navMeshAgent.speed = enemyStats.swiftness;
                FindNearTarget();
                fsm.Update();
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

    #region Effects
    #endregion


    public override void RestartAI()
    {
        base.RestartAI();

        fsm.Update();
    }
}
