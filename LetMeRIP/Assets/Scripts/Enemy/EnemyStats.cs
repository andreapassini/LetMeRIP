using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu]
public class EnemyStats : ScriptableObject
{
	public string enemyName;
	public float health;
	public float maxHealth;

	public float attack;
	public float defense;

	public float swiftness;
	public float rewardSp;
}
