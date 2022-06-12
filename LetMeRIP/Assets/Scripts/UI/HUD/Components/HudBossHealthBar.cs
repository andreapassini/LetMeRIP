using UnityEngine;
using UnityEngine.UI;

public class HudBossHealthBar : HudFillingBar
{
    protected override void Awake()
    {
        slider = gameObject.GetComponent<Slider>();
        fill = gameObject.transform.Find("Fill").gameObject.GetComponent<Image>();
        gameObject.SetActive(false);
    }

    public void Hide() => gameObject.SetActive(false);


    public void Init(float maxHealth)
    {
        Init(maxHealth, maxHealth);
        gameObject.SetActive(true);
    }
}