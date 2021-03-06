using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class HudController : MonoBehaviour
{
    public static HudController Instance;

    private HudStatusController statusController;

    [SerializeField] private HudMessageManager messageManager;

    [SerializeField] private HudFillingBar spiritHealth;
    [SerializeField] private HudOverfillingBarController bodyHealth;
    [SerializeField] private HudFillingBar spiritGauge;

    [SerializeField] private HudRoomTimer roomTimer;
    [SerializeField] private HudBossHealthBar bossHealth;

    private void Awake() => Instance = this;

    public void InitPlayerInfoBar(string playerClass, PlayerController pc)
    {
        //return;

        var formManager = pc.formManager;
        var hpManager = pc.HPManager;
        var sgManager = pc.SGManager;

        if (statusController is null) InitStatusController(playerClass);

        // Only spirit
        if (formManager.IsSpirit)
        {
            ChangeForm(formManager);

            hpManager.OnPlayerDamaged += manager => spiritHealth.SetValue(manager.Health);
            hpManager.OnPlayerHealed += manager => spiritHealth.SetValue(manager.Health);
            sgManager.OnSPGained += manager => spiritGauge.SetValue(manager.SpiritGauge);
            sgManager.OnSPConsumed += manager => spiritGauge.SetValue(manager.SpiritGauge);
            return;
        }

        // Only Body
        (HudEForm initialForm, Dictionary<HudEAbility, Ability> initialAbilities) = PrepareAbilities(formManager);
        statusController.Init(initialForm, initialAbilities);

        spiritHealth.Init(pc.playerManager.spiritStats.maxHealth, pc.playerManager.spiritStats.health);
        bodyHealth.Init(pc.playerManager.bodyStats.maxHealth, pc.playerManager.bodyStats.health);
        spiritGauge.Init(pc.playerManager.bodyStats.maxSpiritGauge, pc.playerManager.bodyStats.spiritGauge);
        
        formManager.OnFormChanged += ChangeForm;
        hpManager.OnPlayerDamaged += manager => bodyHealth.SetValue(manager.Health);
        hpManager.OnPlayerHealed += manager => bodyHealth.SetValue(manager.Health);
        sgManager.OnSPConsumed += manager => spiritGauge.SetValue(manager.SpiritGauge);
    }

    public void InitRoomTimer(float time) => roomTimer.Init(time);
    public void HideTimer() => roomTimer.Hide();

    public void InitBossHealth(EnemyForm enemyForm)
    {
        bossHealth.Init(enemyForm.enemyStats.maxHealth);
        enemyForm.OnEnemyDamaged += form => bossHealth.SetValue(form.enemyStats.health);
    }

    public void HideBossHealth() => bossHealth.Hide();

    // temporary, to attach to the button
    public void PostMessage(string text) => messageManager.PostMessage(text, 2f);
    public void PostMessage(string text, float ttl = 2f) => messageManager.PostMessage(text, ttl);

    private void InitStatusController(string playerClass)
    {
        statusController = playerClass switch
        {
            _ => gameObject.AddComponent<HudWarriorStatusController>()
        };
    }

    private void ChangeForm(FormManager formManager)
    {
        (HudEForm form, Dictionary<HudEAbility, Ability> abilities) = PrepareAbilities(formManager);
        statusController.changeForm(form, abilities);
    }

    private (HudEForm form, Dictionary<HudEAbility, Ability> abilities) PrepareAbilities(FormManager formManager)
    {
        HudEForm newHudEForm = formManager.currentForm.GetType().Name switch
        {
            "SampleForm1" => HudEForm.Trans1,
            "SampleForm2" => HudEForm.Trans2,
            "WarriorBasic" => HudEForm.Base,
            "Berserker" => HudEForm.Trans1,
            "SpiritForm" => HudEForm.Spirit,
            _ => HudEForm.Trans1
        };

        Debug.Log("new hudEForm: " + formManager.currentForm.abilityHandler.abilities);

        Dictionary<string, Ability> formAbilities = formManager.currentForm.abilityHandler.abilities;
        Dictionary<string, Ability> sharedAbilities = formManager.sharedAbilityHandler.abilities;

        var abilities = new Dictionary<HudEAbility, Ability>();
        foreach (var entry in formAbilities) abilities[EnumUtils.FromString<HudEAbility>(entry.Key)] = entry.Value;
        abilities[HudEAbility.Dash] = sharedAbilities["Dash"];

        Debug.Log(String.Join(", ", abilities.Select(res => "Key " + res.Key + ": VAL = " + res.Value)));

        return (newHudEForm, abilities);
    }

    public void Hide() => gameObject.SetActive(false);
    public void Show() => gameObject.SetActive(true);
}