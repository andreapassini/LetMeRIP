using System.Collections.Generic;
using UnityEngine;

public abstract class HudStatusController : MonoBehaviour
{
    protected Dictionary<HudEForm, Dictionary<HudEAbility, Sprite>> abilitiesSprites;
    protected Dictionary<HudEForm, Sprite> formsSprites;

    [SerializeField] protected HudAbilityManager abilityManager;
    [SerializeField] protected HudFormManager formManager;

    protected void Awake()
    {
        abilityManager = GetComponentInChildren<HudAbilityManager>();
        formManager = GetComponentInChildren<HudFormManager>();
    }

    // initialize the abilities shown and the selected hudEForm
    public void Init(HudEForm initialHudEForm, Dictionary<HudEAbility, Ability> abilities)
    {
        abilityManager.Init(initialHudEForm, abilitiesSprites, abilities);
        formManager.Init(initialHudEForm, formsSprites);
    }


    // deals with changing the current hudEForm selected and the shown abilities 
    public void changeForm(HudEForm newHudEForm, Dictionary<HudEAbility, Ability> abilities)
    {
        abilityManager.changeAbilities(newHudEForm, abilities);
        formManager.changeForm(newHudEForm);
    }
}