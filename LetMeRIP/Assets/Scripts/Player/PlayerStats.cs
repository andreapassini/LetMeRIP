using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu]
public class PlayerStats : ScriptableObject
{
	public string formName;
	public float health;
	public float maxHealth;
	public float spiritGauge;
	public float maxSpiritGauge;
	public float strength;
	public float dexterity;
	public float intelligence;

	public float defense;
	public float swiftness;

	public float critDamage;
	public float critChance;

}
