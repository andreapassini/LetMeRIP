using UnityEngine;
using UnityEngine.UI;

public class HudFillingBar : MonoBehaviour
{
    [SerializeField] protected Slider slider;
    [SerializeField] protected Image fill;
    [SerializeField] private Gradient gradient;

    protected virtual void Awake()
    {
        slider = gameObject.GetComponent<Slider>();
        fill = gameObject.transform.Find("Mask").Find("Fill").gameObject.GetComponent<Image>();
    }

    public void Init(float maxHealth, float initialHealth)
    {
        SetMaxValue(maxHealth);
        SetValue(initialHealth);
    }
    
    
    public void SetMaxValue(float health, bool setToMax = false)
    {
        slider.maxValue = health;
        if(setToMax) slider.value = health;

        fill.color = gradient.Evaluate(1f);
    }

    public void SetValue(float health)
    {
        slider.value = health;
        fill.color = gradient.Evaluate(slider.normalizedValue);
    }
}