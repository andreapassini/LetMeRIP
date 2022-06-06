using UnityEngine;
using System;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]
public class EnemyForm : MonoBehaviourPun
{
    public int ViewID { get => photonView.ViewID; }

    [System.NonSerialized]
    public PhotonView photonView;

    public static event Action<EnemyForm> OnEnemyKilled;
    public static event Action<EnemyForm> OnEnemyDamaged;
    public static event Action<EnemyForm> OnEnemyAttack;

    public EnemyStats enemyStats;

    public Dictionary<string, EnemyAbility> abilites;

    //public List<EnemyAbility> enemyAbilities;
    public EnemyAbility attackAction;
    public EnemyAbility chaseAction;
    public EnemyAbility searchAction;

    public float AiFrameRate = 1f;

    [System.NonSerialized]
    public Vector3 lastSeenPos;

    public Transform attackPoint;
    public float attackRange = 3f;

    [HideInInspector]
    public float health;

    public LayerMask whatIsTarget;
    public LayerMask whatICanSeeThrough;

    public Animator animator;

    public float takeDamageDuration = 1f;

    [System.NonSerialized]
    public Rigidbody rb;

    [System.NonSerialized]
    public GameObject[] targets;

    [System.NonSerialized]
    public Transform target;

    //[System.NonSerialized]
    public NavMeshAgent navMeshAgent;

    [System.NonSerialized]
    public float reactionReference;

    // Start is called before the first frame update
    void Start()
    {
    }

    // This method will cast an event when Attack Anim. Event is cast
    // Cause anim events are only related to the object attached to the animator
    public void OnAttack()
    {
        OnEnemyAttack?.Invoke(this);
    }

    public virtual void TakeDamage(float dmg)
    {
        animator.SetTrigger("damage");      

        // Calcolate defense reduction
        dmg -= enemyStats.defense;
        dmg = Mathf.Clamp(dmg, 0, float.MaxValue);
        Debug.Log("Health " + health);
        Debug.Log("dmg " + dmg);
        health = health - dmg;

        Debug.Log("Health " + health);

        if (health <= 0) {
            Die();
        }

        // Take damage Event
        OnEnemyDamaged?.Invoke(this);
    }

    public virtual void Die()
	{
        // Die Event 
        OnEnemyKilled?.Invoke(this);

        // Overwrite
        PhotonNetwork.Destroy(photonView);
        //Destroy(gameObject);
    }

    // Wait until the end of the action to update again the FSM
    public virtual void CastAbilityDuration(EnemyAbility ability)
    {
        if (AiFrameRate < ability.abilityDurtation)
        {
            StartCoroutine(AbilityDuration(ability));
        }
    }

    private IEnumerator AbilityDuration(EnemyAbility ability)
    {
        // Stop FSM
        reactionReference = AiFrameRate;
        AiFrameRate = ability.abilityDurtation;

        yield return new WaitForSeconds(ability.abilityDurtation);

        AiFrameRate = reactionReference;
        
    }

    public void CastEnemyAbility(EnemyAbility enemyAbility)
	{
        photonView.RPC("RpcCastEnemyAbility",
            RpcTarget.All,
            enemyAbility.name
            );
	}

    [PunRPC]
    public void RpcCastEnemyAbility(string enemyAbilityName)
	{
        abilites.TryGetValue(enemyAbilityName, out EnemyAbility e);
        e.StartAbility(this);
	}

    public virtual void Init()
	{
        abilites.Add("attackAction", attackAction);
        abilites.Add("chaseAction", chaseAction);
        abilites.Add("searchAction", searchAction);
    }
}
