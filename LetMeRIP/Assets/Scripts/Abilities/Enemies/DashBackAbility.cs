using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "EnemyAbilities/DashBack")]
public class DashBackAbility : EnemyAbility
{
	public float dashForce = 10f;

	public override void CancelAbility()
	{
	}

	public override void PerformAbility()
	{
		// Enable isKinematic
		enemy.rb.isKinematic = true;
		enemy.rb.detectCollisions = false; // Double check this in test
										   // Enable Navmesh
		enemy.navMeshAgent.enabled = true;
		// Disable collisions	
	}

	public override void StartAbility(EnemyForm enemy)
	{
		// Disable Navmesh
		enemy.navMeshAgent.enabled = false;
		// Disable isKinematic
		enemy.rb.isKinematic = false;
		// Enable collisions
		enemy.rb.detectCollisions = true;

		Vector3 dashBackDirection = new Vector3(enemy.transform.forward.magnitude * -1 * dashForce, enemy.transform.position.y, enemy.transform.position.z);
		// Perform Dash Back
		enemy.rb.AddRelativeForce(dashBackDirection, ForceMode.Impulse);
	}
}
