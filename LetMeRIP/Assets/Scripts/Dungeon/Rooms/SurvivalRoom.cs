using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SurvivalRoom : Room
{

    private bool cleared = false;
    protected RoomSpawner spawners;

    [SerializeField] private float timeToSurvive = 10;
    [SerializeField] private float respawnOffset = 3f;

    private Coroutine closeGatesCoroutine;
    private Coroutine survivalTimerCoroutine;
    private Coroutine respawnEnemiesCoroutine;
    protected override void Awake()
    {
        base.Awake();
        spawners = gameObject.GetComponentInChildren<RoomSpawner>();
    }

    protected override void Start()
    {
        base.Start();
    }

    protected override void Init()
    {
        base.Init();
        if (!PhotonNetwork.IsMasterClient) return; // it just means that this gets executed just once, and it'll be from the master
        if (!cleared)
        {
            spawners.Init();
            
            closeGatesCoroutine = StartCoroutine(CloseGates(3f));
            photonView.RPC("RpcStartUITimer", RpcTarget.All);
            survivalTimerCoroutine = StartCoroutine(SurvivalTimer());
            respawnEnemiesCoroutine = StartCoroutine(RespawnEnemies(respawnOffset));
        }
    }

    protected override void Exit()
    {
        cleared = true;
        base.Exit();
    }

    [PunRPC]
    public void RpcStartUITimer()
    {
        HudController.Instance.InitRoomTimer(timeToSurvive);
    }

    private IEnumerator CloseGates(float time)
    {
        CloseInnerGates();
        // signal gates that are going to close
        yield return new WaitForSeconds(time);
        CloseOuterGates();
    }

    /**
     * Unlocks the doors and stops the spawn of the enemies
     */
    private void RoomCompletion()
    {
        StopCoroutine(survivalTimerCoroutine);
        StopCoroutine(respawnEnemiesCoroutine);
        StopCoroutine(closeGatesCoroutine);
        spawners.ClearAllEnemies();

        OpenInnerGates();
        OpenOuterGates();
        photonView.RPC("RpcHideUITimer", RpcTarget.All);
        Debug.Log("ROOM CLEARED");
    }
    [PunRPC]
    private void RpcHideUITimer()
    {
        HudController.Instance.HideTimer();
    }

    /**
     * Locks the room for timeToSurvive seconds
     */
    private IEnumerator SurvivalTimer()
    {
        for (;;)
        {
            if(timeToSurvive <= 0)
                RoomCompletion();

            yield return new WaitForSeconds(timeStep);
            timeToSurvive -= timeStep; 
        }
    }

    /**
     * Respawns enemies on the room every offset seconds
     */
    private IEnumerator RespawnEnemies(float offset)
    {
        for (;;)
        {
            spawners.Spawn();
            yield return new WaitForSeconds(offset);
        }
    }


}
