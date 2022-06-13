using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameOverManager : MonoBehaviourPun
{
    [SerializeField]
    private GameObject gameOverUI;

    private void Start()
    {
        StartCoroutine(LateStart());
    }

    private IEnumerator LateStart()
    {
        yield return new WaitForSeconds(.5f);
        HPManager.OnPlayerKilled += GameOverCheck;
    }

    private void GameOverCheck(PlayerController cc)
    {
        if (PhotonNetwork.IsMasterClient) photonView.RPC("RpcGameOverCheck", RpcTarget.All, cc.photonView.ViewID);
    }

    [PunRPC]
    public void RpcGameOverCheck(int playerViewId)
    {
        List<PlayerController> players = new List<PlayerController>(FindObjectsOfType<PlayerController>());
        PlayerController playerDied = PhotonView.Find(playerViewId).GetComponent<PlayerController>();
        if(playerDied != null)
            players.Remove(playerDied);
        Debug.Log($"Players count: {players.Count}");
        if(players.Count <= 0)
        {
            Debug.Log("enabling gameover ui");
            gameOverUI.SetActive(true);

            Debug.Log("gameover ui enabled");
        }
    }
}
