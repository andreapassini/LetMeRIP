using UnityEngine;
using System.Collections;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]

public class ChaseWithLOS : MonoBehaviour {

	public Transform destination;
	public float resampleTime = 5f;

	void Start () {
		StartCoroutine (GoChasing());
	}

	private IEnumerator GoChasing() {
		while (true) {
			Vector3 ray = destination.position - transform.position;
			RaycastHit hit;
			if (Physics.Raycast (transform.position, ray, out hit)) {
				if (hit.transform == destination) {
					GetComponent<NavMeshAgent> ().destination = destination.position;
				}
			}
			yield return new WaitForSeconds (resampleTime);
		}
	}
	
}
