using Photon.Pun;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HPManager : MonoBehaviourPun
{
    public static event Action<HPManager> OnPlayerKilled;
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
    private float maxHealth { get => stats.maxHealth; set => stats.maxHealth = value; }
    public float Health { get => stats.health; }
    public float MaxHealth { get => stats.maxHealth; }


    void Start()
    {
        characterController = gameObject.GetComponent<PlayerController>();
    }

    public void TakeDamage(float dmg, Vector3 positionHit)
    {
        Debug.Log("Got HIT");

        // Calculate defense reduction
        if (dmg > 0) 
            health -= dmg - (dmg * (stats.defense * 0.01f));

        if (health <= 0)
        {
            Debug.Log("calling die");
            Die();
        }

        // Take damage Event
        OnPlayerDamaged?.Invoke(this);
    }

    public void Heal(float amount, bool overHeal = false)
    {
        if(amount > 0)
        {
            if (overHeal)
            {
                health += amount;
                stats.maxHealth += amount;
            } else
            {
                health = Mathf.Clamp(health + amount, 0, stats.maxHealth);
            }
        }
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
            OnPlayerDamaged?.Invoke(this);
            if (health <= 0) yield break; // prevents Die() to be called multiple times
            yield return new WaitForSeconds(timeStep);
            timeToDecay -= timeStep;
        }
    }

    public void Die()
    {
        FormManager formManager = characterController.formManager;
        if (!characterController.formManager.IsSpirit)
        {
            Debug.Log("Body died");
            Animator animator = GetComponentInChildren<Animator>();
            gameObject.tag = "";
            animator.SetTrigger("Death");

            if (!formManager.IsOut)
            {
                Debug.Log("Body was in use, spawning spirit");
                formManager.ToggleSpiritForm();
            }
            else
            {
                Debug.Log("Body was NOT in use");
            }
            
            formManager.currentForm.formModelPrefab.transform.SetParent(transform.parent);
            OnPlayerKilled?.Invoke(this);
            PhotonNetwork.Destroy(gameObject);
        }
        else
        {
            GetComponent<CapsuleCollider>().enabled = false;
            Debug.Log("spirit died");
            if (photonView.IsMine) 
            {
                PhotonNetwork.Instantiate("Prefabs/PlayerTomb", transform.position, Quaternion.identity);
                formManager.DisableAbilities();
                Heal(maxHealth, false); // restore health for eventual revive
                PhotonNetwork.Destroy(gameObject);
            }
        }
        // Overwrite
    }
}
