using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[CreateAssetMenu(menuName = "EnemyAbilities/Spawn")]
public class Spawn : EnemyAbility
{
	public string[] prefabPath;
	public int[] numberOfEnemies;

	RoomSpawner r = new RoomSpawner();

	public override void StartAbility(EnemyForm enemy)
	{
		r.Init();

		base.StartAbility(enemy);

		Vector3 enemyCenter = new Vector3(
			enemy.transform.position.x,
			0,
			enemy.transform.position.z);

		// Generate Spawn Points
		for(int i=0; i<prefabPath.Length; i++) {
			// Some Randomness
			float xOffset = Random.Range(0, 0.25f);
			float zOffset = Random.Range(0, 0.25f);

			if (i%2 == 0) 
			{
				xOffset += 2f;
			}
			else {
				xOffset += -2f;
			}

			if(i/2 < 1) {
				zOffset += 2f;
			} else {
				zOffset += -2f;
			}

			Vector3 spawnPoint = new Vector3(
				enemyCenter.x + xOffset,
				0,
				enemyCenter.y + zOffset);

			EnemySpawner toSpawn = new EnemySpawner();
			toSpawn.enemyPrefabPath = prefabPath[i];
			toSpawn.transform.position = spawnPoint;
			toSpawn.transform.rotation = Quaternion.identity;

			NavMeshHit hit;

			// Check If the point is inside the Map
			if (NavMesh.SamplePosition(spawnPoint, out hit, 1f, 1)) {
				// Spawn Tentacles
				for(int j=0; j < numberOfEnemies[i]; j++) {
					r.spawners.Add(toSpawn);
				}
				
			}

		}


		r.Spawn();
		// Use an Event to kill them afterwards
	}

	public override void PerformAbility(EnemyForm enemy)
	{
		base.PerformAbility(enemy);
	}

	public override void CancelAbility()
	{
		throw new System.NotImplementedException();
	}
}
