using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bullet : MonoBehaviour
{

    [SerializeField] GameObject hitDmgEffect;
    [SerializeField] float destroyAfterTime = 5f;
    [SerializeField] float damage = 20f;


    void Start()
    {
        Destroy(gameObject, destroyAfterTime);
        //StartCoroutine(DestroyBulletAfterTime());
    }

    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log("HIT: " + collision.gameObject.GetComponent<EnemyHealth>());

        EnemyHealth enemyHealth = collision.gameObject.GetComponent<EnemyHealth>();

        if(enemyHealth != null)
        {
            enemyHealth.OnDamage(damage, transform.position);
        }

        // NOT WORKING
        //if (TryGetComponent (out EnemyHealth h))
        //{
        //    h.OnDamage(damage, transform.position);
        //}

        //GameObject effect = Instantiate(hitDmgEffect, transform.position, Quaternion.identity);
        //Destroy(effect, 2f);

        //if(collision.collider.CompareTag("Enemy"))
        Destroy(gameObject);
    }

    public IEnumerator DestroyBulletAfterTime()
    {
        yield return new WaitForSeconds(destroyAfterTime);
        Destroy(gameObject);
    }
}
