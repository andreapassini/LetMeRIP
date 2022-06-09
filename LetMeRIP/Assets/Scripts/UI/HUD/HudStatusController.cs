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
    public void Init(Form initialForm, Dictionary<EAbility, Ability> abilities)
    {
        abilityManager.Init(initialForm, abilitiesSprites, abilities);
        formManager.Init(initialForm, formsSprites);
    }


    // deals with changing the current form selected and the shown abilities 
    public void changeForm(Form newForm, Dictionary<EAbility, Ability> abilities)
    {
        abilityManager.changeAbilities(newForm, abilities);
        formManager.changeForm(newForm);
    }
}