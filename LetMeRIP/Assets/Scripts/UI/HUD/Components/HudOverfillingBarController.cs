using System.Collections.Generic;
using UnityEngine;

public class HudOverfillingBarController : MonoBehaviour
{
    private List<HudFillingBar> bars;
    private float maxValue;
    private bool hasOverflown;

    private void Awake()
    {
        bars = new List<HudFillingBar>();    
    }
    
    private void Start()
    {
        foreach (Transform bar in transform)
        {
            Debug.Log("CIAAO dal loop");
            bars.Add(bar.GetComponent<HudFillingBar>());
        }
    }

    public void Init(float maxValue, float initialValue)
    {
        Debug.Log($"MaxValue: {maxValue}, Initial Value: {initialValue}");
        
        this.maxValue = maxValue;

        SetMaxValue(maxValue);
        SetValue(initialValue);
    }

    private void SetMaxValue(float maxValue)
    {
        foreach (var bar in bars)
        {
            Debug.Log($"bar: {bar}");
        }
        
        
        bars.ForEach(_ => _.Init(maxValue, 0));
    }

    public void SetValue(float value)
    {
        Debug.Log($"New Value: {value}");

        
        foreach (var bar in bars)
        {
            if (value >= maxValue)
            {
                bar.SetValue(maxValue);
                value -= maxValue;
            }
            else
            {
                bar.SetValue(value);
                break;
            }
        }
    }
}