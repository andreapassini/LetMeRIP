using Photon.Pun;
using UnityEngine;

public class UIGameOverController : MonoBehaviour
{
    public static UIGameOverController Instance;

    private void Awake()
    {
        Instance = this;
        gameObject.SetActive(false);
    }

    public void Init()
    {
        gameObject.SetActive(true);
    }

    public void NavigateToTitleMenu()
    {
        PhotonNetwork.LoadLevel(0);
        PhotonNetwork.LeaveRoom();
    }

    public void Quit()
    {
        PhotonNetwork.Disconnect();
        Application.Quit();
    }
    
}