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
    public float health;
    public float spiritGauge;

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

    public void Heal(float healAmmount)
	{
        health += healAmmount;

        if(health > playerStats.maxHealth) {
            health = playerStats.maxHealth;
		}
	}

    public virtual void Die()
    {
        // Die Event 
        OnPlayerKilled?.Invoke(this);

        // Overwrite
    }

}