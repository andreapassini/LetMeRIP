using Photon.Pun;
using UnityEngine;

public class UIGameOverController : MonoBehaviour
{
    public void NavigateToTitleMenu()
    {
        // Time.timeScale = 1;
        // PhotonNetwork.LeaveRoom();
        PhotonNetwork.LoadLevel(0);
    }

    public void Quit()
    {
        PhotonNetwork.Disconnect();
        Application.Quit();
    }
}