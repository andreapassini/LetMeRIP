using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent (typeof(Animator))]
public class EnemyRangedJuice : MonoBehaviour
{
    private NavMeshAgent navMeshAgent;
    private Animator animator;

    private bool stopAi = false;
    [SerializeField] private float aiFrameRate = .1f;
    private float health = 100f;

    // Start is called before the first frame update
    void Start()
    {
        #region SetUp
        navMeshAgent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        #endregion


    }

    public IEnumerator Patrol()
    {
        while (true)
        {
            if(!stopAi)
                yield return new WaitForSeconds(aiFrameRate);
        }
    }

    public void TakeDamage(float damageAmount)
    {
        health -= damageAmount;
    }


}
