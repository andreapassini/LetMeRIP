using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "EnemyAbilities/Rise")]
public class Rise : EnemyAbility
{
	public override void StartAbility(EnemyForm enemy)
	{
		base.StartAbility(enemy);

		if (base.previousAbilityTime + coolDown > Time.time) {
			return;
		}

		enemy.animator.SetTrigger("rise");

		// Disable Collider
		enemy.GetComponent<Collider>().enabled = true;
		enemy.navMeshAgent.destination = enemy.target.position;
		//enemy.navMeshAgent.velocity = Vector3.zero;

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
