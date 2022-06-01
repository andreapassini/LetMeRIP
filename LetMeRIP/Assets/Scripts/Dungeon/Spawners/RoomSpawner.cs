using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoomSpawner : MonoBehaviour
{
    [SerializeField] private readonly List<EnemySpawner> spawners;
    private int enemyCount = 0;

    public void Init()
    {
        foreach(EnemySpawner spawner in spawners)
        {
            spawner.Spawn();

        }
    }
}
