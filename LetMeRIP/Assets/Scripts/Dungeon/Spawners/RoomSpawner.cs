using Photon.Pun;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoomSpawner : MonoBehaviourPun
{
    public event Action<RoomSpawner> OnAllEnemiesCleared; 
    public List<EnemySpawner> spawners;
    public List<GameObject> currentEnemies;

    public void Init()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        photonView.RPC("RpcInit", RpcTarget.MasterClient);
    }

    [PunRPC]
    private void RpcInit()
    {
        EnemyForm.OnEnemyKilled += RemoveEnemy;
        EnemyForm.OnEnemyKilled += ClearanceCheck;
    }

    [PunRPC]
    private void RpcSpawn()
    {
        foreach (EnemySpawner spawner in spawners)
        {
            if (PhotonNetwork.IsMasterClient)
                currentEnemies.Add(PhotonNetwork.Instantiate(spawner.enemyPrefabPath, spawner.transform.position, spawner.transform.rotation));
        }
    }

    /**
     * spawns every enemy setted in spawners
     */
    public void Spawn()
    {
        if (!PhotonNetwork.IsMasterClient) return; // ew
        photonView.RPC("RpcSpawn", RpcTarget.MasterClient);
    }

    public void Exit()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        EnemyForm.OnEnemyKilled -= RemoveEnemy;
        EnemyForm.OnEnemyKilled -= ClearanceCheck;
    }

    /**
     * Removes the killed enemy from the list of alive enemies
     */
    private void RemoveEnemy(EnemyForm eform)
    {
        if (PhotonNetwork.IsMasterClient)
            currentEnemies.Remove(eform.gameObject);
    } 

    /**
     * Checks if any enemy is alive, if not then OnAllEnemiesCleared is invoked
     */
    private void ClearanceCheck(EnemyForm eform) 
    {
        if (currentEnemies.Count <= 0) 
            OnAllEnemiesCleared?.Invoke(this);
    }

    public void ClearAllEnemies()
    {
        try
        {
            foreach (GameObject enemy in currentEnemies)
                PhotonNetwork.Destroy(enemy); // if you want to use Die, remember to prepare a list of EnemyForm components otherwise it throws an exception
        } catch(InvalidOperationException e)
        {
            StartCoroutine(RetryAfterSeconds(ClearAllEnemies, .1f));
        }
    }

    private IEnumerator RetryAfterSeconds(Action function, float time)
    {
        yield return new WaitForSeconds(time);
        function();
    }
}
