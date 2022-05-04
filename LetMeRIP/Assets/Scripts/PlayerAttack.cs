using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAttack : MonoBehaviour
{
    public LayerMask enemyLayers;
    public float attackRate = 1f; // How many hit per second

    private float nextAttackTime = 0f;

    [SerializeField] private Animator animator;
    [SerializeField] private Transform attackPoint;

    [SerializeField] private float attackRange = 1f;

    private bool lightAttack = false;
    private bool heavyAttack = false;

    void Start()
    {
    }

    void Update()
    {
        GatherInput();
    }

    private void FixedUpdate()
    {
        Attack();
    }


    private void GatherInput()
    {
        // Maybe better to use coroutine to manage "reload" time
        if (Input.GetMouseButtonDown(0) && Time.time >= nextAttackTime)
        {
            lightAttack = true;
            nextAttackTime = Time.time + 1f / attackRate;   
        }

        if (Input.GetMouseButtonDown(1))
        {
            heavyAttack = true;
        }
    }

    private void Attack()
    {
        if (lightAttack)
        {
            // Start Animation ( maybe an event )
            animator.SetTrigger("attack");

            // Create Collider
            Collider [] hitEnemies = Physics.OverlapSphere(attackPoint.position, attackRange, enemyLayers);

            // Check for collision
            foreach(Collider enemy in hitEnemies)
            {
                Debug.Log("Hit this guy: " + enemy.name);
            }

            // End attack
            lightAttack = false;

            // Wait for end animation
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.DrawWireSphere(attackPoint.position, attackRange);
    }


}
