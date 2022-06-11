using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AbilityHandler : MonoBehaviour
{
    public Dictionary<string, Ability> abilities; // assegnamo ad ogni abilità una chiave
    private float cooldown = 1f; // cooldown tra un abilità e l'altra
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

        foreach(Ability ability in abilities.Values)
        {
            if (cooldown > ability.cooldown * 0.6f) cooldown = ability.cooldown * 0.6f;
        }
    }

    /**
     * starts to cast the ability, locking the possibility to cast anything else until a cancel ability is called
     */
    public void StartAbility(string key)
    {
        if (abilities.ContainsKey(key) && abilities[key].IsReady && IsReady)
        {
            if(characterController.SGManager.SpiritGauge < abilities[key].SPCost)
            {
                Debug.Log("Not enough SPs");
                return;
            }

            characterController.SGManager.ConsumeSP(abilities[key].SPCost);
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
