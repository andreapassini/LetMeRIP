using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "EnemyAbilities/LightAttack1")]
public class LightAttack1 : EnemyAbility
{
	public float range = 2f;

	private void OnEnable()
	{
		Boss.OnEnemyLightAttack1 += PerformAbility;
	}

	private void OnDisable()
	{
		Boss.OnEnemyLightAttack1 -= PerformAbility;
	}

	public override void StartAbility(EnemyForm enemy)
	{
		base.StartAbility(enemy);

		base.StartAbility(enemy);

		if (previousAbilityTime + coolDown > Time.time) {
			return;
		}

		enemy.animator.SetTrigger("LA1");

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

			if(hpManager != null) 
			{
				hpManager.TakeDamage(damage + enemy.enemyStats.attack, enemy.transform.position);
			}
		}

	}
	public override void CancelAbility()
	{
		throw new System.NotImplementedException();
	}
}
