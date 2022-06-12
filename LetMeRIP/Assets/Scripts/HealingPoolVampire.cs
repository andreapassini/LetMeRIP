using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HealingPoolVampire : MonoBehaviourPun
{
    public float destroyAfterTime = 4f;

    public void Init()
	{
        if (PhotonNetwork.IsMasterClient) StartCoroutine(DestroyAfterTime(destroyAfterTime));
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
      
        if (cc.photonView.IsMine) 
        {
            float healAmountPerSec = (float)((50 + 0.4 * cc.stats.intelligence) / 4);
            cc.HPManager.Heal(healAmountPerSec);
            cc.SGManager.AddSP(-1 * (healAmountPerSec/2));
        }
            
        
    }

    private IEnumerator DestroyAfterTime(float lifeTime)
    {
        yield return new WaitForSeconds(lifeTime);
        PhotonNetwork.Destroy(gameObject);
    }
}
