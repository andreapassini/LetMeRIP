using Photon.Pun;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SGManager : MonoBehaviourPun
{
    public event Action<SGManager> OnSPGained;
    public event Action<SGManager> OnSPConsumed;
    public event Action<SGManager> OnSPUnavailable;

    public PlayerController.Stats stats;

    private PlayerController characterController;

    private float spiritGauge { get => stats.spiritGauge; set => stats.spiritGauge = value; }
    private float maxSpiritGauge { get => stats.maxSpiritGauge; set => stats.maxSpiritGauge = value; }
    public float SpiritGauge { get => stats.spiritGauge; }
    public float MaxSpiritGauge { get => stats.maxSpiritGauge; }

    void Start()
    {
        gameObject.GetComponent<PlayerController>();
    }

    public void Init(float startingSpiritGauge)
    {
        spiritGauge = startingSpiritGauge;
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

    public void AddSP(float amount)
    {
        if (PhotonNetwork.IsMasterClient)
            photonView.RPC("RpcAddSP", RpcTarget.All, amount);

    }

    [PunRPC]
    public void RpcAddSP(float amount)
    {
        spiritGauge = Mathf.Clamp(spiritGauge + amount, 0, maxSpiritGauge);
        OnSPGained?.Invoke(this);
    }
}
