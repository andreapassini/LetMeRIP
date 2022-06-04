using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemySimple : EnemyForm
{
	private FSM fsm;

	private float reactionReference;

    [SerializeField]private string targetTag = "Player";

	private void Start()
	{
        // Gather Stats
        health = enemyStats.maxHealth;
        Debug.Log("Start Health " + health);

        rb = GetComponent<Rigidbody>();

        animator = GetComponent<Animator>();

        navMeshAgent = GetComponent<NavMeshAgent>();

        reactionReference = AiFrameRate;

        targets = GameObject.FindGameObjectsWithTag(targetTag);
        target = targets[0].transform;

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
        search.AddTransition(t1, chase);
        //  in: TargetNotVisible()
        chase.AddTransition(t3, search);
        //      action: GoTo(lastSeenPos)
        // Chase
        //  out: TargetInRange()
        chase.AddTransition(t2, attack);
        //  in: TargetNotInRange()
        attack.AddTransition(t4, chase);
        // Attack

        fsm = new FSM(search);

        StartCoroutine(Patrol());
    }

    // Patrol coroutine
    // Periodic update, run forever
    public IEnumerator Patrol()
    {
        while (true) {
            fsm.Update();
            animator.SetFloat("speed", navMeshAgent.velocity.magnitude);
            yield return new WaitForSeconds(AiFrameRate);
        }
    }


	#region Actions
	// Search
	public void Search()
	{
        searchAction.StartAbility(this);
        animator.SetFloat("speed", navMeshAgent.velocity.magnitude);
    }

    // Chase
    public void Chase()
    {
        chaseAction.StartAbility(this);
        animator.SetFloat("speed", navMeshAgent.velocity.magnitude);
    }

    public void Attack()
    {
        animator.SetTrigger("attack");
        attackAction.StartAbility(this);

        // Wait for the end of animation
        StartCoroutine(StopAI());
    }

    public void GoToLastSeenPos()
    {
        lastSeenPos = new Vector3(target.position.x, target.position.y, target.position.z);
        GetComponent<NavMeshAgent>().destination = lastSeenPos;
        animator.SetFloat("speed", navMeshAgent.velocity.magnitude);
    }
    #endregion

    #region Conditions
    // Target Visible
    public bool TargetVisible()
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

    public bool TargetInRange()
    {
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

    // To manage getting Hit:
    //  => Event when something hit an enemy
    //  => The enemy hit by it will resolve the event

    public IEnumerator StopAI()
    {
        float attackDuration = 1f; // Just as an example 

        AiFrameRate = attackDuration;
        yield return new WaitForSeconds(attackDuration);
        AiFrameRate = reactionReference;
    }
}
