using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[CreateAssetMenu(menuName = "EnemyAbilities/Search")]
public class SearchAbility : EnemyAbility
{
	private Vector3 searchDestination;

	public override void StartAbility(EnemyForm enemy)
	{
		base.StartAbility(enemy);

		enemy.navMeshAgent.isStopped = false;

		if(enemy.target == null) {
			enemy.targets = GameObject.FindGameObjectsWithTag(enemy.targetTag);
			enemy.target = enemy.targets[0].transform;
		}

		if (searchDestination == null 
			|| Vector3.Distance(enemy.transform.position, searchDestination) < 4)
        {
			searchDestination = RandomNavmeshLocation(20f, enemy.transform.position);
		}

		// Go to a random new pos on the Navmesh
		enemy.navMeshAgent.destination = searchDestination;

		float distance = float.MaxValue;

		foreach (GameObject t in enemy.targets) {
			float calculatedDistance = (t.transform.position - enemy.transform.position).magnitude;
			if (calculatedDistance < distance) {
				distance = calculatedDistance;
				enemy.target = t.transform;
			}
		}

		base.PerformAbility(this.enemy);
	}

	public override void PerformAbility(EnemyForm enemy)
	{
	}

	public override void CancelAbility()
	{

	}

	public static Vector3 RandomNavmeshLocation(float radius, Vector3 position)
	{
		while (true) {
			Vector3 randomDirection = UnityEngine.Random.insideUnitSphere * radius;
			randomDirection += position;
			UnityEngine.AI.NavMeshHit hit;

			if (UnityEngine.AI.NavMesh.SamplePosition(randomDirection, out hit, radius, 1)) {
				return hit.position;
			}
		}
	}
}
