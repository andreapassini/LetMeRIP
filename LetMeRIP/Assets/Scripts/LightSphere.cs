using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LightSphere : MonoBehaviourPun
{
    [SerializeField] GameObject hitDmgEffect;
    [SerializeField] float destroyAfterTime = 5f;
    [SerializeField] public float damage = 10f;

    void Start()
    {
        // if is master
        if(PhotonNetwork.IsMasterClient)
        StartCoroutine(DestroyBulletAfterTime());

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

        PhotonNetwork.Destroy(photonView);
        Destroy(gameObject);
    }

    public IEnumerator DestroyBulletAfterTime()
    {
        yield return new WaitForSeconds(destroyAfterTime);
        PhotonNetwork.Destroy(photonView);
        Destroy(gameObject);
    }
}
