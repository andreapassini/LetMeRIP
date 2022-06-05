using UnityEngine;

[CreateAssetMenu(menuName = "EnemyAbilities/DashBack")]
public class DashBackAbility : EnemyAbility
{
	public float dashForce = 100f;

	public float dashDuration = 1.5f;

	public override void CancelAbility()
	{
	}

	public override void PerformAbility()
	{
	}

	public override void StartAbility(EnemyForm enemy)
	{
		base.StartAbility(enemy);

		if (base.previousAbilityTime + coolDown > Time.time) {
			return;
		}

		// Disable Navmesh
		enemy.navMeshAgent.enabled = false;
		// Disable isKinematic
		enemy.rb.isKinematic = false;

		Vector3 dashBackDirection = enemy.transform.forward * -1;
		// Perform Dash Back
		enemy.rb.AddForce(dashBackDirection * dashForce, ForceMode.Impulse);

		base.PerformAbility();
	}
}
