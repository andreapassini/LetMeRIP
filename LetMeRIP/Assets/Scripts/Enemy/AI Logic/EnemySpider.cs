using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemySpider : EnemyForm
{
    private FSM fsm;

    [SerializeField] private string targetTag = "Player";

    private bool lateStart = false;

    private void Start()
    {

        Init();

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

        if (!PhotonNetwork.IsMasterClient) return;

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
        animator.SetFloat("speed", 0);

        attackAction.StartAbility(this);

        // Wait for the end of animation
        // StartCoroutine(StopAI());
    }

    public void GoToLastSeenPos()
    {
        lastSeenPos = new Vector3(target.position.x, target.position.y, target.position.z);
        navMeshAgent.destination = lastSeenPos;
        animator.SetFloat("speed", navMeshAgent.velocity.magnitude);
    }
    #endregion

    #region Conditions
    // Target Visible
    public bool TargetVisible()
    {
        Vector3 ray = target.position - transform.position;
        RaycastHit hit;
        if (Physics.Raycast(transform.position, ray, out hit, Mathf.Infinity, ~whatRayHit))
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
        float distance = Vector3.Distance(transform.position, target.position);
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
    #endregion

    public Vector3 RandomNavmeshLocation(float radius)
    {
        while (true)
        {
            Vector3 randomDirection = UnityEngine.Random.insideUnitSphere * radius;
            randomDirection = randomDirection * 10 + transform.position;
            NavMeshHit hit;

            if (NavMesh.SamplePosition(randomDirection, out hit, radius, 1))
            {
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
            navMeshAgent.speed = enemyStats.swiftness;
            fsm.Update();
            yield return new WaitForSeconds(AiFrameRate);
        }
    }

    public IEnumerator StopAI()
    {
        navMeshAgent.speed = enemyStats.swiftness;
        float attackDuration = 1f; // Just as an example 

        AiFrameRate = attackDuration;
        yield return new WaitForSeconds(attackDuration);
        AiFrameRate = reactionReference;
    }

    public IEnumerator StopAI(float duration)
    {
        navMeshAgent.speed = enemyStats.swiftness;
        navMeshAgent.velocity = Vector3.zero;
        //navMeshAgent.isStopped = true;
        AiFrameRate = duration;
        yield return new WaitForSeconds(duration);
        AiFrameRate = reactionReference;
        //navMeshAgent.isStopped = false;
        //navMeshAgent.isStopped = false;
    }



    public IEnumerator WaitDieAnimation(float duration)
    {
        navMeshAgent.speed = enemyStats.swiftness;
        navMeshAgent.enabled = false;
        yield return new WaitForSeconds(duration);
        navMeshAgent.speed = enemyStats.swiftness;
        Destroy(gameObject);

    }

    public IEnumerator LateStart()
    {
        yield return new WaitForSeconds(1f);
        navMeshAgent.speed = enemyStats.swiftness;
        targets = GameObject.FindGameObjectsWithTag(targetTag);
        target = targets[0].transform;
    }
    #endregion

    #region effects
    public void TakeDamageEffect(EnemyForm e)
    {
        if (this == e)
            StartCoroutine(StopAI(takeDamageDuration));
    }

    public void DieEffect(EnemyForm e)
    {
        if (this == e)
            StartCoroutine(StopAI(takeDamageDuration));
    }
    #endregion
}
