using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class BeaconOfHope : MonoBehaviour
{
	public float radius;
	private float duration = 5f;
	private float damageOnSummon;
	private float damageTick;
	private GameObject vfxSpawn;

    private void Awake()
    {
		vfxSpawn = Resources.Load<GameObject>($"Particles/{nameof(BeaconOfHope)}Spawn");
    }

    /**
	 * Calculates the damage scaling on caster's intelligence
	 */
    public void Init(float intelligence)
	{
		damageOnSummon = (float)(30 + (0.3 * intelligence));
		damageTick = (float)(10 + (0.2 * intelligence));

		Destroy(gameObject, duration);
		RiseUP();
	}


	/**
	 * Adds the proper effect to player and enemies entering the beacon of hope area
	 * if a player or enemy enetering already has the effect (it shouldn't) it doesn't add another one
	 * but rather resets its duration
	 */
    private void OnTriggerEnter(Collider other)
    {
		if (other.CompareTag("Player"))
		{
			Debug.Log($"{other.name} entered a beacon");
			//Buff
			PlayerController player = other.GetComponent<PlayerController>();
				
			if(player.TryGetComponent<BeaconOfHopePE>(out BeaconOfHopePE effect))
            {
				effect.ResetDuration();
            } else
            {
				effect = player.gameObject.AddComponent<BeaconOfHopePE>();
				effect.Init(duration);
				effect.StartEffect();
			}
		} else if (other.CompareTag("Enemy") && !other.TryGetComponent<BeaconOfHopeEE>(out BeaconOfHopeEE effect))
        {
			effect = other.gameObject.AddComponent<BeaconOfHopeEE>();
			effect.Init(damageTick, duration);
			effect.StartEffect();
		}
	}

	/**
	 * removes the damaging effect from the exiting enemies
	 */
    private void OnTriggerExit(Collider other)
    {
        if(other.CompareTag("Enemy") && other.TryGetComponent<BeaconOfHopeEE>(out BeaconOfHopeEE effect))
		{
			Destroy(effect);
        }
    }

	/**
	 * Inflicts a higher starting damage to enemies in range 
	 */
    public void RiseUP()
	{
		Collider[] hitEnemies = Physics.OverlapSphere(transform.position, radius);

		vfxSpawn ??= Resources.Load<GameObject>($"Particles/{nameof(BeaconOfHope)}Spawn");
		Destroy(Instantiate(vfxSpawn, transform), 2f);

		// Check for collision
		foreach (Collider e in hitEnemies) {
			if (e.CompareTag("Enemy")) {

				EnemyForm enemyForm = e.transform.GetComponent<EnemyForm>();

				if (enemyForm != null) {
					enemyForm.TakeDamage(damageOnSummon);
				}
			}
		}
	}
}
