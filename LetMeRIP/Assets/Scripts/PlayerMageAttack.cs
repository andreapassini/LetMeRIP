using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMageAttack : MonoBehaviour
{
    public LayerMask whatIsEnemy;
    public float attackRate = 1f; // How many hit per second

    [SerializeField] private Animator animator;
    [SerializeField] private Transform attackPoint;
    [SerializeField] private GameObject bullet;
    [SerializeField] private float bulletForce = 2f;

    private bool mageLightAttack = false;

    public void LightMageAttack(InputAction.CallbackContext context)
    {
        if (context.performed && !mageLightAttack)
        {
            mageLightAttack = true;

            // Start Animation ( maybe an event )
            animator.SetTrigger("attack");

            // Fire bullet
            Instantiate(bullet, attackPoint.position, attackPoint.rotation);

            Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
            rb.AddForce(attackPoint.up * bulletForce, ForceMode2D.Impulse);

            // End attack
            StartCoroutine(AttackCooldown());

            // Wait for end animation
        }
    }

    private IEnumerator AttackCooldown()
    {
        yield return new WaitForSeconds(attackRate);
        mageLightAttack = false;
    }
}
