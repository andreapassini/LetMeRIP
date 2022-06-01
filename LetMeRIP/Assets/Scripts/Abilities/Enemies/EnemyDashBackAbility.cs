using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "EnemyAbilities/DashBack")]
public class EnemyDashBackAbility : EnemyAbility
{
	public override void CancelAbility()
	{
		// Enable isKinematic
		// Enable Navmesh
		// Disable collisions
	}

	public override void PerformAbility()
	{
		throw new System.NotImplementedException();
	}

	public override void StartAbility(EnemyForm enemy)
	{
		// Disable Navmesh
		enemy.navMeshAgent.enabled = false;
		// Disable isKinematic
		enemy.rb.isKinematic = false;
		// Enable collisions
		enemy.rb.detectCollisions = true;

		Vector3 dashBackDirection = new Vector3(enemy.transform.forward.magnitude * -1, enemy.transform.position.y, enemy.transform.position.z);
		// Perform Dash Back
		enemy.rb.AddRelativeForce(dashBackDirection, ForceMode.Impulse);
	}
}
