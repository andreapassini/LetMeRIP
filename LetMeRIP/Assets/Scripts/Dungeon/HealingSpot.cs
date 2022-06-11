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
            if(PhotonNetwork.IsMasterClient)
                characterController.HPManager.Heal(healAmount, false);
        } else
        {
            Debug.Log("Healing spot already used you greedy bastard");
        }
    }

}
