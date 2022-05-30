using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HPManager : MonoBehaviour
{
    public static event Action<HPManager> OnPlayerKilled;
    public static event Action<HPManager> OnPlayerDamaged;
    public static event Action<HPManager> OnPlayerHealed;
    private PlayerStats stats;
    public PlayerStats Stats
    {
        get => stats;
        set
        {
            stats = value;
            health = value.health;
        }
    }

    private PlayerController characterController;

    private float health;

    void Start()
    {
        gameObject.GetComponent<PlayerController>();
    }

    public void TakeDamage(float dmg, Vector3 positionHit)
    {
        //Debug.Log("Got HIT");

        // Calculate defense reduction
        if (dmg > 0) 
            health -= dmg - (dmg * (stats.defense * 0.01f));

        if (health <= 0)
        {
            Die();
        }

        // Take damage Event
        OnPlayerDamaged?.Invoke(this);
    }

    public void Heal(float amount, bool overHeal = false)
    {
        if(amount > 0)
            health = (health + amount > stats.maxHealth || overHeal) ? stats.maxHealth : health + amount;
        OnPlayerHealed?.Invoke(this);
    }

    /**
     * Tries to consume Spirit points, if it succeeds returns true, false otherwise.
     * it returns true if the amount of spirit points left is greater or equal than amount
     * if ignoreMissingPoints is true then any missing points to consume are ignored and true is returned.
     */
    //public bool ConsumeSpiritPoints(float amount, bool ignoreMissingPoints = false)
    //{
    //    if (spiritGauge >= amount) {
    //        spiritGauge -= amount;
    //        return true;
    //    }
    //    else if (ignoreMissingPoints) 
    //    {
    //        spiritGauge = 0;
    //        return true;
    //    }
    //    return false;
    //}

    public void Die()
    {
        // Die Event 
        OnPlayerKilled?.Invoke(this);

        // Overwrite
    }
}
