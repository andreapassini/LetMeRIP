using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HudFillingBar : MonoBehaviour
{
    public Slider slider;
    public Image fill;
    public Gradient gradient;

    protected virtual void Awake()
    {
        slider = gameObject.GetComponent<Slider>();
        fill = gameObject.transform.Find("Mask").Find("Fill").gameObject.GetComponent<Image>();
    }
    
    public void Init(float maxHealth, float initialHealth)
    {
        SetMaxValue(maxHealth);
        SetValue(initialHealth, false);
    }


    public void SetMaxValue(float health, bool setToMax = false)
    {
        slider.maxValue = health;
        if (setToMax) slider.value = health;

        fill.color = gradient.Evaluate(1f);
    }

    public void SetValue(float health, bool flash = true)
    {
        // Debug.LogError($"New value: {health}");
        slider.value = health;
        fill.color = gradient.Evaluate(1f);
        if (flash) StartCoroutine(Flash());
    }

    private IEnumerator Flash()
    {
        fill.color = gradient.Evaluate(0);
        yield return new WaitForSeconds(0.15f);
        fill.color = gradient.Evaluate(1f);
    }
}