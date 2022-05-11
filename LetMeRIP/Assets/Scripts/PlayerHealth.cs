using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] float maxHealth = 100f;
    private float health;

    private Rigidbody rb;

    void Start()
    {
        health = maxHealth;
        rb = GetComponent<Rigidbody>();
    }

    public void OnDamage(float dmg, Vector3 positionHit)
    {
        Debug.Log("Got HIT");

        health -= dmg;

        if (health <= 0)
        {
            Die();
        }

        Vector3 fromHitToBody = (transform.position - positionHit).normalized;

        rb.AddForce(fromHitToBody * dmg / 10, ForceMode.Impulse);
    }

    public void Die()
    {
        Object.Destroy(gameObject);
    }
}
