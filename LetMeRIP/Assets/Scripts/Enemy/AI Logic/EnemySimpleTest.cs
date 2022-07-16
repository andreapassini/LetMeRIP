using System;
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
    [SerializeField] private float AIFrameRate;
    
    private bool stopAI = false;
    private Vector3 standarVelocity;

    [SerializeField] private float cooldownDoubleClick;
    private float startTime;

    #region FSM
    FSM fsm;

    #endregion

    #region Input
    [SerializeField] private InputAction action;
    private void OnEnable()
    {
        action.Enable();
    }
    private void OnDisable()
    {
        action.Disable();
    }
    #endregion

    private void Awake()
	{
        navMeshAgent = GetComponent<NavMeshAgent>();
    }

    // Start is called before the first frame update
    void Start()
    {
        action.performed += _ => StopRestartAI();

        standarVelocity = navMeshAgent.velocity;
        //startTime = Time.time - (2 * cooldown);

        FSMState chase = new FSMState();
        chase.stayActions.Add(Chase);
        
        fsm = new FSM(chase);

        StartCoroutine(Patrol());
    }

    private void Chase()
    {
        navMeshAgent.destination = target.position;
    }

    private IEnumerator Patrol()
    {
        while (true)
        {
            if (!stopAI)
            {
                fsm.Update();
            }

            yield return new WaitForSeconds(AIFrameRate);
        }
    }

    private void StopRestartAI()
	{
		if (!CooldownOff())
			return;

		if (stopAI) 
        {
            stopAI = false;
            StopMoving();
        } 
        else 
        {
            stopAI = true;
            StartMoving();
        }
    }

    private void StopMoving()
    {
        navMeshAgent.velocity = Vector3.zero;
        navMeshAgent.isStopped = true;
    }
    private void StartMoving()
	{
        navMeshAgent.isStopped = false;
        navMeshAgent.velocity = standarVelocity;
    }

	private bool CooldownOff()
	{
		if ((Time.time - startTime) >= cooldownDoubleClick)
			return true;
		return false;
	}
}
