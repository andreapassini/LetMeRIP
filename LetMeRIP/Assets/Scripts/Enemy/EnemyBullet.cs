using Photon.Pun;
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

		if (collision.gameObject.CompareTag("Player") && collision.gameObject.TryGetComponent<PlayerController>(out PlayerController player)) {
            player.HPManager.TakeDamage(damage, transform.position);
        }

        if (PhotonNetwork.IsConnected && PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.Destroy(gameObject);
        }

        Destroy(gameObject);
    }

    public IEnumerator DestroyBulletAfterTime()
    {
        yield return new WaitForSeconds(destroyAfterTime);
        PhotonNetwork.Destroy(gameObject);
    }
}
