using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bullet : MonoBehaviour
{

    [SerializeField] GameObject hitDmgEffect;

    [SerializeField] float destroyAfterTime = 5f;

    void Start()
    {
        Destroy(gameObject, destroyAfterTime);
        //StartCoroutine(DestroyBulletAfterTime());
    }

    private void OnCollisionEnter(Collision collision)
    {
        //GameObject effect = Instantiate(hitDmgEffect, transform.position, Quaternion.identity);
        //Destroy(effect, 2f);
        if(collision.collider.CompareTag("Enemy"))
            Destroy(gameObject);
    }

    public IEnumerator DestroyBulletAfterTime()
    {
        yield return new WaitForSeconds(destroyAfterTime);
        Destroy(gameObject);
    }
}
