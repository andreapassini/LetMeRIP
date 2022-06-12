using Photon.Pun;
using TMPro;
using UnityEngine;

public class PhotonLauncher : MonoBehaviourPunCallbacks
{
    [SerializeField] private TMP_InputField roomNameInputField;

    private void Start()
    {
        Debug.Log("Connecting to Master Server");

        if (PhotonNetwork.IsConnected) PhotonNetwork.Disconnect();
        PhotonNetwork.ConnectUsingSettings();
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("Connected to Master Server");
        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        MenuManager.Instance.OpenMenu("title");
        Debug.Log("Joined Lobby");
    }

    public void CreateRoom()
    {
        if (string.IsNullOrEmpty(roomNameInputField.text)) return;
        PhotonNetwork.CreateRoom(roomNameInputField.text);

        MenuManager.Instance.OpenMenu("loading");
    }

    public void JoinRoom()
    {
        if (string.IsNullOrEmpty(roomNameInputField.text)) return;
        PhotonNetwork.JoinRoom(roomNameInputField.text);

        MenuManager.Instance.OpenMenu("loading");
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("Joined Room");
        PhotonNetwork.LoadLevel(1);
        MenuManager.Instance.OpenMenu("joined");
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.Log("Room creation failed: " + message);
        MenuManager.Instance.OpenMenu("title");
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.Log("Join creation failed: " + message);
        MenuManager.Instance.OpenMenu("lobby");
    }

    public void LeaveRoom()
    {
        PhotonNetwork.LeaveRoom();
        MenuManager.Instance.OpenMenu("loading");
    }

    public override void OnLeftRoom()
    {
        MenuManager.Instance.OpenMenu("title");
    }
}