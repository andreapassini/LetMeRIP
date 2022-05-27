using System.Collections;
using Photon.Pun;
using UnityEngine;

public class Bullet : MonoBehaviour
{
    [SerializeField] GameObject hitDmgEffect;
    [SerializeField] float destroyAfterTime = 5f;
    [SerializeField] float damage = 20f;

    public PhotonView view;
    
    void Start()
    {
        if (!view.IsMine) return;
        
        // Destroy(gameObject, destroyAfterTime);
        // PhotonNetwork.Destroy(gameObject);
        StartCoroutine(DestroyBulletAfterTime());

        Physics.IgnoreLayerCollision(9, 9);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!view.IsMine) return;

        Debug.Log("HIT: " + collision.gameObject.GetComponent<EnemyHealth>());

        EnemyHealth enemyHealth = collision.gameObject.GetComponent<EnemyHealth>();

        if (enemyHealth != null)
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
        PhotonNetwork.Destroy(gameObject);
    }

    private IEnumerator DestroyBulletAfterTime()
    {
        yield return new WaitForSeconds(destroyAfterTime);
        PhotonNetwork.Destroy(gameObject);
    }
}