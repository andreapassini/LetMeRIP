using System.IO;
using Photon.Pun;
using UnityEngine;

public class PlayerManager : MonoBehaviourPun
{
    public PlayerController.Stats spiritStats;
    public PlayerController.Stats bodyStats;

    private void Awake()
    {
        spiritStats = new PlayerController.Stats();
        bodyStats = new PlayerController.Stats();
        spiritStats.formName = "";
        bodyStats.formName = "";

    }

    private void Start()
    {
        if (!photonView.IsMine) return;
        CreateController();
    }

    private void CreateController()
    {
        PhotonNetwork.Instantiate(Path.Combine("Prefabs", "MageCharacter"), Vector3.zero, Quaternion.identity);
    }


}