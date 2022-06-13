using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public class Pot : MonoBehaviourPun
{
    private float lifeTime = 4.5f;
    private float radius = 3f;
    private float healing;

    private SphereCollider s;

    public void Init(float amount, float radius)
    {
        healing = amount;
        if(radius <= 7) {
            this.radius = radius;
        } else {
            this.radius = 7f;
		}

        s.radius = this.radius;

        if (PhotonNetwork.IsMasterClient) StartCoroutine(DestroyAfterTime(lifeTime));
    }

    public void DrainPool(float amount, PlayerController characterController)
    {
        if (PhotonNetwork.IsMasterClient) {
            if (characterController == null) Debug.Log("cc NULL");
            Debug.Log($"POOL VIEWID {photonView.ViewID}");
            photonView.RPC("RpcDrainPool", RpcTarget.All, amount, characterController.photonView.ViewID);
        }
    }

    [PunRPC]
    private void RpcDrainPool(float amount, int playerViewID)
    {
        PlayerController cc = new List<PlayerController>(FindObjectsOfType<PlayerController>()).Find(player => player.photonView.ViewID == playerViewID); // fuck it seems kinda expensive
        
        if (cc.photonView.IsMine) {
            cc.HPManager.Heal(amount/4.5f);
        }

        // a
    }

    private IEnumerator DestroyAfterTime(float lifeTime)
    {
        yield return new WaitForSeconds(lifeTime);
        PhotonNetwork.Destroy(gameObject);
    }
}
