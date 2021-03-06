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

	public override void PerformAbility(EnemyForm enemy)
	{
		throw new System.NotImplementedException();
	}

	public override void StartAbility(EnemyForm enemy)
	{
		base.StartAbility(enemy);

		enemy.navMeshAgent.isStopped = false;
		enemy.navMeshAgent.destination = enemy.target.position;

		base.PerformAbility(this.enemy);
	}
}
