using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
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

    //private void GatherInput()
    //{
    //    // Maybe better to use coroutine to manage "reload" time
    //    if (Input.GetMouseButtonDown(0) && Time.time >= nextAttackTime)
    //    {
    //        lightAttack = true;
    //        nextAttackTime = Time.time + 1f / attackRate;   
    //    }

    //    if (Input.GetMouseButtonDown(1))
    //    {
    //        heavyAttack = true;
    //    }
    //}

    public void LightAttack(InputAction.CallbackContext context)
    {
        if (context.performed && !lightAttack)
        {
            lightAttack = true;

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
            StartCoroutine(AttackCooldown());

            // Wait for end animation
        }
    }

    private IEnumerator AttackCooldown()
    {
        yield return new WaitForSeconds(attackRate);
        lightAttack = false;
    }

    private void OnDrawGizmos()
    {
        Gizmos.DrawWireSphere(attackPoint.position, attackRange);
    }


}
