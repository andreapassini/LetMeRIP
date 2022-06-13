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
        Destroy(gameObject, destroyAfterTime);
    }

    private void OnCollisionEnter(Collision collision)
    {

		if (collision.transform.tag.Equals("Player")) {
            //Debug.Log("HIT: " + collision.gameObject.GetComponent<EnemyHealth>());

            HPManager playerHealth = collision.gameObject.GetComponent<HPManager>();

            if (playerHealth != null) {
                playerHealth.TakeDamage(damage, transform.position);
            }
        }
        

        Destroy(gameObject);
    }

    public IEnumerator DestroyBulletAfterTime()
    {
        yield return new WaitForSeconds(destroyAfterTime);
        Destroy(gameObject);
    }
}
