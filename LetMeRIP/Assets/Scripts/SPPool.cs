using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/**
 * if this is already present in the scene, it won't be destroyed after a set time
 */
public class SPPool : MonoBehaviourPun
{
    private float lifeTime;
    [SerializeField] private float holdedSp = 0f;
    
    public void Init(float amount, float lifeTime) 
    {
        holdedSp = amount;
        this.lifeTime = lifeTime;

        if (PhotonNetwork.IsMasterClient) StartCoroutine(DestroyAfterTime(lifeTime));
    }

    public void DrainPool(float amount, PlayerController characterController)
    {
        if (PhotonNetwork.IsMasterClient)
        {
            if (characterController == null) Debug.Log("cc NULL");
            photonView.RPC("RpcDrainPool", RpcTarget.All, amount, characterController.photonView.ViewID);
        }
    }

    [PunRPC]
    public void RpcDrainPool(float amount, int playerViewID)
    {
        PlayerController cc = PhotonView.Find(playerViewID).GetComponent<PlayerController>(); // fuck it seems kinda expensive
        float finalAmount = amount > holdedSp ? holdedSp : amount;
        holdedSp -= finalAmount;
        cc.SGManager.AddSP(finalAmount);
        if (holdedSp <= 0f && PhotonNetwork.IsMasterClient) PhotonNetwork.Destroy(gameObject); 
    }

    private IEnumerator DestroyAfterTime(float lifeTime)
    {
        yield return new WaitForSeconds(lifeTime);
        PhotonNetwork.Destroy(gameObject);
    }

}
