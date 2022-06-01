using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public event Action<EnemySpawner> OnEnemySpawned;

    [SerializeField] private GameObject enemyPrefab;
    /*[HideInInspector]*/ public List<EnemyForm> spawnedEnemies = new List<EnemyForm>();
    private RoomSpawner container;

    private void Start()
    {
        container = gameObject.GetComponentInParent<RoomSpawner>();
    }

    public void Spawn() 
    {
        GameObject enemy = Instantiate(enemyPrefab, transform.position, transform.rotation);
        spawnedEnemies.Add(enemy.GetComponent<EnemyForm>());
        OnEnemySpawned?.Invoke(this);
    }
}
