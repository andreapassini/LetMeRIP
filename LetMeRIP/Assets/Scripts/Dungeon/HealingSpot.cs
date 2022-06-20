using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HealingSpot : Interactable, IOnEventCallback
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
            if (characterController is null) Debug.LogError("cc is null");

            if (PhotonNetwork.IsMasterClient)
                //photonView.RPC("RpcHealingSpotHeal", RpcTarget.All, characterController.photonView.ViewID);
                characterController.HPManager.Heal(healAmount);
                    
        } else
        {
            Debug.Log("Healing spot already used you greedy bastard");
        }
    }

    public void OnEvent(EventData photonEvent)
    {
        throw new System.NotImplementedException();
    }

    //[PunRPC]
    //private void RpcHealingSpotHeal(int playerViewID)
    //{
    //    PlayerController cc = PhotonView.Find(playerViewID).GetComponent<PlayerController>();
    //    cc.HPManager.Heal(healAmount, false);
    //}
}
