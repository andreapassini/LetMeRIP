using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class BulletTrapassing : MonoBehaviour
{
    [SerializeField] GameObject hitDmgEffect;
    [SerializeField] float destroyAfterTime = 1.5f;
    [SerializeField] public float damage = 10f;

    private Rigidbody rb;
    private Collider collider;

    // Start is called before the first frame update
    void Start()
    {
        Destroy(gameObject, destroyAfterTime);

        rb = transform.GetComponent<Rigidbody>();
        collider = transform.GetComponent<Collider>();

        collider.isTrigger = true;
    }

	private void OnTriggerEnter(Collider collision)
	{
        EnemyForm enemyHealth = collision.gameObject.GetComponent<EnemyForm>();

        if (enemyHealth != null) {
            enemyHealth.TakeDamage(damage);
        }
    }
}
