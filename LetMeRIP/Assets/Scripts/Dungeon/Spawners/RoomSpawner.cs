using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoomSpawner : MonoBehaviour
{
    [SerializeField] private List<EnemySpawner> spawners;
    public int EnemyCount { get => enemyCount; }
    private int enemyCount = 0;

    public void Init()
    {
        EnemyForm.OnEnemyKilled += eform => enemyCount--;

        foreach (EnemySpawner spawner in spawners)
        {
            spawner.OnEnemySpawned += espawner => enemyCount++;
            spawner.Spawn();
        }
    }

    public void Exit()
    {
        EnemyForm.OnEnemyKilled -= eform => enemyCount--;
    }
}
