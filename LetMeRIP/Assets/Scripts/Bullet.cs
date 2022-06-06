using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bullet : MonoBehaviour
{

    [SerializeField] GameObject hitDmgEffect;
    [SerializeField] float destroyAfterTime = 5f;
    [SerializeField] float damage = 10f;


    void Start()
    {
        Destroy(gameObject, destroyAfterTime);
        //StartCoroutine(DestroyBulletAfterTime());

        Physics.IgnoreLayerCollision(9, 9);
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Debug.Log("HIT: " + collision.gameObject.GetComponent<EnemyForm>());

        EnemyForm enemyHealth = collision.gameObject.GetComponent<EnemyForm>();

        if(enemyHealth != null)
        {
            enemyHealth.TakeDamage(damage);
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
