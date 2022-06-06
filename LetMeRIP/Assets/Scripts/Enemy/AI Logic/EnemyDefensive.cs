using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemyDefensive : EnemyForm
{
    private FSM fsm;
    private FSM fightFSM;

    [SerializeField] private string targetTag = "Player";

    public float timeToAttack;
    public float timeToBLock;

    public EnemyAbility blockAction;

    private bool isBlocking;

    private void Start()
    {
        // Gather Stats
        health = enemyStats.maxHealth;
        Debug.Log("Start Health " + health);

        rb = GetComponent<Rigidbody>();

        animator = GetComponent<Animator>();

        navMeshAgent = GetComponent<NavMeshAgent>();

        reactionReference = AiFrameRate;

        isBlocking = true;

        targets = GameObject.FindGameObjectsWithTag(targetTag);
        target = targets[0].transform;

        FSMState search = new FSMState();
        search.stayActions.Add(Search);

        FSMState chase = new FSMState();
        chase.stayActions.Add(Chase);

        List<FSMAction> listActions = new List<FSMAction>();
        FSMAction a1 = new FSMAction(GoToLastSeenPos);
        listActions.Add(a1);

        FSMState fight = new FSMState();
        fight.stayActions.Add(RunFightFSM);

        // FIGHT FSM

        FSMState block = new FSMState();
        block.enterActions.Add(BlockCooldown);
        block.stayActions.Add(Block);

        FSMState attack = new FSMState();
        block.enterActions.Add(AttackCooldown);
        attack.stayActions.Add(Attack);

        FSMTransition t1 = new FSMTransition(TargetVisible);
        FSMTransition t2 = new FSMTransition(TargetInRange);
        FSMTransition t3 = new FSMTransition(TargetNotVisible, listActions.ToArray());
        FSMTransition t4 = new FSMTransition(TargetNotInRange);
        FSMTransition t5 = new FSMTransition(TimeToAttack);
        FSMTransition t6 = new FSMTransition(TimeToBlock);

        // Search
        //  out: TargetVisible()
        search.AddTransition(t1, chase);
        //  in: TargetNotVisible()
        chase.AddTransition(t3, search);
        //      action: GoTo(lastSeenPos)

        // Chase
        //  out: TargetInRange()
        chase.AddTransition(t2, fight);
        //  in: TargetNotInRange()
        fight.AddTransition(t4, chase);
        // Attack

        // Fight
        // out: TargetNotInRange()
        // Block
        //  out: TimeToAttack
        block.AddTransition(t5, attack);
        //  in: TimeToBlock
        attack.AddTransition(t6, block);

        fsm = new FSM(search);

        fightFSM = new FSM(block);

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

    public bool TimeToBlock()
    {
        return isBlocking;
    }

    public bool TimeToAttack()
    {
        return !isBlocking;
    }

    #endregion

    #region Actions
    public void Search()
    {
        searchAction.StartAbility(this);
    }

    public void Chase()
    {
        chaseAction.StartAbility(this);
    }

    public void Attack()
    {
        attackAction.StartAbility(this);

        // Wait for the end of animation
        StartCoroutine(StopAI(1f));
    }

    public void GoToLastSeenPos()
    {
        lastSeenPos = new Vector3(target.position.x, target.position.y, target.position.z);
        GetComponent<NavMeshAgent>().destination = lastSeenPos;
    }

    public void RunFightFSM()
    {
        StartCoroutine(PatrolFight());
    }

    public void Block()
    {
        blockAction.StartAbility(this);

        // Wait for the end of animation
        StartCoroutine(StopAI(1f));
    }

    public void BlockCooldown()
    {
        StartCoroutine(CooldownBLock());
    }

    public void AttackCooldown()
    {
        StartCoroutine(CooldownAttack());
    }

    #endregion

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
        while (TargetInRange())
        {
            fightFSM.Update();
            yield return new WaitForSeconds(AiFrameRate);
        }
    }

    public IEnumerator CooldownBLock()
    {
        yield return new WaitForSeconds(timeToBLock);
        isBlocking = false;
    }

    public IEnumerator CooldownAttack()
    {
        yield return new WaitForSeconds(timeToAttack);
        isBlocking = true;
    }

    // To manage getting Hit:
    //  => Event when something hit an enemy
    //  => The enemy hit by it will resolve the event

    public IEnumerator StopAI(float stopTime)
    {
        float attackDuration = stopTime; // Just as an example 

        AiFrameRate = attackDuration;
        yield return new WaitForSeconds(attackDuration);
        AiFrameRate = reactionReference;
    }
}
