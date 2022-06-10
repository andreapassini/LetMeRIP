using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HPManager : MonoBehaviour
{
    public event Action<HPManager> OnPlayerKilled;
    public event Action<HPManager> OnPlayerDamaged;
    public event Action<HPManager> OnPlayerHealed;
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

    private float health { get => stats.health; set => stats.health = value; }
    public float Health { get => stats.health; }

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
            health = (overHeal || health + amount > stats.maxHealth) ? health + amount : stats.maxHealth;
        OnPlayerHealed?.Invoke(this);
    }

    public void DecayingHeal(float amount, float timeToDecay)
    {
        if (amount <= 0) return;
        StartCoroutine(DecayingHealCo(amount, timeToDecay));
    }

    private IEnumerator DecayingHealCo(float amount, float timeToDecay)
    {
        Heal(amount, true);
        float timeStep = 0.5f;
        float healthLossPerTick = amount * timeStep/timeToDecay;

        while(timeToDecay > 0)
        {
            health-=healthLossPerTick;
            if (health <= 0) yield break; // prevents Die() to be called multiple times
            yield return new WaitForSeconds(timeStep);
            timeToDecay -= timeStep;
        }
    }

    public void Die()
    {
        // Die Event 
        OnPlayerKilled?.Invoke(this);

        // Overwrite
    }
}
