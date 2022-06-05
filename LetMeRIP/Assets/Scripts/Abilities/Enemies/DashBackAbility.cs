using UnityEngine;

[CreateAssetMenu(menuName = "EnemyAbilities/DashBack")]
public class DashBackAbility : EnemyAbility
{
	public float dashForce = 10f;

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

		base.PerformAbility();

		enemy.navMeshAgent.enabled = false;
		enemy.rb.isKinematic = false;

		Vector3 dashBackDirection = new Vector3(
			enemy.transform.position.x * -1 * (dashForce * dashForce),
			enemy.transform.position.y,
			enemy.transform.position.z);

		Vector3 oppositeDir = enemy.transform.forward * -1;

		// Perform Dash Back
		enemy.rb.AddForce(oppositeDir * dashForce, ForceMode.Impulse);

	}
}
