using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[CreateAssetMenu(menuName = "EnemyAbilities/SimpleAttack")]
public class SimpleAttackAbility : EnemyAbility
{
	public override void CancelAbility()
	{
		throw new System.NotImplementedException();
	}

	public override void PerformAbility()
	{
        // Create Collider
        Collider[] hitEnemies = Physics.OverlapSphere(enemy.attackPoint.position, enemy.attackRange, enemy.whatIsTarget);

        // Check for collision
        foreach (Collider e in hitEnemies) {
            Debug.Log("Hit this guy: " + e.name);

            //PlayerHealth playerHealth = enemy.gameObject.GetComponent<PlayerHealth>();

            //if (playerHealth != null) {
            //    playerHealth.TakeDamage(enemy.enemyStats.attack, enemy.transform.position);
            //}
        }
    }

	public override void StartAbility(EnemyForm enemy)
	{
        base.StartAbility(enemy);

        // Stop Moving
        enemy.GetComponent<NavMeshAgent>().isStopped = true;
        enemy.animator.SetTrigger("attack");

        // Look at Target
        enemy.transform.LookAt(new Vector3(enemy.target.position.x, enemy.transform.position.y, enemy.target.position.z), Vector3.up);

        // Play attack animation

        base.PerformAbility();
    }
}
