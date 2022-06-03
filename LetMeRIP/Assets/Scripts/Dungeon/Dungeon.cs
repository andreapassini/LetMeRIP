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
    [SerializeField] private List<Room> inputRooms;
    [SerializeField] private Gate startGate;

    public Dictionary<int, PlayerController> players;
    public Dictionary<int, Room> rooms;
    
    private void Start()
    {
        photonView = GetComponent<PhotonView>();
        players = new Dictionary<int, PlayerController>();
        rooms = new Dictionary<int, Room>();
        
        inputRooms = new List<Room>(gameObject.GetComponentsInChildren<Room>());
        foreach(Room room  in inputRooms)
            rooms[room.photonView.ViewID] = room;

        StartCoroutine(LateStart());

        //current = inputRooms[0];
        //current.Init();
    }

    private IEnumerator LateStart()
    {
        if (!PhotonNetwork.IsMasterClient) yield break;

        yield return new WaitForSeconds(3f);
        Debug.Log("Started");
        foreach (PlayerController player in FindObjectsOfType<PlayerController>())
            players[player.photonView.ViewID] = player;

        //foreach (int key in players.Keys)
        //    Switch(startGate, key);
    }

    public void Switch(Gate gate, int playerViewID)
    {
        RefreshPlayerList(playerViewID);
        if (!players[playerViewID].photonView.IsMine) return;

        Debug.Log("Switching");
        if(gate != null && gate.spawnPoint != null && gate.room != null)
        {
            //current.Exit();
            //Debug.Log($"Exited {current.name}");
            photonView.RPC("RpcSwitch", RpcTarget.All, players[playerViewID].photonView.ViewID, gate.room.photonView.ViewID, gate.photonView.ViewID);
            //current = gate.connection.room;
            //current.Init();
            //Debug.Log($"Initiated {current.name}");
        }
    }

    [PunRPC]
    private void RpcSwitch(int playerViewID, int roomViewID, int gateViewID) // the room view id is not necessary, but it speeds this process up
    {
        PlayerController player = players[playerViewID];
        Gate gate = rooms[roomViewID].gates[gateViewID];
        if (player != null)
        {
            player.transform.position = gate.connection.spawnPoint.position;

            rooms[roomViewID].players.Remove(playerViewID);
            Debug.Log($"Old: {string.Join(",", rooms[roomViewID].players.Keys)}");
            gate.connection.room.players[playerViewID] = player;
            Debug.Log($"New: {string.Join(",", gate.connection.room.players.Keys)}");
        }
    }

    private void RefreshPlayerList(int playerViewID)
    {
        if (!players.ContainsKey(playerViewID))
        {
            players.Clear();
            foreach (PlayerController player in FindObjectsOfType<PlayerController>())
                players[player.photonView.ViewID] = player;
        } 
    }
}
