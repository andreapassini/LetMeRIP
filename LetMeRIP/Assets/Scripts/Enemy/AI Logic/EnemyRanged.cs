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
        InitStats();

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
        escape.stayActions.Add(Dash);

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

    private void OnEnable()
    {
        OnEnemyDamaged += TakeDamageEffect;
        OnEnemyKilled += DieEffect;
    }

    private void OnDisable()
    {
        OnEnemyDamaged -= TakeDamageEffect;
        OnEnemyKilled -= DieEffect;
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
        
        if (attackAction.previousAbilityTime + attackAction.coolDown > Time.time)
        {
            return false;
        }

        float distance = Vector3.Distance(transform.position, target.position);
        if (distance <= attackRange)
        {
            Debug.Log("In Range");
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
        Debug.Log("Too near");
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
        animator.SetFloat("speed", navMeshAgent.velocity.magnitude);

    }

    public void Chase()
    {
        //navMeshAgent.isStopped = false;

        chaseAction.StartAbility(this);
        animator.SetFloat("speed", navMeshAgent.velocity.magnitude);

    }

    public void Attack()
    {
        animator.SetTrigger("attack");
        // navMeshAgent.enabled = false;
        navMeshAgent.isStopped = true;

        attackAction.StartAbility(this);

        
    }

    public void GoToLastSeenPos()
    {
        navMeshAgent.isStopped = false;

        lastSeenPos = new Vector3(target.position.x, target.position.y, target.position.z);
        GetComponent<NavMeshAgent>().destination = lastSeenPos;
        animator.SetFloat("speed", navMeshAgent.velocity.magnitude);

    }

    public void RunFightFSM()
    {
        StartCoroutine(PatrolFight());
    }

    public void Dash()
    {
        animator.SetTrigger("dash"); // TO CREATE

        animator.SetFloat("speed", 0);

        // Disable Navmesh
        // navMeshAgent.isStopped = true;
        // navMeshAgent.enabled = false;

        // Disable isKinematic
        // rb.isKinematic = false;
        // Enable collisions
        // rb.detectCollisions = true;

        dashAction.StartAbility(this);

        StartCoroutine(WaitDashAnimation(dashAction.abilityDurtation));

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

    public IEnumerator WaitDamageAnimation(float stopTime)
    {
        AiFrameRate = stopTime;
        navMeshAgent.velocity = Vector3.zero;
        yield return new WaitForSeconds(stopTime);
        AiFrameRate = reactionReference;

        navMeshAgent.isStopped = false;
    }

    public IEnumerator WaitDieAnimation(float duration)
    {
        navMeshAgent.enabled = false;
        yield return new WaitForSeconds(duration);
        Destroy(gameObject);

    }

    public IEnumerator WaitDashAnimation(float duration)
    {
        yield return new WaitForSeconds(duration);

        rb.isKinematic = true;
        navMeshAgent.enabled = true;
    }
    #endregion

    public void TakeDamageEffect(EnemyForm e)
    {
        if (this == e)
            StartCoroutine(WaitDamageAnimation(takeDamageDuration));
    }

    public void DieEffect(EnemyForm e)
    {
        if (this == e)
            StartCoroutine(WaitDieAnimation(takeDamageDuration));
    }

    public override void InitStats()
    {
        // Gather Stats
        health = enemyStats.maxHealth;

        rb = GetComponent<Rigidbody>();

        animator = GetComponent<Animator>();

        navMeshAgent = GetComponent<NavMeshAgent>();

        reactionReference = AiFrameRate;

        targets = GameObject.FindGameObjectsWithTag(targetTag);
        target = targets[0].transform;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, tooNearRange);
    }
}
