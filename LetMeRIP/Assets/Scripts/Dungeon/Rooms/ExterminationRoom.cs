using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExterminationRoom : Room
{
    private bool cleared = false;
    protected RoomSpawner spawners;
    private IEnumerator closeGatesCoroutine;
    protected override void Awake()
    {
        base.Awake();
        spawners = gameObject.GetComponentInChildren<RoomSpawner>();
    }

    protected override void Start()
    {
        base.Start();
        spawners.OnAllEnemiesCleared += RoomCompletion;
    }

    protected override void Init()
    {
        base.Init();
        if (!PhotonNetwork.IsMasterClient) return; // it just means that this gets executed just once, and it'll be from the master
        if (!cleared)
        {
            spawners.Init();
            spawners.Spawn();
            closeGatesCoroutine = CloseGates(3f);
            StartCoroutine(closeGatesCoroutine); 
        }
    }

    protected override void Exit()
    {
        cleared = true;
        base.Exit();
    }

    private IEnumerator CloseGates(float time)
    {
        CloseInnerGates();
        // signal gates that are going to close
        yield return new WaitForSeconds(time);
        CloseOuterGates();
    }

    private void RoomCompletion(RoomSpawner spawner)
    {
        StopCoroutine(closeGatesCoroutine); // prevents getting stuck in the room if the players are able to clear it within 3 seconds
        OpenInnerGates();
        OpenOuterGates();
        Debug.Log("ROOM CLEARED");
    }
}
