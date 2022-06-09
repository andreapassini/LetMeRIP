using System;
using System.Collections.Generic;
using UnityEngine;

public class HudAbilityManager : MonoBehaviour
{
    //holds the gameobject for each ability
    private Dictionary<EAbility, GameObject> hudAbilitiesGO;

    private void Awake()
    {
        //init the dictionary with all the abilities' gameobjects
        hudAbilitiesGO = new Dictionary<EAbility, GameObject>();

        foreach (string abilityName in Enum.GetNames(typeof(EAbility)))
        {
            Enum.TryParse(abilityName, out EAbility ability);
            Transform abilityTransform = transform.Find(abilityName);

            if (abilityTransform == null) throw new Exception("The ability " + abilityName + " is not available");

            hudAbilitiesGO[ability] = abilityTransform.gameObject;
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