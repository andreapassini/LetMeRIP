using UnityEngine;
using System;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]
public class EnemyForm : MonoBehaviour
{
    public static event Action<EnemyForm> OnEnemyKilled;
    public static event Action<EnemyForm> OnEnemyDamaged;

    public EnemyStats enemyStats;

    //public List<EnemyAbility> enemyAbilities;
    public EnemyAbility attackAction;
    public EnemyAbility chaseAction;
    public EnemyAbility searchAction;


    public float AiFrameRate = 1f;

    [System.NonSerialized]
    public Vector3 lastSeenPos;

    public Transform attackPoint;
    public float attackRange = 3f;

    private float health;

    public LayerMask whatIsTarget;
    public LayerMask whatICanSeeThrough;

    public Animator animator;

    [System.NonSerialized]
    public Rigidbody rb;

    [System.NonSerialized]
    public GameObject[] targets;

    [System.NonSerialized]
    public Transform target;

    [System.NonSerialized]
    public NavMeshAgent navMeshAgent;

    // Do not update FSM until the action is over
    private bool isExecutingAction = false;

    // Start is called before the first frame update
    void Start()
    {
        // Gather Stats
        health = enemyStats.maxHealth;
        rb = GetComponent<Rigidbody>();

        animator = GetComponent<Animator>();

        navMeshAgent = GetComponent<NavMeshAgent>();
    }

    public void TakeDamage(float dmg)
    {
        // Calcolate defense reduction
        dmg -= enemyStats.defense;
        dmg = Mathf.Clamp(dmg, 0, float.MaxValue);

        health -= dmg;

        if (health <= 0) {
            Die();
        }

        // Take damage Event
        OnEnemyDamaged?.Invoke(this);
    }

    public virtual void Die()
	{
        // Die Event 
        OnEnemyKilled?.Invoke(this);

        // Overwrite
    }
}
