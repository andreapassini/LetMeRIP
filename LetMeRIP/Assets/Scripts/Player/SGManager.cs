using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SGManager : MonoBehaviour
{
    public static event Action<SGManager> OnSPConsumed;
    public static event Action<SGManager> OnSPUnavailable;

    public PlayerStats stats;

    private PlayerController characterController;

    public float SpiritGauge { get => stats.spiritGauge; }
    public float MaxSpiritGauge { get => stats.maxSpiritGauge; }

    void Start()
    {
        gameObject.GetComponent<PlayerController>();
    }

    /**
     * Tries to consume Spirit points, if it succeeds returns true, false otherwise.
     * it returns true if the amount of spirit points left is greater or equal than amount
     * if ignoreMissingPoints is true then any missing points to consume are ignored and true is returned.
     */
    public bool ConsumeSP(float amount, bool ignoreMissingPoints = false)
    {
        if (stats.spiritGauge >= amount)
        {
            stats.spiritGauge -= amount;
            OnSPConsumed?.Invoke(this);
            return true;
        }
        else if (ignoreMissingPoints)
        {
            stats.spiritGauge = 0;
            OnSPConsumed?.Invoke(this);
            return true;
        }
        OnSPUnavailable?.Invoke(this);
        return false;
    }

    public void AddSP(float amount)
    {
        stats.spiritGauge = Mathf.Clamp(stats.spiritGauge + amount, 0, stats.maxSpiritGauge);
    }
}
