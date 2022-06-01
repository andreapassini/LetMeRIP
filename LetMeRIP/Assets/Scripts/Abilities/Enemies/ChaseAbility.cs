using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[CreateAssetMenu(menuName = "EnemyAbilities/Chase")]
public class ChaseAbility : EnemyAbility
{
	public override void CancelAbility()
	{
		throw new System.NotImplementedException();
	}

	public override void PerformAbility()
	{
		throw new System.NotImplementedException();
	}

	public override void StartAbility(EnemyForm enemy)
	{
		base.StartAbility(enemy);

		enemy.GetComponent<NavMeshAgent>().isStopped = false;
		enemy.GetComponent<NavMeshAgent>().destination = enemy.target.position;
	}
}
