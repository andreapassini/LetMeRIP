using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu]
public class PlayerStats : ScriptableObject
{
	public string formName; // meglio non usare name come nome di variabile
	public float health;
	public float maxHealth;
	public float spiritGauge;
	public float maxSpiritGauge;
	public int strength;
	public int dexterity;
	public int intelligence;

	public float defense;
	public float swiftness;

	public float critDamage;
	public float critChance;

}
