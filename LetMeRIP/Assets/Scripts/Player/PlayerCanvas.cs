using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

[RequireComponent(typeof(Rigidbody))]
public class PlayerCanvas : MonoBehaviour
{
    public static event Action<PlayerCanvas> OnPlayerKilled;
    public static event Action<PlayerCanvas> OnPlayerDamaged;
    public PlayerStats playerStats;
    private float health;
    private float spiritGauge;

    private Rigidbody rb;

    void Start()
    {
        // Gather Stats
        health = playerStats.maxHealth;
        spiritGauge = playerStats.maxSpiritGauge;
        rb = GetComponent<Rigidbody>();
    }

    public void TakeDamage(float dmg)
    {
        // Calcolate defense reduction
        dmg -= playerStats.defense;
        dmg = Mathf.Clamp(dmg, 0, float.MaxValue);
        
        health -= dmg;

        if (health <= 0) {
            Die();
        }

        // Take damage Event
        OnPlayerDamaged?.Invoke(this);
    }

    public virtual void Die()
    {
        // Die Event 
        OnPlayerKilled?.Invoke(this);

        // Overwrite
    }
}
