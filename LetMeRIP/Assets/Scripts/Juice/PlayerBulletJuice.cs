using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public class PlayerBulletJuice : MonoBehaviour
{
    private Rigidbody rb;
    private SphereCollider sphereCol;
    public float damage = 10f;

    public GameObject shootEffect;
    public GameObject destroyEffect;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        sphereCol = GetComponent<SphereCollider>();

        // Shoot Effect
        GameObject h = Instantiate(shootEffect, transform.position, transform.rotation);
        Destroy(h, 3f);
    }

    private void OnCollisionEnter(Collision collision)
    {

        if (collision.transform.TryGetComponent<EnemyRangedJuice>(out EnemyRangedJuice enemyRangedJuice))
        {

            Debug.Log("Hit Hit");
            enemyRangedJuice.TakeDamage(damage);

            Destroy(gameObject);
        }

        // Destroy Effect
        GameObject h = Instantiate(destroyEffect, transform);
        Destroy(h, 3f);

        Destroy(gameObject);
    }
}
