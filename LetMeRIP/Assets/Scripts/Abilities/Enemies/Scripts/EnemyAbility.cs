using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public abstract class EnemyAbility : ScriptableObject
{
	public string abilityName;

	public float coolDown;
	public float abilityDurtation;

	public float damage;

	[System.NonSerialized]
	public EnemyForm enemy;

	[System.NonSerialized]
	public float previousAbilityTime = 0;

	public virtual void StartAbility(EnemyForm enemy) 
	{
		Debug.Log(this);
		//if (!PhotonNetwork.IsMasterClient) return;
		this.enemy = enemy;
		enemy.CastEnemyAbility(this);
	}

	public virtual void PerformAbility(EnemyForm enemy)
    {
		//if (!PhotonNetwork.IsMasterClient) return;
		enemy.CastAbilityDuration(this);
		previousAbilityTime = Time.time;
	}

	public abstract void CancelAbility();

	public virtual void LookAtTarget()
	{
		// Look at the target
		Vector3 direction = (enemy.target.position - enemy.transform.position).normalized;
		Quaternion lookRotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
		enemy.rb.rotation = Quaternion.Slerp(enemy.rb.rotation, lookRotation, Time.deltaTime * 20f);

	}
}
