using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[CreateAssetMenu(menuName = "EnemyAbilities/CreateVulnerableSign")]
public class CreateVulnerableSign : EnemyAbility
{
	public GameObject prefab;

	public override void StartAbility(EnemyForm enemy)
	{
		base.StartAbility(enemy);

		Vector3 enemyCenter = new Vector3(
			enemy.transform.position.x,
			0,
			enemy.transform.position.z);

		// Generate Spawn Points
		for (int i = 0; i < 4; i++) {

			float xOffset  = 0 , zOffset = 0;

			if (i == 0) {
				xOffset = 0;
				zOffset = 1;
			}
			else if (i == 1) {
				xOffset = 1;
				zOffset = 0;
			} 
			else if( i == 2) {
				xOffset = 0;
				zOffset = -1;
			}
			else if(i == 3) {
				xOffset = -1;
				zOffset = 0;
			}

			Vector3 spawnPoint = new Vector3(
				enemyCenter.x + xOffset,
				0,
				enemyCenter.y + zOffset);

			GameObject toSpawn = prefab;
			toSpawn.transform.position = spawnPoint;
			toSpawn.transform.rotation = Quaternion.identity;

			NavMeshHit hit;

			// Check If the point is inside the Map
			if (NavMesh.SamplePosition(spawnPoint, out hit, 1f, 1)) {
				// Spawn Tentacles
				PhotonView.Instantiate(toSpawn);
				return;
			}

		}


		
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
