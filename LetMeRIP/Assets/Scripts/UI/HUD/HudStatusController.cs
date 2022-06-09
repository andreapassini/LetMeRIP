using System;
using System.Collections.Generic;
using UnityEngine;


public abstract class HudStatusController : MonoBehaviour
{
    protected Dictionary<Form, Dictionary<EAbility, Sprite>> abilitiesSprites;
    protected Dictionary<Form, Sprite> formsSprites;

    [SerializeField] protected HudAbilityManager abilityManager;
    [SerializeField] protected HudFormManager formManager;

    protected void Awake()
    {
        abilityManager = GetComponentInChildren<HudAbilityManager>();
        formManager = GetComponentInChildren<HudFormManager>();
    }
    
    // initialize the abilities shown and the selected form
    public void Init(Form form, Dictionary<EAbility, Ability> abilities)
    {
        abilityManager.Init(createDictionaryForAbilityManager(form, abilities));
        formManager.Init(formsSprites, Form.Base);
    }
    
    
    // deals with changing the current form selected and the shown abilities 
    public void changeForm(Form newForm, Dictionary<EAbility, Ability> abilities)
    {
        abilityManager.changeAbilities(createDictionaryForAbilityManager(newForm, abilities));
        formManager.changeForm(newForm);
    }

    private Dictionary<EAbility, (Sprite sprite, Ability ability)> createDictionaryForAbilityManager(Form form, Dictionary<EAbility, Ability> abilities)
    {
        var result = new Dictionary<EAbility, (Sprite sprite, Ability ability)>();

        var spritesForNewForm = abilitiesSprites[form];

        foreach (var sprite in spritesForNewForm)
        {
            // the ability associated to the sprite
            var eAbility = sprite.Key;
            // check if an ability with the same name was passed, if not we skip the sprite
            if(!abilities.ContainsKey(eAbility)) continue;
            
            result[eAbility] = (sprite.Value, abilities[eAbility]);
        }
        
        return result;
    }
    
    
}