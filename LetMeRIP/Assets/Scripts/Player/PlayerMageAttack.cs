using System.Collections;
using Photon.Pun;
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

    public PhotonView view;

    private bool mageLightAttack = false;

    public void LightMageAttack(InputAction.CallbackContext context)
    {
        if (!view.IsMine) return;
        if (!context.performed || mageLightAttack) return;
        
        mageLightAttack = true;

        // Start Animation ( maybe an event )
        animator.SetTrigger("attack");

        // Fire bullet
        GameObject bullet = PhotonNetwork.Instantiate("Bullet", attackPoint.position, attackPoint.rotation);

        Rigidbody rb = bullet.GetComponent<Rigidbody>();
        rb.AddForce(attackPoint.forward * bulletForce, ForceMode.Impulse);

        // End attack
        StartCoroutine(AttackCooldown());

        // Wait for end animation
    }

    private IEnumerator AttackCooldown()
    {
        yield return new WaitForSeconds(attackRate);
        mageLightAttack = false;
    }
}
