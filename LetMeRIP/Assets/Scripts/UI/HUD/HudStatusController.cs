using System;
using System.Collections.Generic;
using UnityEngine;


public abstract class HudStatusController : MonoBehaviour
{
    protected Dictionary<Form, Dictionary<EAbility, Sprite>> abilitiesSprites;
    protected Dictionary<Form, Sprite> formsSprites;

    [SerializeField] protected AbilityManager abilityManager;
    [SerializeField] protected HudFormManager formManager;

    public void Awake()
    {
        abilityManager = GetComponentInChildren<AbilityManager>();
        formManager = GetComponentInChildren<HudFormManager>();
    }


    // deals with changing the current form selected and the shown abilities 
    public void changeForm(Form newForm, Dictionary<EAbility, Ability> abilities)
    {
        var t = new Dictionary<EAbility, (Sprite sprite, Ability ability)>();

        var newSprites = abilitiesSprites[newForm];

        foreach (var sprite in newSprites)
        {
            var eAbility = sprite.Key;
            
            if(!abilities.ContainsKey(eAbility)) continue;
            
            t[eAbility] = (sprite.Value, abilities[eAbility]);
        }

        abilityManager.changeAbilities(t);
        formManager.changeForm(newForm);
    }
}