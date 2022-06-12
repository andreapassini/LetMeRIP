using UnityEngine;

public class PlayerBillboard : Billboard
{
    private RectTransform barsContainer;
    private HudFillingBar sgBar;
    private GameObject hidables;
    private RectTransform border;

    // the vertical offset to adjust the bars when the spirit gauge is hidden
    private float verticalOffset;

    protected override void Awake()
    {
        camera = Camera.main.transform;
        barsContainer = transform.Find("BarsContainer").GetComponent<RectTransform>();
        
        healthBar = barsContainer.Find("HealthBar").GetComponent<HudFillingBar>();
        hidables = barsContainer.Find("Hidable").gameObject;
        sgBar = hidables.transform.Find("SGBar").GetComponent<HudFillingBar>();
        border = barsContainer.Find("Border").GetComponent<RectTransform>();

        verticalOffset = barsContainer.sizeDelta.y / 2;
    }
    
    public void Init(PlayerController pc)
    {
        var hpManager = pc.HPManager;
        var sgManager = pc.SGManager;
        var formManager = pc.formManager;

        if (pc.formManager.IsSpirit)
        {
            healthBar.Init(pc.playerManager.spiritStats.maxHealth, pc.playerManager.spiritStats.health);
            sgBar.Init(pc.playerManager.spiritStats.maxSpiritGauge, pc.playerManager.spiritStats.spiritGauge);
            
            
            hpManager.OnPlayerDamaged += manager => healthBar.SetValue(manager.Health);
            hpManager.OnPlayerHealed += manager => healthBar.SetValue(manager.Health);
            sgManager.OnSPGained += manager => sgBar.SetValue(manager.SpiritGauge);
            sgManager.OnSPConsumed += manager => sgBar.SetValue(manager.SpiritGauge);

            return;
        }
        
        healthBar.Init(pc.playerManager.bodyStats.maxHealth, pc.playerManager.bodyStats.health);
        sgBar.Init(pc.playerManager.bodyStats.maxSpiritGauge, pc.playerManager.bodyStats.spiritGauge);
        
        ToggleSpiritGauge(formManager);

        hpManager.OnPlayerDamaged += manager => healthBar.SetValue(manager.Health);
        hpManager.OnPlayerHealed += manager => healthBar.SetValue(manager.Health);
        sgManager.OnSPGained += manager => sgBar.SetValue(manager.SpiritGauge);
        sgManager.OnSPConsumed += manager => sgBar.SetValue(manager.SpiritGauge);
        formManager.OnBodyExit += ToggleSpiritGauge;
        
        MoveBars(formManager);
        formManager.OnFormChanged += MoveBars;
    }

    private void ToggleSpiritGauge(FormManager formManager)
    {
        Debug.LogError($"ID: {formManager.ViewID}, IsOut: {formManager.IsOut}");
        
        if (formManager.IsOut)
        { // Hide spirit gauge
            hidables.SetActive(false);
            border.offsetMin = new Vector2(border.offsetMin.x, verticalOffset);
            barsContainer.localPosition -= new Vector3(0, verticalOffset, 0);
        }
        else
        { // Show spirit gauge
            hidables.SetActive(true);
            border.offsetMin = new Vector2(border.offsetMin.x, 0);
            barsContainer.localPosition += new Vector3(0, verticalOffset, 0);
            sgBar.SetValue(formManager.GetComponent<PlayerController>().SGManager.SpiritGauge);
        }
    }


    private void MoveBars(FormManager formManager)
    {
        // TODO change if more forms
        var newY = formManager.currentForm.GetType().Name switch
        {
            "WarriorBasic" => 120,
            "Berserker" => 210,
            _ => 0
        };
        
        if (newY is 0) return;
        barsContainer.localPosition = new Vector3(0, newY, 0);
    }
}