using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class EnemyAbility : ScriptableObject
{
	public string abilityName;

	public float coolDown;

	public float damage;

	public EnemyForm enemy;

	public abstract void StartAbility(EnemyForm  enemy);

	public abstract void PerformAbility();


	public abstract void CancelAbility();

}
