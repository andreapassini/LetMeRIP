using UnityEngine;
using UnityEngine.UI;

public class HudFillingBar : MonoBehaviour
{
    [SerializeField] private Slider slider;
    [SerializeField] private Gradient gradiant;
    [SerializeField] private Image fill;

    public void Start()
    {
        slider = gameObject.GetComponent<Slider>();
        fill = gameObject.transform.Find("Fill").gameObject.GetComponent<Image>();
    }


    public void SetMaxValue(int health)
    {
        slider.maxValue = health;
        slider.value = health; //??

        fill.color = gradiant.Evaluate(1f);
    }

    public void SetValue(int health)
    {
        slider.value = health;
        fill.color = gradiant.Evaluate(slider.normalizedValue);
    }
}