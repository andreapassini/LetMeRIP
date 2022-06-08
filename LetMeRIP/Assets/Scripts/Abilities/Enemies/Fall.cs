using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Fall : EnemyAbility
{
	public override void StartAbility(EnemyForm enemy)
	{
		base.StartAbility(enemy);

		if (base.previousAbilityTime + coolDown > Time.time) {
			return;
		}

		enemy.animator.SetTrigger("fall");

		// Disable Collider
		enemy.GetComponent<Collider>().enabled = false;
		enemy.navMeshAgent.destination = enemy.transform.position;
		enemy.navMeshAgent.velocity = Vector3.zero;

		base.PerformAbility(this.enemy);
	}

	public override void PerformAbility(EnemyForm enemy)
	{
	}

	public override void CancelAbility()
	{
		throw new System.NotImplementedException();
	}
}
