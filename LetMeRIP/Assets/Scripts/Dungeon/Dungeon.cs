using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/**
 * A set of linked rooms, with a start and an end.
 * Handles the switch between rooms
 */
public class Dungeon : MonoBehaviour
{
    public PhotonView photonView;
    public List<PlayerController> players;
    [SerializeField] private List<Room> rooms;
    private Room current;
    
    private void Start()
    {
        photonView = GetComponent<PhotonView>();
        rooms = new List<Room>(gameObject.GetComponentsInChildren<Room>());
        current = rooms[0];
        current.Init();
    }


    public void Switch(Gate gate)
    {
        Debug.Log($"I am master client: {PhotonNetwork.IsMasterClient}");
        if (PhotonNetwork.IsMasterClient)
        {
            players = new List<PlayerController>(FindObjectsOfType<PlayerController>()); // quite slow but it's done only once

            Debug.Log("Switching");
            if(gate != null && gate.spawnPoint != null && gate.room != null)
            {
                current.Exit();
                Debug.Log($"Exited {current.name}");
                foreach(PlayerController player in players)
                {
                    photonView.RPC("RpcSwitch", RpcTarget.All, gate.connection.spawnPoint.position);
                    //player.transform.position = gate.connection.spawnPoint.position;
                }
                current = gate.connection.room;
                current.Init();
                Debug.Log($"Initiated {current.name}");
            }
        }
    }

    [PunRPC]
    private void RpcSwitch(Vector3 gatePosition)
    {
        players = new List<PlayerController>(FindObjectsOfType<PlayerController>()); // quite slow but it's done only once

        foreach (PlayerController player in players)
            player.transform.position = gatePosition;
    }
}
