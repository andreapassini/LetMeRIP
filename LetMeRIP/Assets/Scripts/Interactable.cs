using UnityEngine;

public class Interactable : MonoBehaviour
{
	public float radius = 3f;   // Distance to interact

	public Transform interactionTransform;

	Transform player;


	private bool hasInteracted;
	private bool isFocus;

	private void Awake()
	{
		if (interactionTransform != null) {
			interactionTransform = transform;
		}
	}

	private void Start()
	{
		// Creates the collider to interact with
		SphereCollider sphereCollider = gameObject.AddComponent<SphereCollider>();
		sphereCollider.center = interactionTransform.position;
		sphereCollider.radius = radius;
		sphereCollider.isTrigger = true;
	}

	private void OnDrawGizmosSelected()
	{
		Gizmos.color = Color.yellow;
		Gizmos.DrawWireSphere(interactionTransform.position, radius);
	}

	public virtual void Interact()
	{
		// Must be overwritten
		Debug.Log("Interacting with " + transform.name);
	}


	// We could use Ignore Collider to make it more efficient
	private void OnTriggerEnter(Collider other)
	{
		if (other.CompareTag("Player")) {
			isFocus = true;
		}
	}

	private void OnTriggerExit(Collider other)
	{
		if (other.CompareTag("Player")) {
			isFocus = false;
		}
	}


}
