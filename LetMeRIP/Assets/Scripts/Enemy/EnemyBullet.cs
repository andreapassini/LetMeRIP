using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyBullet : MonoBehaviour
{

    [SerializeField] GameObject hitDmgEffect;
    [SerializeField] float destroyAfterTime = 5f;
    [SerializeField] float damage = 20f;


    void Start()
    {
        Debug.Log(name);

        Destroy(gameObject, destroyAfterTime);
        //StartCoroutine(DestroyBulletAfterTime());

        Physics.IgnoreLayerCollision(9, 9);
    }

    private void OnCollisionEnter(Collision collision)
    {
        //Debug.Log("HIT: " + collision.gameObject.GetComponent<EnemyHealth>());

        PlayerHealth playerHealth = collision.gameObject.GetComponent<PlayerHealth>();

        if(playerHealth != null)
        {
            playerHealth.TakeDamage(damage, transform.position);
        }

        //GameObject effect = Instantiate(hitDmgEffect, transform.position, Quaternion.identity);
        //Destroy(effect, 2f);

        Destroy(gameObject);
    }

    public IEnumerator DestroyBulletAfterTime()
    {
        yield return new WaitForSeconds(destroyAfterTime);
        Destroy(gameObject);
    }
}
