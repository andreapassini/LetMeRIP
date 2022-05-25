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
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float bulletForce = 10f;

    private bool mageLightAttack = false;

    public void LightMageAttack(InputAction.CallbackContext context)
    {
        if (context.performed && !mageLightAttack)
        {
            mageLightAttack = true;

            // Start Animation ( maybe an event )
            animator.SetTrigger("attack");

            // Fire bullet
            GameObject bullet = Instantiate(bulletPrefab, attackPoint.position, attackPoint.rotation);

            Rigidbody rb = bullet.GetComponent<Rigidbody>();
            rb.AddForce(attackPoint.forward * bulletForce, ForceMode.Impulse);

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
