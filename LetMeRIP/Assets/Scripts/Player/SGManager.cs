using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SGManager : MonoBehaviour
{
    public static event Action<SGManager> OnSPConsumed;
    public static event Action<SGManager> OnSPUnavailable;

    private PlayerStats stats;
    public PlayerStats Stats
    {
        get => stats;
        set
        {
            stats = value;
            spiritGauge = value.spiritGauge;
        }
    }

    private CharacterController characterController;

    private float spiritGauge;

    void Start()
    {
        gameObject.GetComponent<CharacterController>();
    }

    /**
     * Tries to consume Spirit points, if it succeeds returns true, false otherwise.
     * it returns true if the amount of spirit points left is greater or equal than amount
     * if ignoreMissingPoints is true then any missing points to consume are ignored and true is returned.
     */
    public bool ConsumeSP(float amount, bool ignoreMissingPoints = false)
    {
        if (spiritGauge >= amount)
        {
            spiritGauge -= amount;
            OnSPConsumed?.Invoke(this);
            return true;
        }
        else if (ignoreMissingPoints)
        {
            spiritGauge = 0;
            OnSPConsumed?.Invoke(this);
            return true;
        }
        OnSPUnavailable?.Invoke(this);
        return false;
    }
}
