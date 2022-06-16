using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerAttackJuice : MonoBehaviour
{
    private Animator animator;
    private Transform attackPoint;

    private float health = 100;
    private float bulletForce = 10f;

    private bool stopAi;
    [SerializeField] private float aiFrameRate = .1f;

    private GameObject bulletPrefab;
    public Transform target;

    public float coolDown;
    private float startTimeAction;

    void Start()
    {
        animator = GetComponent<Animator>();

        bulletPrefab = Resources.Load<GameObject>("Prefabs/Juice/PlayerBulletJuice");
        attackPoint = transform.Find("attackPoint").transform;

        startTimeAction = Time.time;

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

        Shoot();

        animator.SetTrigger("attack");

        return null;
    }

    public void Shoot()
    {
        startTimeAction = Time.time;

        GameObject bullet = Instantiate(bulletPrefab, attackPoint.position, transform.rotation);

        bullet.GetComponent<Rigidbody>().AddForce(bullet.transform.forward * bulletForce, ForceMode.Impulse);

    }

    public void StopAIPlayer()
    {
        stopAi = true;
    }

    // This will be called by the animation Event
    public void RestartAIPlayer()
    {
        stopAi = false;
    }
}
