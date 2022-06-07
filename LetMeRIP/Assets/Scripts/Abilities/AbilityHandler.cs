using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AbilityHandler : MonoBehaviour
{
    private Dictionary<string, Ability> abilities; // assegnamo ad ogni abilità una chiave
    private float cooldown = 0.1f; // cooldown tra un abilità e l'altra
    private bool isReady = true;

    private string current = null;
    public bool IsReady { get { return isReady && current == null; } }
    private PlayerController characterController;

    public void Init(Dictionary<string, Ability> abilities, PlayerController characterController)
    {
        this.characterController = characterController;

        this.abilities = new Dictionary<string, Ability>(abilities);
        foreach (Ability ability in abilities.Values)
            ability.Init(characterController);
    }

    /**
     * starts to cast the ability, locking the possibility to cast anything else until a cancel ability is called
     */
    public void StartAbility(string key)
    {
        if (abilities.ContainsKey(key) && abilities[key].IsReady && IsReady)
        {
            abilities[key].StartedAction();
            current = key;
            isReady = false;
            
            //Debug.Log("Started " + current);
        } else
        {
            //Debug.Log($"{current ?? "what the fuck"} is getting cast");
        }
    }

    public void PerformAbility(string key)
    {
        if(current != null && key.Equals(current))
        {
            abilities[key].PerformedAction();
            //Debug.Log("Performed " + current);
        }
    }

    public void CancelAbility(string key)
    {
        if(current != null && key.Equals(current))
        {
            abilities[key].CancelAction();
            current = null;
            StartCoroutine(Cooldown());

            //Debug.Log("Finished " + current);
        }
    }

    public IEnumerator Cooldown() 
    {
        yield return new WaitForSeconds(cooldown);
        isReady = true;
    }
}
