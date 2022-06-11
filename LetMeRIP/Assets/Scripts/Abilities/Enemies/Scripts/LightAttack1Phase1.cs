using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "EnemyAbilities/LightAttack1Phase1")]
public class LightAttack1Phase1 : EnemyAbility
{
	public float range = 2f;

	private void OnEnable()
	{
		Boss.OnEnemyLightAttack1Phase1 += PerformAbility;
	}

	private void OnDisable()
	{
		Boss.OnEnemyLightAttack1Phase1 -= PerformAbility;
	}

	public override void StartAbility(EnemyForm enemy)
	{
		base.StartAbility(enemy);

		if (previousAbilityTime + coolDown > Time.time) {
			return;
		}

		enemy.animator.SetTrigger("LA1Phase1");

		//enemy.navMeshAgent.velocity = Vector3.zero;
		//enemy.animator.SetFloat("speed", enemy.rb.velocity.magnitude);
		//enemy.navMeshAgent.isStopped = true;
		//Vector3 v = new Vector3(enemy.transform.position.x,
		//	enemy.transform.position.y,
		//	enemy.transform.position.z);

		//enemy.navMeshAgent.destination = v.normalized;
		//enemy.navMeshAgent.enabled = false;

		// Look at target
		LookAtTarget();

		base.PerformAbility(enemy);
	}

	public override void PerformAbility(EnemyForm enemy)
	{
		base.PerformAbility(enemy);

		// Creates overlap sphere
		// Layer Mask does not work
		//Collider[] hitEnemies = Physics.OverlapSphere(enemy.attackPoint.position, range, enemy.whatIsTarget);

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
