using System;
using System.Collections.Generic;
using UnityEngine;

public class AbilityManager : MonoBehaviour
{
    private Dictionary<EAbility, GameObject> hudAbilitiesGO;
    // private Dictionary<EAbility, (Sprite sprite, Ability ability)> sprites;

    private void Awake()
    {
        // populate abilities
        hudAbilitiesGO = new Dictionary<EAbility, GameObject>();

        foreach (string abilityName in Enum.GetNames(typeof(EAbility)))
        {
            Enum.TryParse(abilityName, out EAbility ability);
            GameObject abilityGO = transform.Find(abilityName).gameObject;

            if (abilityGO == null) throw new Exception("The ability " + abilityName + " is not available");

            hudAbilitiesGO[ability] = abilityGO;
        }
    }

    public void Init(Dictionary<EAbility, (Sprite sprite, Ability ability)> sprites)
    {
        setAbilities(sprites);
    }

    public void changeAbilities(Dictionary<EAbility, (Sprite sprite, Ability ability)> sprites)
    {
        foreach (var ability in hudAbilitiesGO.Values)
            Destroy(ability.GetComponent<HudAbility>());

        setAbilities(sprites);
    }


    private void setAbilities(Dictionary<EAbility, (Sprite sprite, Ability ability)> sprites)
    {
        foreach (var ability in sprites)
            hudAbilitiesGO[ability.Key]
                .AddComponent<HudAbility>()
                .Init(ability.Value.sprite, ability.Value.ability);
    }
}