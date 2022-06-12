using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class BeaconOfHope : MonoBehaviourPun
{
	public float areaOfEffect;

	private float damageRise;
	private float damageDuring;

	private bool isUp = false;

	private Collider collider;

	// Player and if it is inside
	private List<Transform> playersBuffed;

    public void Init(float intelligence)
	{
		damageRise = (float)(30 + (0.3 * intelligence));
		damageDuring = (float)(10 + (0.2 * intelligence));

		if (PhotonNetwork.IsMasterClient) StartCoroutine(DestroyAfterTime(5f));

		RiseUP();
	}

	private void Start()
	{
		collider.isTrigger = true;
	}

	private void OnCollisionStay(Collision e)
	{
		if (isUp) 
		{
			if (e.transform.CompareTag("Enemy")) {

				EnemyForm enemyForm = e.transform.GetComponent<EnemyForm>();

				if (enemyForm != null) {
					enemyForm.TakeDamage(damageDuring);
				}
			}
		}
	}

	private void OnCollisionEnter(Collision e)
	{
		if (isUp) {
			if (e.transform.CompareTag("Player")) {
				//Buff
				PlayerController p = e.transform.GetComponent<PlayerController>();

				if (!playersBuffed.Contains(p.transform)) {

					p.GetComponent<HPManager>().BuffStats(
					0.1f,
					0.1f,
					0.15f,
					3f
					);

					playersBuffed.Add(p.transform);
					PlayerBuffWaitToGive(p.transform);
				}
			}
		}
	}

	public void RiseUP()
	{
		Collider[] hitEnemies = Physics.OverlapSphere(transform.position, areaOfEffect);

		// Check for collision
		foreach (Collider e in hitEnemies) {
			if (e.CompareTag("Enemy")) {

				EnemyForm enemyForm = e.transform.GetComponent<EnemyForm>();

				if (enemyForm != null) {
					enemyForm.TakeDamage(damageRise);
				}
			}
		}

		isUp = true;
	}

	private IEnumerator PlayerBuffWaitToGive(Transform player)
	{
		// I dont want to keep giving buff inside, just wait for buff cooldown
		yield return new WaitForSeconds(3f);

		playersBuffed.Remove(player);
			
	}

	private IEnumerator DestroyAfterTime(float lifeTime)
	{
		yield return new WaitForSeconds(lifeTime);
		PhotonNetwork.Destroy(gameObject);
	}
}
