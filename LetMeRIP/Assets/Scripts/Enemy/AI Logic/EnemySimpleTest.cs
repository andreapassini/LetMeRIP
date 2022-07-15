using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemySimpleTest : MonoBehaviour
{
    private NavMeshAgent navMeshAgent;
    [SerializeField] private Transform target;

    [SerializeField] private InputAction action;

	private void OnEnable()
    {
        action.Enable();
    }
    private void OnDisable()
    {
        action.Disable();
    }

	private void Awake()
	{
        navMeshAgent = GetComponent<NavMeshAgent>();
    }

    // Start is called before the first frame update
    void Start()
    {
        action.performed += _ => StopAITest();

        navMeshAgent.destination = target.position;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void StopAITest()
    {
        //navMeshAgent.destination = transform.position;
        navMeshAgent.velocity = Vector3.zero;
        navMeshAgent.isStopped = true;
    }
}
