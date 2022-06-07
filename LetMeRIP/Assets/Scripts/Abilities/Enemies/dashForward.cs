using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "EnemyAbilities/DashForward")]
public class DashForward : EnemyAbility
{
    public float dashForce = 100f;

    public float dashDuration = 1.5f;

	public override void StartAbility(EnemyForm enemy)
	{
		base.StartAbility(enemy);

		if (base.previousAbilityTime + coolDown > Time.time) {
			return;
		}

		enemy.animator.SetTrigger("dash");

		// Disable Navmesh
		enemy.navMeshAgent.enabled = false;
		// Disable isKinematic
		enemy.rb.isKinematic = false;

		// Perform Dash Back
		enemy.rb.AddForce(enemy.transform.forward * dashForce, ForceMode.Impulse);

		base.PerformAbility(this.enemy);
	}

	public override void CancelAbility()
	{
		throw new System.NotImplementedException();
	}

	public override void PerformAbility(EnemyForm enemy)
	{
	}
}
