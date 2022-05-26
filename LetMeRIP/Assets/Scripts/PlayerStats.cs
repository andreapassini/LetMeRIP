using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu]
public class PlayerStats : ScriptableObject
{
	public string name;
	public float health;
	public float maxHealth;
	public float spiritGauge;
	public float maxSpiritGauge;
	public float strenght;
	public float dexterity;
	public float intelligence;

	public float defense;
	public float agility;

	public float critDamage;
	public float critChance;

}
