using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SoundEffectManager : MonoBehaviour
{
	#region Singleton Implementation
	public static RoomManager instance;

	public void Awake()
	{
		if (instance) // check if there's already another room manager
		{
			// if there is destroy this cuz it's not needed
			Destroy(gameObject);
			return;
		}

		DontDestroyOnLoad(gameObject);
		instance = null;
	}
	#endregion

	private void OnEnable()
	{
		// Sub to events
		EnemyForm.OnEnemyTakeDamage += PlayEnemyTakeDmg;
		EnemyForm.OnEnemyKilled += PlayEnemyDeath;
		HPManager.OnPlayerTakeDamage += PlayPlayerTakeDmg;
		HPManager.OnPlayerH += PlayPlayerHeal;
	}

	private void OnDisable()
	{
		// Unsub to events
		EnemyForm.OnEnemyTakeDamage -= PlayEnemyTakeDmg;
		EnemyForm.OnEnemyKilled -= PlayEnemyDeath;
		HPManager.OnPlayerTakeDamage -= PlayPlayerTakeDmg;
		HPManager.OnPlayerH -= PlayPlayerHeal;
	}

	private void PlayPlayerHeal(HPManager obj)
	{
		AudioClip playerHeal = Resources.Load<AudioClip>("Sounds/Toon healing 1");
		AudioSource a = new AudioSource();
		a.clip = playerHeal;
		a.Play();
	}

	private void PlayPlayerTakeDmg(HPManager obj)
	{
		AudioClip playerTakeDmg = Resources.Load<AudioClip>("Sounds/Attack Jump & Hit Damage Human Sounds/Hit & Damage 5");
		AudioSource a = new AudioSource();
		a.clip = playerTakeDmg;
		a.Play();
	}

	private void PlayEnemyDeath(EnemyForm enemy)
	{
		AudioClip enemyDeath = Resources.Load<AudioClip>("Sounds/Attack Jump & Hit Damage Human Sounds/Hit & Damage 11");
		AudioSource a = new AudioSource();
		a.clip = enemyDeath;
		a.Play();
	}

	private void PlayEnemyTakeDmg(EnemyForm enemy)
	{
		AudioClip enemyTakeDmg = Resources.Load<AudioClip>("Sounds/Attack Jump & Hit Damage Human Sounds/Hit & Damage 6");
		AudioSource a = new AudioSource();
		a.clip = enemyTakeDmg;
		a.Play();
	}


}
