using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

public class PlayerHudController : MonoBehaviourPun
{
    private PlayerController playerController;
    
    public void Start()
    {
        if (!photonView.IsMine) return;
        playerController = transform.GetComponentInParent<PlayerController>();
    }
    
    public void Hide() => HudController.Instance.Hide();
    public void Show() => HudController.Instance.Show();

    public void DisableMovement() => playerController.DisableAll();
    public void EnableMovement() => playerController.EnableAll();
}
