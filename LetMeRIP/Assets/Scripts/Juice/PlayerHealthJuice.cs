using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Animator))]
[RequireComponent (typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class PlayerHealthJuice : MonoBehaviour
{
    private Animator animator;
    private Rigidbody rb;
    private Collider collider;
    private PlayerAttackJuice playerAttackJuice;

    private float health;

    public GameObject hitEffect;

    // Start is called before the first frame update
    void Start()
    {
        #region Component's References
        animator = GetComponent<Animator>();
        collider = GetComponent<Collider>();
        rb = GetComponent<Rigidbody>();
        playerAttackJuice = GetComponent<PlayerAttackJuice>();
        #endregion


        health = 100;

        LockCostraints();
    }

    public void TakeDamage(float damageAmount)
    {
        playerAttackJuice.StopAIPlayer();
        UnLockCostraints();

        Debug.Log("Got HIT for: " + damageAmount);
        health -= damageAmount;

        animator.SetTrigger("damage");

        GameObject h = Instantiate(hitEffect, transform.position, transform.rotation);
        Destroy(h, 3f);
    }

    public void LockCostraints()
    {
        rb.constraints = RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezePositionY;
    }

    public void UnLockCostraints()
    {
        rb.constraints = RigidbodyConstraints.None;
    }
}
