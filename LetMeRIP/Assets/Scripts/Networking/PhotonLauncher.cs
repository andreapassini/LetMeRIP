using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;

public class PhotonLauncher : MonoBehaviourPunCallbacks
{
    public static PhotonLauncher Instance;
    
    [SerializeField] private TMP_InputField roomNameInputField;
    [SerializeField] private TMP_Text roomNameText;
    [SerializeField] private Transform roomListContent;
    [SerializeField] private GameObject roomListItemPrefab;
    [SerializeField] private Transform playerListContent;
    [SerializeField] private GameObject playerListItemPrefab;
    [SerializeField] private GameObject startGameButton;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        Debug.Log("Connecting to Master Server");
        PhotonNetwork.ConnectUsingSettings();
    }
    
    public override void OnConnectedToMaster()
    {
        Debug.Log("Connected to Master Server");
        PhotonNetwork.JoinLobby();
        PhotonNetwork.AutomaticallySyncScene = true;
    }

    public override void OnJoinedLobby()
    {
        MenuManager.Instance.OpenMenu("title");
        
        PhotonNetwork.NickName = "Player " + Random.Range(0,1000).ToString("0000");
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

    public void JoinRoom(RoomInfo info)
    {
        PhotonNetwork.JoinRoom(info.Name);
        MenuManager.Instance.OpenMenu("loading");
    }

    public override void OnJoinedRoom()
    {
        MenuManager.Instance.OpenMenu("room");
        roomNameText.text = PhotonNetwork.CurrentRoom.Name;

        foreach (Transform trans in playerListContent) Destroy(trans.gameObject);
        
        foreach (Player player in PhotonNetwork.PlayerList) 
            Instantiate(playerListItemPrefab, playerListContent).GetComponent<PlayerListItem>().Init(player);
        
        startGameButton.SetActive(PhotonNetwork.IsMasterClient);
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        startGameButton.SetActive(PhotonNetwork.IsMasterClient);
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

    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        // clear list
        foreach (Transform trans in roomListContent) Destroy(trans.gameObject);

        // make list
        foreach (RoomInfo roomInfo in roomList)
        {
            if (roomInfo.RemovedFromList) continue;
            Instantiate(roomListItemPrefab, roomListContent).GetComponent<RoomListItem>().Init(roomInfo);
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {            
        Instantiate(playerListItemPrefab, playerListContent).GetComponent<PlayerListItem>().Init(newPlayer);
    }

    public void StartGame()
    {
        PhotonNetwork.LoadLevel(1);
    }
}