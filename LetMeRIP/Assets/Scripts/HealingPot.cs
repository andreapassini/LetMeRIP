using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class HealingPot : MonoBehaviourPun
{
    private float destroyAfterTime = 4.5f;
    private float maxRadius = 7;
    private float minRadius = 3;

    private float holdenHeal;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    public void Init(float amount)
    {
        holdenHeal = amount;

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

        if (cc.photonView.IsMine) {
            cc.HPManager.Heal(amount / 4.5f);
        }
    }

    private IEnumerator DestroyAfterTime(float lifeTime)
    {
        yield return new WaitForSeconds(lifeTime);
        PhotonNetwork.Destroy(gameObject);
    }
}
