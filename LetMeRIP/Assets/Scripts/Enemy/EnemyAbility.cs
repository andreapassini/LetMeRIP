using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu]
public class EnemyAbility : ScriptableObject
{
	public string abilityName;

	public float coolDown;

	public float damage;

	private EnemyForm enemy;

	public virtual void StartAbility(EnemyForm enemyForm)
	{
		this.enemy = enemyForm;
	}

	public virtual void PerformAbility(EnemyForm enemyForm)
	{
		this.enemy = enemyForm;
	}

	public virtual void CancelAbility(EnemyForm enemyForm)
	{
		this.enemy = enemyForm;
	}

}
