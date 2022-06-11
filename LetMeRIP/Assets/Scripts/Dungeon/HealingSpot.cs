using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HealingSpot : Interactable
{
    [SerializeField] private float healAmount;
    private bool isUsed = false;

    public override void Effect(PlayerController characterController)
    {
        base.Effect(characterController);
        if (!isUsed)
        {
            if (characterController.HPManager.Health == characterController.HPManager.MaxHealth)
            {
                Debug.Log("Max health already");
                return;
            }
            isUsed = true;
            gameObject.SetActive(false);
            if (PhotonNetwork.IsMasterClient)
                photonView.RPC("RpcHealingSpotHeal", RpcTarget.All, characterController.photonView.ViewID);
        } else
        {
            Debug.Log("Healing spot already used you greedy bastard");
        }
    }


    [PunRPC]
    private void RpcHealingSpotHeal(int playerViewID)
    {
        PlayerController cc = PhotonView.Find(playerViewID).GetComponent<PlayerController>();
        if(cc.IsMine)
            cc.HPManager.Heal(healAmount, false);
    }
}
