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

    [SerializeField] private string targetTag = "Player";

    public EnemyAbility dashAction;

    // Start is called before the first frame update
    void Start()
    {
        reactionReference = AiFrameRate;

        targets = GameObject.FindGameObjectsWithTag(targetTag);
        target = targets[0].transform;

		navMeshAgent = transform.GetComponent<NavMeshAgent>();

        FSMState search = new FSMState();
        search.stayActions.Add(Search);

        List<FSMAction> listActions = new List<FSMAction>();
        FSMAction a1 = new FSMAction(GoToLastSeenPos);
        listActions.Add(a1);

        FSMState fight = new FSMState();
        fight.stayActions.Add(RunFightFSM);

        // FIGHT FSM

        FSMState chase = new FSMState();
        chase.stayActions.Add(Chase);

        FSMState attack = new FSMState();
        attack.stayActions.Add(Attack);

        FSMState escape = new FSMState();
        escape.stayActions.Add(Escape);

        FSMTransition t1 = new FSMTransition(TargetVisible);
        FSMTransition t2 = new FSMTransition(TargetInRange);
        FSMTransition t3 = new FSMTransition(TargetNotVisible, listActions.ToArray());
        FSMTransition t4 = new FSMTransition(TargetNotInRange);
        FSMTransition t5 = new FSMTransition(TargetTooNear);
        FSMTransition t6 = new FSMTransition(TargetNotTooNear);

        // Search
        //  out: TargetVisible()
        search.AddTransition(t1, fight);
        //  in: TargetNotVisible()
        fight.AddTransition(t3, search);
        //      action: GoTo(lastSeenPos)

        // Fight
        // out: TargetNotVisible()

        // Chase
        //  out: TargetInRange
        chase.AddTransition(t2, attack);
        
        // Attack
        // out: TargetTooNear
        attack.AddTransition(t5, escape);
        //  in: TargetInRange()
        //  in: TargetTargetNotTooNear()

        // Escape
        // out: TargetNotInRange()
        escape.AddTransition(t4, chase);
        // out: TargetTargetNotTooNear()
        escape.AddTransition(t6, attack);

        fsm = new FSM(search);

        fightFSM = new FSM(chase);

        StartCoroutine(Patrol());
    }

    #region Conditions
    // Target Visible
    public bool TargetVisible()
    {
        Vector3 ray = target.position - transform.position;
        RaycastHit hit;
        if (Physics.Raycast(transform.position, ray, out hit, whatICanSeeThrough))
        {
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
        // navMeshAgent.isStopped = false;

        searchAction.StartAbility(this);
    }

    public void Chase()
    {
        //navMeshAgent.isStopped = false;

        chaseAction.StartAbility(this);
    }

    public void Attack()
    {
        // navMeshAgent.enabled = false;
        navMeshAgent.isStopped = true;

        attackAction.StartAbility(this);

        // Wait for the end of animation
        // StartCoroutine(StopAI(2f));
    }

    public void GoToLastSeenPos()
    {
        navMeshAgent.isStopped = false;

        lastSeenPos = new Vector3(target.position.x, target.position.y, target.position.z);
        GetComponent<NavMeshAgent>().destination = lastSeenPos;
    }

    public void RunFightFSM()
    {
        StartCoroutine(PatrolFight());
    }

    public void Escape()
    {
        // Disable Navmesh
        navMeshAgent.isStopped = true;
        navMeshAgent.enabled = false;
        // Disable isKinematic
        rb.isKinematic = false;
        // Enable collisions
        rb.detectCollisions = true;

        dashAction.StartAbility(this);

        // Enable isKinematic
        rb.isKinematic = true;
        rb.detectCollisions = false; // Double check this in test
                                           // Enable Navmesh
        navMeshAgent.enabled = true;
        // Disable collisions	
    }
    #endregion

    #region Coroutines
    public IEnumerator Patrol()
    {
        while (true)
        {
            fsm.Update();
            yield return new WaitForSeconds(AiFrameRate);
        }
    }

    public IEnumerator PatrolFight()
    {
        while (TargetVisible())
        {
            fightFSM.Update();
            yield return new WaitForSeconds(AiFrameRate);
        }
    }

    // To manage getting Hit:
    //  => Event when something hit an enemy
    //  => The enemy hit by it will resolve the event

    public IEnumerator StopAI(float stopTime)
    {
        AiFrameRate = stopTime;
        yield return new WaitForSeconds(stopTime);
        AiFrameRate = reactionReference;

        navMeshAgent.isStopped = false;
    }
    #endregion
}
