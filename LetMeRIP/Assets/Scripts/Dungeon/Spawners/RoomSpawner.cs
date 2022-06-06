using Photon.Pun;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoomSpawner : MonoBehaviourPun
{
    public event Action<RoomSpawner> OnAllEnemiesCleared; 
    [SerializeField] private List<EnemySpawner> spawners;
    public int EnemyCount { get => enemyCount; }
    private int enemyCount = 0;


    public void Init()
    {
        photonView.RPC("Spawn", RpcTarget.All);
    }

    [PunRPC]
    private void Spawn()
    {
        EnemyForm.OnEnemyKilled += eform => enemyCount--;
        EnemyForm.OnEnemyKilled += eform => { if (enemyCount <= 0) OnAllEnemiesCleared?.Invoke(this); };

        foreach (EnemySpawner spawner in spawners)
        {
            if(PhotonNetwork.IsMasterClient)
                PhotonNetwork.Instantiate(spawner.enemyPrefabPath, spawner.transform.position, spawner.transform.rotation);
            
            enemyCount++;
        }
    }



    public void Exit()
    {
        EnemyForm.OnEnemyKilled -= eform => enemyCount--;
    }
}
