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
        //StartCoroutine(DestroyBulletAfterTime());

        Physics.IgnoreLayerCollision(9, 9);
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Debug.Log("HIT: " + collision.gameObject.GetComponent<EnemyForm>());

        EnemyForm enemyHealth = collision.gameObject.GetComponent<EnemyForm>();

        if (enemyHealth != null) {
            enemyHealth.TakeDamage(damage);
            enemyHealth.enemyStats.defense *= 0.1f;
        }

        Destroy(gameObject);
    }

    public IEnumerator DestroyBulletAfterTime()
    {
        yield return new WaitForSeconds(destroyAfterTime);
        Destroy(gameObject);
    }
}
