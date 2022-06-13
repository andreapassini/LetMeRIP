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
        DontDestroyOnLoad(gameObject);
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

        PhotonView playerDied = PhotonView.Find(playerViewId);
        if(playerDied != null)
            players.Remove(playerDied.GetComponent<PlayerController>());

        Debug.Log($"Players count: {players.Count}");
        if(players.Count <= 0)
        {
            Debug.Log("enabling gameover ui");
            gameOverUI.SetActive(true);
            return;
        }

        byte remainingPlayingCharacters = 0;
        foreach(PlayerController player in players)
        {
            if (!(player.formManager.IsOut || player.IsSpirit)) remainingPlayingCharacters++;
            if (player.IsSpirit) remainingPlayingCharacters++;
        }

        if(remainingPlayingCharacters == 0)
        {
            Debug.Log("enabling gameover ui");
            gameOverUI.SetActive(true);
            return;
        }
    }
}
