using UnityEngine;
using UnityEngine.UI;

public class HudFillingBar : MonoBehaviour
{
    [SerializeField] private Slider slider;
    [SerializeField] private Gradient gradiant;
    [SerializeField] private Image fill;

    public void Awake()
    {
        slider = gameObject.GetComponent<Slider>();
        fill = gameObject.transform.Find("Mask").Find("Fill").gameObject.GetComponent<Image>();
    }


    public void SetMaxValue(float health)
    {
        slider.maxValue = health;
        slider.value = health;

        fill.color = gradiant.Evaluate(1f);
    }

    public void SetValue(float health)
    {
        slider.value = health;
        fill.color = gradiant.Evaluate(slider.normalizedValue);
    }
}