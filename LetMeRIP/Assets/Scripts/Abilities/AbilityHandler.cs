using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AbilityHandler : MonoBehaviour
{
    Dictionary<string, IAbility> abilities; // assegnamo ad ogni abilità una chiave
    private float cooldown = 0.5f; // cooldown tra un abilità e l'altra
    private float currentCooldown = 0;

    private string current = null;
    public bool IsReady { get { return currentCooldown <= 0 && current == null; } }

    public void Init(Dictionary<string, IAbility> abilities)
    {
        this.abilities = new Dictionary<string, IAbility>(abilities);
    }


    /**
     * starts to cast the ability, locking the possibility to cast anything else until a cancel ability is called
     */
    public void StartAbility(string key)
    {
        if(abilities.ContainsKey(key) && abilities[key].IsReady && IsReady)
        {
            current = key;
            abilities[key].StartedAction();
            Debug.Log("Started " + current);
        } else
        {
            Debug.Log($"{current ?? "what the fuck"} is getting cast");
        }
    }

    public void PerformAbility(string key)
    {
        if(current != null && key.Equals(current))
        {
            abilities[key].PerformedAction();
            Debug.Log("Performed " + current);
        }
    }

    public void CancelAbility(string key)
    {
        if(current != null && key.Equals(current))
        {
            abilities[key].CancelAction();
            currentCooldown = cooldown; // preferirei farlo partire alla fine dell'ultima animazione dell'abilità
            current = null;
            StartCoroutine("Cooldown");
            Debug.Log("Finished " + current);
        }
    }

    public static IEnumerator Cooldown(float cooldown) 
    {
        yield return new WaitForSeconds(cooldown);
        cooldown = 0;
    }
}
