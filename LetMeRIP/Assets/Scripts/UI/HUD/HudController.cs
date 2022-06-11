using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Photon.Realtime;
using UnityEngine;

public class HudController : MonoBehaviour
{
    public static HudController Instance;

    [SerializeField] private HudFillingBar spiritHealth;
    [SerializeField] private HudFillingBar bodyHealth;
    [SerializeField] private HudFillingBar spiritGauge;

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

    
    // private IEnumerator Wait(string playerClass, FormManager formManager)
    // {
    //     // some vfx to cover this late init
    //     yield return new WaitForSeconds(5f);
    //     LInit( playerClass, formManager);
    // }
    //
    //
    // public void Init(string playerClass, FormManager formManager)
    // {
    //     StartCoroutine(Wait(playerClass, formManager));
    //     
    // }
    
    public void Init(string playerClass, FormManager formManager)
    {
        if (statusController is null) InitStatusController(playerClass);
        
        // funziona solo se lo spirit non può essere la prima forma 
        if (!formManager.IsSpirit)
        {
            Debug.Log("Init non spirit");
            
            // Debug.LogError("Initializing ...");

            // Debug.Log("ELO");
            // Debug.Log("form manager " + formManager);
            
            (HudEForm initialForm, Dictionary<HudEAbility, Ability> initialAbilities) = PrepareAbilities(formManager);
            statusController.Init(initialForm, initialAbilities);
            
            
            
            formManager.OnFormChanged += newFormManager =>
            {
                Debug.Log("Form changed");
                (HudEForm form, Dictionary<HudEAbility, Ability> abilities) = PrepareAbilities(newFormManager);

                // if (firstFormChange)
                // {
                //     firstFormChange = false;
                //     return;
                //     statusController.Init(form, abilities);
                //     // Debug.Log("WORKINGGG ");
                //     firstFormChange = false;a
                // }
                // else statusController.changeForm(form, abilities);
                statusController.changeForm(form, abilities);
            };
        }
        else
        {
            // Debug.LogError("Initializing spirit ... ...");
            
            (HudEForm form, Dictionary<HudEAbility, Ability> abilities) = PrepareAbilities(formManager);
            statusController.changeForm(form, abilities);
        }
    }

    private void InitStatusController(string playerClass)
    {
        statusController = playerClass switch
        {
            _ => gameObject.AddComponent<HudWarriorStatusController>()
        };
    }
    
    

    private (HudEForm form, Dictionary<HudEAbility, Ability> abilities) PrepareAbilities(FormManager formManager)
    {
        // Debug.LogError("Initializing form: " +  formManager.currentForm.GetType().Name);
        
        HudEForm newHudEForm = formManager.currentForm.GetType().Name switch
        {
            "SampleForm1" => HudEForm.Trans1,
            "SampleForm2" => HudEForm.Trans2,
            "WarriorBasic" => HudEForm.Trans1,
            "Berserker" => HudEForm.Trans2,
            "SpiritForm" => HudEForm.Spirit,
            // ""
            _ => HudEForm.Trans1
        };

        Debug.Log("new hudEForm: " + formManager.currentForm.abilityHandler.abilities);

        Dictionary<string, Ability> formAbilities = formManager.currentForm.abilityHandler.abilities;
        Dictionary<string, Ability> sharedAbilities = formManager.sharedAbilityHandler.abilities;

        var abilities = new Dictionary<HudEAbility, Ability>();
        foreach (var entry in formAbilities) abilities[EnumUtils.FromString<HudEAbility>(entry.Key)] = entry.Value;
        foreach (var entry in sharedAbilities) abilities[EnumUtils.FromString<HudEAbility>(entry.Key)] = entry.Value;

        Debug.Log( String.Join(", ", abilities.Select(res => "Key " + res.Key + ": VAL = " + res.Value)));
        
        return (newHudEForm, abilities);
    }

    public void setSpiritMaxHealth(int maxHealth) => spiritHealth.SetMaxValue(maxHealth);
    public void setBodyMaxHealth(int maxHealth) => bodyHealth.SetMaxValue(maxHealth);
    public void setMaxSpiritGauge(int maxSpirits) => spiritGauge.SetMaxValue(maxSpirits);

    public void setSpiritHealth(int health) => spiritHealth.SetValue(health);
    public void setBodyHealth(int health) => spiritHealth.SetValue(health);
    public void setSpiritGauge(int spirits) => spiritHealth.SetValue(spirits);
}