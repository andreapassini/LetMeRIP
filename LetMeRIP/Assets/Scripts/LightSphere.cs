using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LightSphere : MonoBehaviour
{
    [SerializeField] GameObject hitDmgEffect;
    [SerializeField] float destroyAfterTime = 5f;
    [SerializeField] public float damage = 10f;

    void Start()
    {
        Destroy(gameObject, destroyAfterTime);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.transform.CompareTag("Enemy"))
        {
            EnemyForm enemyHealth = collision.gameObject.GetComponent<EnemyForm>();

            if (enemyHealth != null) {
                enemyHealth.TakeDamage(damage);
                enemyHealth.enemyStats.defense *= 0.1f;
            }
        }

        Destroy(gameObject);
    }
}
