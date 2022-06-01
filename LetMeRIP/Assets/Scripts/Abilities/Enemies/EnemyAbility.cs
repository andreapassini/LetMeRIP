using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class EnemyAbility : ScriptableObject
{
	public string abilityName;

	public float coolDown;

	public float damage;

	[System.NonSerialized]
	public EnemyForm enemy;

	private float previousAbilityTime;

	public virtual void StartAbility(EnemyForm enemy) { 
		if(previousAbilityTime+coolDown > Time.time) {
			return;
		}

		this.enemy = enemy;
		previousAbilityTime = Time.time;
	}
	

	public virtual void PerformAbility()
	{
		if (previousAbilityTime + coolDown > Time.time) {
			return;
		}
	}


	public virtual void CancelAbility()
	{
		if (previousAbilityTime + coolDown > Time.time) {
			return;
		}
	}

}
