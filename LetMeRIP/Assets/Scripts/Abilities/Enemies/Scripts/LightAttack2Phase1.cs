using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "EnemyAbilities/LightAttack2Phase1")]
public class LightAttack2Phase1 : EnemyAbility
{
	public float range = 2f;

	private void OnEnable()
	{
		Boss.OnEnemyLightAttack2Phase1 += PerformAbility;
	}

	private void OnDisable()
	{
		Boss.OnEnemyLightAttack2Phase1 -= PerformAbility;
	}

	public override void StartAbility(EnemyForm enemy)
	{
		base.StartAbility(enemy);

		if (previousAbilityTime + coolDown > Time.time) {
			return;
		}

		enemy.animator.SetTrigger("LA2Phase1");

		enemy.navMeshAgent.velocity = Vector3.zero;
		enemy.navMeshAgent.isStopped = true;

		// Look at target
		LookAtTarget();

		base.PerformAbility(enemy);
	}

	public override void PerformAbility(EnemyForm enemy)
	{
		base.PerformAbility(enemy);

		// Creates overlap sphere
		Collider[] hitEnemies = Physics.OverlapSphere(enemy.attackPoint.position, range, enemy.whatIsTarget);

		// Check for collision
		foreach (Collider e in hitEnemies) {
			Debug.Log("Hit this guy: " + e.name);

			// HPManager
			// Anche eventi

			HPManager hpManager = e.transform.GetComponent<HPManager>();

			if (hpManager != null) {
				hpManager.TakeDamage(damage + enemy.enemyStats.attack, enemy.transform.position);
			}
		}

	}
	public override void CancelAbility()
	{
		throw new System.NotImplementedException();
	}
}
