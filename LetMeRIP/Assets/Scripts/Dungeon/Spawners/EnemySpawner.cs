using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private GameObject enemyPrefab;
    public List<EnemyCanvas> spawnedEnemies = new List<EnemyCanvas>();

    public void Spawn() 
    {
        GameObject enemy = Instantiate(enemyPrefab, transform.position, transform.rotation);
        spawnedEnemies.Add(enemy.GetComponent<EnemyCanvas>());
    }
}
