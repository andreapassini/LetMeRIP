using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemySimple : EnemyForm
{
	[SerializeField] private float attackRange = 2f;
	[SerializeField] private Transform attackPoint;

	private Vector3 lastSeenPos;
	private FSM fsm;

	private float reactionReference;

    [SerializeField]private string targetTag = "Player";



	private void Start()
	{
		reactionReference = AiFrameRate;

        targets = GameObject.FindGameObjectsWithTag(targetTag);

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
            yield return new WaitForSeconds(AiFrameRate);
        }
    }


    #region Actions
    // Search
    public void Search()
    {
        float distance = float.MaxValue;

		foreach (GameObject t in targets) {
            float calculatedDistance = (t.transform.position - transform.position).magnitude;
            if (calculatedDistance < distance) {
                distance = calculatedDistance;
                target = t.transform;
			}
		}

        if ((target.position - lastSeenPos).magnitude <= 1f) {
            // Go to a random new pos on the Navmesh
            GetComponent<NavMeshAgent>().isStopped = false;
            GetComponent<NavMeshAgent>().destination = RandomNavmeshLocation(10f);
        }
    }

    // Chase
    public void Chase()
    {
        GetComponent<NavMeshAgent>().isStopped = false;
        GetComponent<NavMeshAgent>().destination = target.position;

    }

    public void Attack()
    {
        // Stop Moving
        GetComponent<NavMeshAgent>().isStopped = true;
        animator.SetTrigger("attack");

        // Look at Target
        transform.LookAt(new Vector3(target.position.x, transform.position.y, target.position.z), Vector3.up);

        // Play attack animation


        // Create Collider
        Collider[] hitEnemies = Physics.OverlapSphere(attackPoint.position, attackRange, base.whatIsTarget);

        // Check for collision
        foreach (Collider enemy in hitEnemies) {
            //Debug.Log("Hit this guy: " + enemy.name);

            PlayerHealth playerHealth = enemy.gameObject.GetComponent<PlayerHealth>();

            if (playerHealth != null) {
                playerHealth.TakeDamage(enemyStats.attack, transform.position);
            }
        }

        // Wait for the end of animation
        StartCoroutine(StopAI());
    }

    public void GoToLastSeenPos()
    {
        lastSeenPos = new Vector3(target.position.x, target.position.y, target.position.z);
        GetComponent<NavMeshAgent>().destination = lastSeenPos;
    }
    #endregion

    #region Conditions
    // Target Visible
    public bool TargetVisible()
    {
        Vector3 ray = target.position - transform.position;
        RaycastHit hit;
        if (Physics.Raycast(transform.position, ray, out hit)) {
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
            randomDirection += transform.position;
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

        // reactionTime = attackDuration;
        yield return new WaitForSeconds(attackDuration);
        // reactionTime = reactionReference;
    }
}
