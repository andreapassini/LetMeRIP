using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class HudController : MonoBehaviour
{
    public static HudController Instance;

    [SerializeField] private FillingBar spiritHealth;
    [SerializeField] private FillingBar bodyHealth;
    [SerializeField] private FillingBar spiritGauge;

    private GameObject currentAbilities;
    [SerializeField] protected GameObject baseAbilities;
    [SerializeField] protected GameObject transform1Abilities;
    [SerializeField] protected GameObject transform2Abilities;

    private HudStatusController statusController;

    private bool firstFormChange = true;

    private void Awake()
    {
        Instance = this;
    }

    public void Init(string playerClass, FormManager formManager)
    {
        statusController = playerClass switch
        {
            _ => gameObject.AddComponent<HudWarriorStatusController>()
        };

        // Debug.Log("ELO");
        // Debug.Log("form manager " + formManager);
        
        // (Form initialForm, Dictionary<EAbility, Ability> initialAbilities) = PrepareAbilities(formManager);
        
        // statusController.Init(initialForm, initialAbilities);

        // formManager.OnFormChanged += newFormManager => SwitchAbilities(newFormManager.currentForm.GetType().Name);
        formManager.OnFormChanged += newFormManager =>
        {
            // Form newForm = newFormManager.currentForm.GetType().Name switch
            // {
            //     "SampleForm1" => Form.Trans1,
            //     "SampleForm2" => Form.Trans2,
            //     _ => Form.Trans1
            // };
            //
            // Dictionary<string, Ability> ah = newFormManager.currentForm.abilityHandler.abilities;
            // Dictionary<string, Ability> ah2 = newFormManager.sharedAbilityHandler.abilities;
            //
            // Dictionary<EAbility, Ability> abilities = new Dictionary<EAbility, Ability>();
            // foreach (var entry in ah)
            // {
            //     Enum.TryParse(entry.Key, out EAbility ability);
            //     abilities[ability] = entry.Value;
            // }
            //
            // abilities[EAbility.Dash] = ah2["Dash"];
            //
            // var output = String.Join(", ", abilities.Select(res => "Key " + res.Key + ": VAL = " + res.Value));
            // Debug.Log(output);


            (Form form, Dictionary<EAbility, Ability> abilities) = PrepareAbilities(newFormManager);

            if (firstFormChange)
            {
                statusController.Init(form, abilities);
                firstFormChange = false;
            } else statusController.changeForm(form, abilities);
        };
    }

    private (Form form, Dictionary<EAbility, Ability> abilities) PrepareAbilities(FormManager formManager)
    {
        Form newForm = formManager.currentForm.GetType().Name switch
        {
            "SampleForm1" => Form.Trans1,
            "SampleForm2" => Form.Trans2,
            _ => Form.Trans1
        };

        Debug.Log("new form: " + formManager.currentForm.abilityHandler.abilities);
        
        Dictionary<string, Ability> formAbilities = formManager.currentForm.abilityHandler.abilities;
        Dictionary<string, Ability> sharedAbilities = formManager.sharedAbilityHandler.abilities;
        
        var abilities = new Dictionary<EAbility, Ability>();
        foreach (var entry in formAbilities) abilities[EnumUtils.FromString<EAbility>(entry.Key)] = entry.Value;
        foreach (var entry in sharedAbilities) abilities[EnumUtils.FromString<EAbility>(entry.Key)] = entry.Value;
        
        return (newForm, abilities);
    }

    public void setSpiritMaxHealth(int maxHealth) => spiritHealth.SetMaxValue(maxHealth);
    public void setBodyMaxHealth(int maxHealth) => bodyHealth.SetMaxValue(maxHealth);
    public void setMaxSpiritGauge(int maxSpirits) => spiritGauge.SetMaxValue(maxSpirits);

    public void setSpiritHealth(int health) => spiritHealth.SetValue(health);
    public void setBodyHealth(int health) => spiritHealth.SetValue(health);
    public void setSpiritGauge(int spirits) => spiritHealth.SetValue(spirits);
}