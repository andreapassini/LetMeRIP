using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
		//if (base.previousAbilityTime + coolDown > Time.time) {
		//	return;
		//}

		this.enemy = enemy;

	}

	public virtual void PerformAbility(EnemyForm enemy)
    {
		enemy.CastAbilityDuration(this);
		previousAbilityTime = Time.time;
	}

	public abstract void CancelAbility();


}
