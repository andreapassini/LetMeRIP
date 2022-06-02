using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// a connection between rooms, its parent must have the Room component
public class Gate : MonoBehaviour
{
    public PhotonView photonView;
    [HideInInspector] public Room room;
    public Gate connection;
    public Transform spawnPoint = null;

    public bool IsOpen { get => isOpen; }
    private bool isOpen = true;
    private Dungeon dungeon;

    private void Awake()
    {
        spawnPoint = transform.Find("spawnPoint"); // default spawn point
        dungeon = FindObjectOfType<Dungeon>();
        room = gameObject.GetComponentInParent<Room>();
    }

    private void Start()
    {
        photonView = GetComponent<PhotonView>();
    }

    public void Open() => isOpen = true;
    public void Close() => isOpen = false;

    private void OnTriggerEnter(Collider other)
    {
        photonView.RPC("RpcSendMessage", RpcTarget.All, "helo");
        //photonView.RPC("dungeon.Switch", RpcTarget.All, connection.spawnPoint.position);
        if (other.CompareTag("Player") && isOpen && other.GetComponentInParent<PhotonView>().IsMine && PhotonNetwork.IsMasterClient)
            dungeon.Switch(this);
    }

    [PunRPC]
    void RpcSendMessage(string msg)
    {
        Debug.Log(msg);
    }
}
