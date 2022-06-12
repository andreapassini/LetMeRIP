using UnityEngine;

public class PlayerBillboard : Billboard
{
    private Transform barsContainer;
    private HudFillingBar sgBar;
    private GameObject hidables;
    private RectTransform border;

    protected override void Awake()
    {
        camera = Camera.main.transform;
        barsContainer = transform.Find("BarsContainer");
        
        healthBar = barsContainer.Find("HealthBar").GetComponent<HudFillingBar>();
        hidables = barsContainer.Find("Hidable").gameObject;
        sgBar = hidables.transform.Find("SGBar").GetComponent<HudFillingBar>();
        border = barsContainer.Find("Border").GetComponent<RectTransform>();
    }
    
    public void Init(PlayerController pc)
    {
        var hpManager = pc.HPManager;
        var sgManager = pc.SGManager;
        var formManager = pc.formManager;

        if (pc.formManager.IsSpirit)
        {
            healthBar.SetMaxValue(pc.spiritStats.maxHealth);
            healthBar.SetValue(hpManager.Health);
            sgBar.SetMaxValue(pc.spiritStats.maxSpiritGauge);
            sgBar.SetValue(sgManager.SpiritGauge);
            
            hpManager.OnPlayerDamaged += manager => healthBar.SetValue(manager.Health);
            hpManager.OnPlayerHealed += manager => healthBar.SetValue(manager.Health);
            sgManager.OnSPGained += manager => sgBar.SetValue(manager.SpiritGauge);
            sgManager.OnSPConsumed += manager => sgBar.SetValue(manager.SpiritGauge);

            return;
        }
        
        // healthBar.SetMaxValue(pc.formManager.IsSpirit ? pc.spiritStats.maxHealth : pc.bodyStats.maxHealth);
        healthBar.SetMaxValue(pc.currentStats.maxHealth);
        // healthBar.SetValue(hpManager.Health);
        sgBar.SetMaxValue(pc.currentStats.maxSpiritGauge);
        ToggleSpiritGauge(formManager);
        // sgBar.SetValue(sgManager.SpiritGauge);
        
        hpManager.OnPlayerDamaged += manager => healthBar.SetValue(manager.Health);
        hpManager.OnPlayerHealed += manager => healthBar.SetValue(manager.Health);
        sgManager.OnSPGained += manager => sgBar.SetValue(manager.SpiritGauge);
        sgManager.OnSPConsumed += manager => sgBar.SetValue(manager.SpiritGauge);
        formManager.OnBodyExit += ToggleSpiritGauge;
        

        // if(formManager.IsSpirit)
        MoveBar(formManager);
        formManager.OnFormChanged += MoveBar;
    }

    private void ToggleSpiritGauge(FormManager formManager)
    {
        Debug.LogError($"IS OUT: {formManager.IsOut}");
        if (formManager.IsOut)
        {
            hidables.SetActive(false);
            border.offsetMin = new Vector2(border.offsetMin.x, 17.5f);
            barsContainer.localPosition -= new Vector3(0, 17.50f, 0);
        }
        else
        {
            hidables.SetActive(true);
            border.offsetMin = new Vector2(border.offsetMin.x, 0);
            barsContainer.localPosition += new Vector3(0, 17.50f, 0);
            sgBar.SetValue(formManager.GetComponent<PlayerController>().SGManager.SpiritGauge);
        }
    }


    private void MoveBar(FormManager formManager)
    {
        var newY = formManager.currentForm.GetType().Name switch
        {
            "WarriorBasic" => 120,
            "Berserker" => 210,
            _ => 0
        };
        if (newY is 0) return;

        barsContainer.localPosition = new Vector3(0, newY, 0);
        // healthBar.transform.transform.localPosition = new Vector3(0, newY, 0);
    }
}