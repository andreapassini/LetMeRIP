using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[CreateAssetMenu(menuName = "EnemyAbilities/Search")]
public class SearchAbility : EnemyAbility
{
	public override void StartAbility(EnemyForm enemy)
	{
		this.enemy = enemy;

		float distance = float.MaxValue;

		foreach (GameObject t in enemy.targets) {
			float calculatedDistance = (t.transform.position - enemy.transform.position).magnitude;
			if (calculatedDistance < distance) {
				distance = calculatedDistance;
				enemy.target = t.transform;
			}
		}

		// Go to a random new pos on the Navmesh
		enemy.GetComponent<NavMeshAgent>().isStopped = false;
		enemy.GetComponent<NavMeshAgent>().destination = RandomNavmeshLocation(10f, enemy.transform.position);
	}

	public override void PerformAbility()
	{
		throw new System.NotImplementedException();
	}

	public override void CancelAbility()
	{
		throw new System.NotImplementedException();
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
