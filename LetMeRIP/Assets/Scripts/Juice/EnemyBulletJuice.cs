using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof (SphereCollider))]
public class EnemyBulletJuice : MonoBehaviour
{
    private Rigidbody rb;
    private SphereCollider sphereCol;
    private float damage = 10f;

    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        sphereCol = GetComponent<SphereCollider>();

    }

    private void OnCollisionEnter(Collision collision)
    {
        if(TryGetComponent<PlayerHealthJuice>(out PlayerHealthJuice playerHealth))
        {
            playerHealth.TakeDamage(damage);
        }
    }
}
