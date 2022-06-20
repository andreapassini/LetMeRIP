using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HealingPool : MonoBehaviour
{
    private float healAmount;
    private HashSet<PlayerController> players;
    private float healingOffset = 0.25f;
    private float lifeTime = 8f;
    private float healTick;

    private Coroutine healingCoroutine;
    public void Init(float healAmount)
    {
        this.healAmount = healAmount;
        players = new HashSet<PlayerController>();
        Destroy(gameObject, 9.5f); // hardcoded time woo (it refers to the particle effect duration)
        healTick = (healAmount / lifeTime) * healingOffset;
        Debug.Log($"BULLET HEAL AMOUNT: {healAmount} | HEAL TICK: {healTick}");

        healingCoroutine = StartCoroutine(HealingIteration());
    }

    private void OnTriggerEnter(Collider other)
    {
        if (players == null) return;

        if (other.CompareTag("Player"))
        {
            players.Add(other.GetComponent<PlayerController>());
            Debug.Log($"{other.name} in");
        }        
    }

    private void OnTriggerExit(Collider other)
    {
        if (players == null) return;

        if (other.CompareTag("Player"))
        {
            players.Remove(other.GetComponent<PlayerController>());
            Debug.Log($"{other.name} out");
        }
    }

    private IEnumerator HealingIteration()
    {
        StartCoroutine(HealingDuration());
        for(;;)
        {
            foreach(PlayerController player in players)
            {
                player.HPManager.Heal(healTick);
                Debug.Log($"{player.name} Healed of {healTick}");
            }
            yield return new WaitForSeconds(healingOffset);
        }
    }
    
    private IEnumerator HealingDuration()
    {
        yield return new WaitForSeconds(8f);
        StopCoroutine(healingCoroutine);
    }
}
