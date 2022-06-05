using UnityEngine;
using System;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]
public abstract class EnemyForm : MonoBehaviour
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

    [HideInInspector]
    public float health;

    public LayerMask whatIsTarget;
    public LayerMask whatICanSeeThrough;

    public Animator animator;

    public float takeDamageDuration = 1f;

    [System.NonSerialized]
    public Rigidbody rb;

    [System.NonSerialized]
    public GameObject[] targets;

    [System.NonSerialized]
    public Transform target;

    //[System.NonSerialized]
    public NavMeshAgent navMeshAgent;

    [System.NonSerialized]
    public float reactionReference;

    // Start is called before the first frame update
    void Start()
    {
    }

    public virtual void TakeDamage(float dmg)
    {
        animator.SetTrigger("damage");      

        // Calcolate defense reduction
        dmg -= enemyStats.defense;
        dmg = Mathf.Clamp(dmg, 0, float.MaxValue);
        health = health - dmg;

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
        Destroy(gameObject);
    }

    // Wait until the end of the action to update again the FSM
    public virtual void CastAbilityDuration(EnemyAbility ability)
    {
        if (AiFrameRate < ability.abilityDurtation)
        {
            StartCoroutine(AbilityDuration(ability));
        }
    }

    private IEnumerator AbilityDuration(EnemyAbility ability)
    {
        // Stop FSM
        reactionReference = AiFrameRate;
        AiFrameRate = ability.abilityDurtation;

        yield return new WaitForSeconds(ability.abilityDurtation);

        AiFrameRate = reactionReference;
        
    }

    public abstract void InitStats();
}
