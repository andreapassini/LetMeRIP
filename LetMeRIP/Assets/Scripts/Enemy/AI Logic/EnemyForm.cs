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
    [Serializable]
    public class Stats
    {
        public string enemyName;
        public float health;
        public float maxHealth;

        public float attack;
        public float defense;

        public float swiftness;
        public float rewardSp;
    }

    public int ViewID { get => photonView.ViewID; }

    public static event Action<EnemyForm> OnEnemyKilled;
    public event Action<EnemyForm> OnEnemyDamaged;
    public static event Action<EnemyForm> OnEnemyAttack;

    public EnemyStats enemyStatsSrc;
    public Stats enemyStats;

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
    public LayerMask whatRayHit;

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

    public bool stopAI = false;

    public string targetTag = "Player";


    private EnemyBillboard healthBar;
    
    void Start()
    {
    }

    GameObject spPool;

    protected virtual void Awake()
    {
        enemyStats = new Stats();
        enemyStats.enemyName = enemyStatsSrc.enemyName;
        enemyStats.maxHealth = enemyStatsSrc.maxHealth;
        enemyStats.health = enemyStatsSrc.health;
        enemyStats.attack = enemyStatsSrc.attack;
        enemyStats.defense = enemyStatsSrc.defense;
        enemyStats.swiftness = enemyStatsSrc.swiftness;
        enemyStats.rewardSp = enemyStatsSrc.rewardSp;

        spPool = transform.Find("SpPool").gameObject;
        spPool.SetActive(false);

        healthBar = this.GetComponentInChildren<EnemyBillboard>();
    }
    // This method will cast an event when Attack Anim. Event is cast
    // Cause anim events are only related to the object attached to the animator
    public void OnAttack()
    {
        OnEnemyAttack?.Invoke(this);
    }

    public virtual void TakeDamage(float dmg)
    {
        if(!PhotonNetwork.IsMasterClient) return;

        photonView.RPC("RpcTakeDamage", RpcTarget.All, dmg);
    }

    public virtual void Die()
	{
        Debug.Log($"{name} died");
        spPool.transform.SetParent(transform.parent);
        spPool.SetActive(true);

        // UN-Sub
        FormManager.OnBodyExitForEnemy -= RestTargetAfterSpiritExit;
        HPManager.OnPlayerKilled -= RestTargetAfterSpiritExit;

        if (!PhotonNetwork.IsMasterClient) return;

        // Die Event 
        Debug.Log($"{name} called killed event");
        OnEnemyKilled?.Invoke(this);

        // UN-Subscribe to Event Change Spirit Form

        // Overwrite
        PhotonNetwork.Destroy(photonView);
        //Destroy(gameObject);
    }

    // Wait until the end of the action to update again the FSM
    public virtual void CastAbilityDuration(EnemyAbility ability)
    {
        
        //if (AiFrameRate < ability.abilityDurtation)
        //{
        //}

        StartCoroutine(AbilityDuration(ability));

    }

    private IEnumerator AbilityDuration(EnemyAbility ability)
    {
        if (ability.abilityDurtation > AiFrameRate) {
            // Stop FSM
            //reactionReference = AiFrameRate;
            //AiFrameRate = ability.abilityDurtation;
            stopAI = true;
            animator.SetFloat("speed", 0);
            //StopEverythingForAbilityExecution();

            yield return new WaitForSeconds(ability.abilityDurtation);

            stopAI = false;
            //AiFrameRate = reactionReference;
        }
    }

    public void CastEnemyAbility(EnemyAbility enemyAbility)
	{
        if (!PhotonNetwork.IsMasterClient) return;

        //Debug.Log(enemyAbility.abilityName);

        photonView.RPC("RpcCastEnemyAbility",
            RpcTarget.All,
            enemyAbility.abilityName);
	}

    [PunRPC]
    public void RpcCastEnemyAbility(string enemyAbilityName)
	{
        if (PhotonNetwork.IsMasterClient) return;

        abilites.TryGetValue(enemyAbilityName, out EnemyAbility e);
        e.StartAbility(this);
	}


    [PunRPC]
    public void RpcTakeDamage(float dmg)
	{
        animator.SetTrigger("damage");

        // Calcolate defense reduction
        dmg = dmg - (dmg * enemyStats.defense * 0.01f); ;
        dmg = Mathf.Clamp(dmg, 0, float.MaxValue);
        enemyStats.health = enemyStats.health - dmg;

        Debug.Log($"{enemyStats.enemyName} took {dmg} => {enemyStats.health}HP");

        if (enemyStats.health <= 0) {
            Die();
        }

        // Take damage Event
        OnEnemyDamaged?.Invoke(this);
    }

    public virtual void Init()
	{
        abilites = new Dictionary<string, EnemyAbility>();
        abilites.Add(attackAction.abilityName, attackAction);
        abilites.Add(chaseAction.abilityName, chaseAction);
        abilites.Add(searchAction.abilityName, searchAction);
        
        healthBar.Init(this);

        FormManager.OnBodyExitForEnemy += RestTargetAfterSpiritExit;
        HPManager.OnPlayerKilled += RestTargetAfterSpiritExit;
    }

    public virtual void StopEverythingForAbilityExecution()
	{

	}

    public virtual void RestartAI()
	{

	}

    public virtual void RestTargetAfterSpiritExit(FormManager formManager)
	{
        if (target == null) {
            targets = GameObject.FindGameObjectsWithTag(targetTag);

            if(targets.Length == 0) {
                return;
			}
            target = targets[0].transform;
        }

        FormManager a;

		if (target.TryGetComponent(out a)) {
            if (formManager != a)
                return;

            float distance = float.MaxValue;

            if (targets.Length == 0)
                return;

            foreach (GameObject t in targets) {
                float calculatedDistance = (t.transform.position - transform.position).magnitude;
                if (calculatedDistance < distance) {
                    distance = calculatedDistance;
                    target = t.transform;
                }
            }
        }
        
    }

    public virtual void RestTargetAfterSpiritExit(PlayerController formManager)
    {
        if (target == null) {
            targets = GameObject.FindGameObjectsWithTag(targetTag);
            target = targets[0].transform;
        }

        PlayerController a;

        if (target.TryGetComponent(out a)) {
            if (formManager != a)
                return;

            float distance = float.MaxValue;

            foreach (GameObject t in targets) {
                float calculatedDistance = (t.transform.position - transform.position).magnitude;
                if (calculatedDistance < distance) {
                    distance = calculatedDistance;
                    target = t.transform;
                }
            }
        }

    }

}
