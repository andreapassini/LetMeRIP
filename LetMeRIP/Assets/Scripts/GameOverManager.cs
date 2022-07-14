using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameOverManager : MonoBehaviour
{
    [SerializeField]
    private GameObject gameOverUI;

    private void Start()
    {
        HPManager.OnPlayerKilled += _ => GameOverCheck();
    }

    private void GameOverCheck()
    {
        PlayerManager[] players = FindObjectsOfType<PlayerManager>();

        foreach (PlayerManager p in players)
        {
            Debug.Log($"Checking{p.name}");
            if (!p.spiritStats.isDead) 
            {
                Debug.Log("Still has spirit"); return;
            }
            if (!p.bodyStats.isDead)
            {
                Debug.Log("Still has body"); return;
            }
            //if (!(p.spiritStats.isDead && p.bodyStats.isDead)) return;
        }

        gameOverUI.SetActive(true);
        gameOverUI.SetActive(true);
    }

    //public void RpcGameOverCheck(int playerViewId)
    //{
    //    List<PlayerController> players = new List<PlayerController>(FindObjectsOfType<PlayerController>());
    //    PlayerController playerDied = PhotonView.Find(playerViewId).GetComponent<PlayerController>();
    //    if(playerDied != null)
    //        players.Remove(playerDied);
    //    Debug.Log($"Players count: {players.Count}");
    //    if(players.Count <= 0)
    //    {
    //        Debug.Log("enabling gameover ui");
    //        gameOverUI.SetActive(true);

    //        Debug.Log("gameover ui enabled");
    //    }
    //}
}
