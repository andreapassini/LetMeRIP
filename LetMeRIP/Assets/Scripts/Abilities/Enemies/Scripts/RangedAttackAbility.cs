using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "EnemyAbilities/RangedAttack")]
public class RangedAttackAbility : EnemyAbility
{
	public int numberOfBullets = 1;
	public GameObject bulletPrefab;
	public float bulletForce;

    private void OnEnable()
    {
		EnemyForm.OnEnemyAttack += PerformAbility;
    }

    private void OnDisable()
    {
		EnemyForm.OnEnemyAttack -= PerformAbility;
	}

	public override void CancelAbility()
	{
	}

	public override void PerformAbility(EnemyForm enemy)
	{
		if(this.enemy == enemy)
        {
			for (int i = 0; i < numberOfBullets; i++)
			{
				// Fire Bullet
				GameObject bulletFired = Instantiate(bulletPrefab, enemy.attackPoint.position, enemy.attackPoint.rotation);

				Rigidbody rbBullet = bulletFired.GetComponent<Rigidbody>();
				rbBullet.AddForce(enemy.attackPoint.forward * bulletForce, ForceMode.Impulse);
			}
		}
	}

	public override void StartAbility(EnemyForm enemy)
	{
		base.StartAbility(enemy);

		if (previousAbilityTime + coolDown > Time.time)
		{
			return;
		}

		enemy.animator.SetTrigger("attack");

		enemy.navMeshAgent.velocity = Vector3.zero;
		enemy.navMeshAgent.isStopped = true;

		// Look at Target
		// Maybe better to use RigidBody and use Slerp for a smoother rotation
		enemy.transform.LookAt(new Vector3(enemy.target.position.x, enemy.transform.position.y, enemy.target.position.z), Vector3.up);

		

		base.PerformAbility(enemy);

	}
}
