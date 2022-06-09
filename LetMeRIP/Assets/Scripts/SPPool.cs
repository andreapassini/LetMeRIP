using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/**
 * if this is already present in the scene, it won't be destroyed after a set time
 */
public class SPPool : MonoBehaviourPun
{
    public float remainingSP { get => holdedSp; }
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
            Debug.Log($"POOL VIEWID {photonView.ViewID}");
            photonView.RPC("RpcDrainPool", RpcTarget.All, amount, characterController.photonView.ViewID);
        }
    }

    [PunRPC]
    private void RpcDrainPool(float amount, int playerViewID)
    {
        PlayerController cc = new List<PlayerController>(FindObjectsOfType<PlayerController>()).Find(player => player.photonView.ViewID == playerViewID); // fuck it seems kinda expensive
        if(remainingSP <= 0 && PhotonNetwork.IsMasterClient) PhotonNetwork.Destroy(gameObject);
        else 
        {
            float finalAmount = amount > remainingSP ? remainingSP : amount;
            holdedSp -= finalAmount;
            if (cc.photonView.IsMine) cc.SGManager.AddSP(finalAmount);
        }
    }

    private IEnumerator DestroyAfterTime(float lifeTime)
    {
        yield return new WaitForSeconds(lifeTime);
        PhotonNetwork.Destroy(gameObject);
    }

}
