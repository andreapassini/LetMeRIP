using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "EnemyAbilities/RangedAttack")]
public class RangedAttackAbility : EnemyAbility
{
	public int numberOfBullets = 1;
	public GameObject bulletPrefab;
	public float bulletForce;

	public override void CancelAbility()
	{
		throw new System.NotImplementedException();
	}

	public override void PerformAbility()
	{
		throw new System.NotImplementedException();
	}

	public override void StartAbility(EnemyForm enemy)
	{
		base.StartAbility(enemy);

		//Look at Target
		// Maybe better to use RigidBody and use Slerp for a smoother rotation
		enemy.transform.LookAt(new Vector3(enemy.target.position.x, enemy.transform.position.y, enemy.target.position.z), Vector3.up);

		for (int i=0; i < numberOfBullets; i++) {
			// Fire Bullet
			GameObject bulletFired = Instantiate(bulletPrefab, enemy.attackPoint.position, enemy.attackPoint.rotation);

			Rigidbody rbBullet = bulletFired.GetComponent<Rigidbody>();
			rbBullet.AddForce(enemy.attackPoint.forward * bulletForce, ForceMode.Impulse);
		}

	}
}
