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

		Vector3 dashBackDirection = new Vector3(enemy.transform.position.x * -1 * (dashForce*dashForce), enemy.transform.position.y, enemy.transform.position.z);
		// Perform Dash Back
		enemy.rb.AddRelativeForce(dashBackDirection, ForceMode.Impulse);

		base.PerformAbility();
	}
}
