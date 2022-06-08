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

	public override void PerformAbility(EnemyForm enemy)
	{
        // Create Collider
        Collider[] hitEnemies = Physics.OverlapSphere(enemy.attackPoint.position, enemy.attackRange, enemy.whatIsTarget);

        // Check for collision
        foreach (Collider e in hitEnemies) {
            Debug.Log("Hit this guy: " + e.name);

            // HPManager
            // Anche eventi

            HPManager hpManager = e.transform.GetComponent<HPManager>();

            if (hpManager != null) {
                hpManager.TakeDamage(damage, enemy.transform.position);
            }
        }
    }

	public override void StartAbility(EnemyForm enemy)
	{
        base.StartAbility(enemy);

        if (previousAbilityTime + coolDown > Time.time) {
            return;
        }

        enemy.animator.SetTrigger("attack");

        // Stop Moving  
        enemy.navMeshAgent.velocity = Vector3.zero;
        enemy.navMeshAgent.isStopped = true;

        // Look at Target
        enemy.transform.LookAt(new Vector3(enemy.target.position.x, enemy.transform.position.y, enemy.target.position.z), Vector3.up);

        // Play attack animation

        base.PerformAbility(this.enemy);
    }
}
