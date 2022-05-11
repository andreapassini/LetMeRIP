using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]

public class GoToClick : MonoBehaviour {

    [SerializeField] private GameObject indicator;

    public LayerMask movementMask;
    public int maxDistance = 100;

    //private NavMeshAgent agent;
    //private readonly float epsilon = 2f;

    private void Start()
    {
        GetComponent<NavMeshAgent>().speed = 10f;
    }

    void Update() {
		if(Input.GetMouseButton(0)) {
			Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
			RaycastHit hit;
			if (Physics.Raycast (ray, out hit, maxDistance, movementMask)) {
				GetComponent<NavMeshAgent>().destination = hit.point;
                //GameObject indicatorInstance = Instantiate(indicator, hit.point, Quaternion.identity);
                //Destroy(indicatorInstance, 2f);
            }
        }
	}
}