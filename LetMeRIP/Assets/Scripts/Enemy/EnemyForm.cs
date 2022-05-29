using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.AI;

[RequireComponent(typeof(Rigidbody))]
public class EnemyForm : MonoBehaviour
{
    public static event Action<EnemyForm> OnEnemyKilled;
    public static event Action<EnemyForm> OnEnemyDamaged;

    public EnemyStats enemyStats;

    public List<EnemyAbility> enemyAbilities;

    public float AiFrameRate = 1f;

    public Vector3 lastSeenPos;

    public Transform attackPoint;
    public float attackRange = 3f;

    private float health;

    private Rigidbody rb;

    public LayerMask whatIsTarget;
    public Animator animator;
    public GameObject[] targets;
    public Transform target;

    // Start is called before the first frame update
    void Start()
    {
        // Gather Stats
        health = enemyStats.maxHealth;
        rb = GetComponent<Rigidbody>();

        animator = GetComponent<Animator>();
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
