using System.IO;
using Photon.Pun;
using UnityEngine;

public class PlayerManager : MonoBehaviourPun
{
    private void Start()
    {
        if (!photonView.IsMine) return;

        CreateController();
    }

    private void CreateController()
    {
        PhotonNetwork.Instantiate(Path.Combine("Prefabs", "WarriorCharacter"), Vector3.zero, Quaternion.identity);
    }
}