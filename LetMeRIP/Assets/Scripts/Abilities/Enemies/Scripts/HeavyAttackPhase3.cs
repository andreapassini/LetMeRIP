using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "EnemyAbilities/HeavyAttackPhase3")]
public class HeavyAttackPhase3 : EnemyAbility
{
	public float range = 2f;

	private void OnEnable()
	{
		Boss.OnEnemyHeavyAttackPhase3 += PerformAbility;
	}

	private void OnDisable()
	{
		Boss.OnEnemyHeavyAttackPhase3 -= PerformAbility;
	}

	public override void StartAbility(EnemyForm enemy)
	{
		base.StartAbility(enemy);

		if (previousAbilityTime + coolDown > Time.time) {
			return;
		}

		enemy.animator.SetTrigger("HAPhase3");

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
		Collider[] hitEnemies = Physics.OverlapSphere(enemy.attackPoint.position, range);

		// Check for collision
		foreach (Collider e in hitEnemies) {

			if (e.CompareTag("Player")) {
				HPManager hpManager = e.transform.GetComponent<HPManager>();

				if (hpManager != null) {
					hpManager.TakeDamage(damage + enemy.enemyStats.attack, enemy.transform.position);
				}	
			}			
		}

	}
	public override void CancelAbility()
	{
		throw new System.NotImplementedException();
	}
}
