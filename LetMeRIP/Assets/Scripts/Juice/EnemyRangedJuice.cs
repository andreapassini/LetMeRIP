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
    private Transform attackPoint;

    private bool stopAi = false;
    [SerializeField] private float aiFrameRate = .1f;

    private float health = 100f;
    private float bulletForce = 10f;
    private GameObject bulletPrefab;
    public Transform target;
    public GameObject hitEffect;

    private DecisionTree dt;
    public float coolDown;

    private float startTimeAction;

    // Start is called before the first frame update
    void Start()
    {
        #region SetUp
        navMeshAgent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        bulletPrefab = Resources.Load<GameObject>("Prefabs/Juice/EnemyBulletJuice");
        attackPoint = transform.Find("Hips").Find("Staff").Find("attackPoint").transform;
        #endregion

        startTimeAction = 0;

        //DTAction dTAction = new DTAction(Shoot);

        //dt = new DecisionTree(dTAction);

        StartCoroutine(Patrol());
    }

    public IEnumerator Patrol()
    {
        while (true)
        {
            if (!stopAi)
            {
                Attack(this);
            }

            yield return new WaitForSeconds(aiFrameRate);
        }
    }

    public object Attack(object o)
    {
        // Look at Target
        // Maybe better to use RigidBody and use Slerp for a smoother rotation
        transform.LookAt(new Vector3(target.position.x, transform.position.y, target.position.z), Vector3.up);

        if (startTimeAction + coolDown > Time.time)
        {
            return null;
        }

        animator.SetTrigger("attack");
        
        return null;
    }

    public void Shoot()
    {
        startTimeAction = Time.time;

        GameObject bullet = Instantiate(bulletPrefab, attackPoint.position, transform.rotation);

        bullet.GetComponent<Rigidbody>().AddForce(bullet.transform.forward * bulletForce, ForceMode.Impulse);
    }

    public void TakeDamage(float damageAmount)
    {
        StopAIEnemy();

        health -= damageAmount;

        animator.SetTrigger("damage");

        GameObject h = Instantiate(hitEffect, transform.position, transform.rotation);
        Destroy(h, 3f);
    }

    public void StopAIEnemy()
    {
        stopAi = true;
    }

    // This will be called by the animation Event
    public void RestartAIEnemy()
    {
        stopAi = false;
    }
}
