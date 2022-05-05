using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bullet : MonoBehaviour
{

    [SerializeField] GameObject hitDmgEffect;

    [SerializeField] float destroyAfterTime = 5f;

    void Start()
    {
        StartCoroutine(DestroyBulletAfterTime());
    }

    private void OnCollisionEnter(Collision collision)
    {
        GameObject effect = Instantiate(hitDmgEffect, transform.position, Quaternion.identity);
        Destroy(effect, 2f);
        Destroy(gameObject);
    }

    public IEnumerator DestroyBulletAfterTime()
    {
        yield return new WaitForSeconds(destroyAfterTime);
        Destroy(gameObject);
    }
}
