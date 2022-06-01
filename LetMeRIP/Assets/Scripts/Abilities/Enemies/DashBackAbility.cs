using UnityEngine;

[CreateAssetMenu(menuName = "EnemyAbilities/DashBack")]
public class DashBackAbility : EnemyAbility
{
	public float dashForce = 100f;

	public float dashDuration = 1.5f;
	private float dashTime = 0;
	private bool dashReady = true;

	public override void CancelAbility()
	{
	}

	public override void PerformAbility()
	{
	}

	public override void StartAbility(EnemyForm enemy)
	{
		if (base.previousAbilityTime + coolDown > Time.time) {
			return;
		}

		Debug.Log("Dashing");

		dashTime = Time.time + dashDuration;

		Vector3 dashBackDirection = new Vector3(enemy.transform.forward.magnitude * -1 * (dashForce*dashForce), enemy.transform.position.y, enemy.transform.position.z);
		// Perform Dash Back
		enemy.rb.AddRelativeForce(dashBackDirection, ForceMode.Impulse);
	}
}
