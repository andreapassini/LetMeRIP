using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public static event Action<PlayerHealth> OnPlayerKilled;
    public static event Action<PlayerHealth> OnPlayerDamaged;
    public PlayerStats playerStats;
    private float health;
    private float spiritGauge;

    private Rigidbody rb;

    void Start()
    {
        health = playerStats.maxHealth;
        spiritGauge = playerStats.maxSpiritGauge;
        rb = GetComponent<Rigidbody>();
    }

    public void TakeDamage(float dmg, Vector3 positionHit)
    {
        //Debug.Log("Got HIT");

        // Calcolate defense reduction
        health -= dmg;

        if (health <= 0)
        {
            Die();
        }

        // Take damage Event
        OnPlayerDamaged?.Invoke(this);
    }

    public void Die()
    {
        // Die Event 
        OnPlayerKilled?.Invoke(this);

        // Overwrite
    }
}
