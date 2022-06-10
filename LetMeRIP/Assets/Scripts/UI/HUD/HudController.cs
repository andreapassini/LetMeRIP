using System.Collections.Generic;
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

    public void Init(string playerClass, FormManager formManager)
    {
        statusController = playerClass switch
        {
            _ => gameObject.AddComponent<HudWarriorStatusController>()
        };

        // Debug.Log("ELO");
        // Debug.Log("form manager " + formManager);

        // (HudEForm initialForm, Dictionary<HudEAbility, Ability> initialAbilities) = PrepareAbilities(formManager);

        // statusController.Init(initialForm, initialAbilities);

        // formManager.OnFormChanged += newFormManager => SwitchAbilities(newFormManager.currentForm.GetType().Name);
        formManager.OnFormChanged += newFormManager =>
        {
            (HudEForm form, Dictionary<HudEAbility, Ability> abilities) = PrepareAbilities(newFormManager);

            if (firstFormChange)
            {
                statusController.Init(form, abilities);
                firstFormChange = false;
            }
            else statusController.changeForm(form, abilities);
        };
    }

    private (HudEForm form, Dictionary<HudEAbility, Ability> abilities) PrepareAbilities(FormManager formManager)
    {
        HudEForm newHudEForm = formManager.currentForm.GetType().Name switch
        {
            "SampleForm1" => HudEForm.Trans1,
            "SampleForm2" => HudEForm.Trans2,
            _ => HudEForm.Trans1
        };

        Debug.Log("new hudEForm: " + formManager.currentForm.abilityHandler.abilities);

        Dictionary<string, Ability> formAbilities = formManager.currentForm.abilityHandler.abilities;
        Dictionary<string, Ability> sharedAbilities = formManager.sharedAbilityHandler.abilities;

        var abilities = new Dictionary<HudEAbility, Ability>();
        foreach (var entry in formAbilities) abilities[EnumUtils.FromString<HudEAbility>(entry.Key)] = entry.Value;
        foreach (var entry in sharedAbilities) abilities[EnumUtils.FromString<HudEAbility>(entry.Key)] = entry.Value;

        return (newHudEForm, abilities);
    }

    public void setSpiritMaxHealth(int maxHealth) => spiritHealth.SetMaxValue(maxHealth);
    public void setBodyMaxHealth(int maxHealth) => bodyHealth.SetMaxValue(maxHealth);
    public void setMaxSpiritGauge(int maxSpirits) => spiritGauge.SetMaxValue(maxSpirits);

    public void setSpiritHealth(int health) => spiritHealth.SetValue(health);
    public void setBodyHealth(int health) => spiritHealth.SetValue(health);
    public void setSpiritGauge(int spirits) => spiritHealth.SetValue(spirits);
}