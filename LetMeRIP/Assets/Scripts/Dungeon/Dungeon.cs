using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/**
 * A set of linked rooms, with a start and an end.
 * Handles the switch between rooms
 */
public class Dungeon : MonoBehaviourPun
{
    [SerializeField] private List<Room> inputRooms;
    [SerializeField] private Gate startGate;

    public Dictionary<int, PlayerController> players;
    public Dictionary<int, Room> rooms;
    
    private void Start()
    {
        players = new Dictionary<int, PlayerController>();
        rooms = new Dictionary<int, Room>();
        
        inputRooms = new List<Room>(gameObject.GetComponentsInChildren<Room>());
        foreach(Room room  in inputRooms)
            rooms[room.photonView.ViewID] = room;
        StartCoroutine(LateStart());
    }

    [PunRPC]
    private void AddPlayer()
    {
        foreach (PlayerController player in FindObjectsOfType<PlayerController>())
        {
            if (!players.ContainsKey(player.photonView.ViewID))
            {
                players[player.photonView.ViewID] = player;
                if (player.photonView.IsMine) Switch(startGate, player.photonView.ViewID);
            }
        }
    }

    /**
     * If performed in the start the players are not yet spawned and the procedure fails
     */
    private IEnumerator LateStart()
    {
        yield return new WaitForSeconds(1f);
        photonView.RPC("AddPlayer", RpcTarget.All);
    }

    public void Switch(Gate gate, int playerViewID)
    {
        RefreshPlayerList(playerViewID);
        if (!players[playerViewID].photonView.IsMine) return;

        Debug.Log("Switching");
        if(gate != null && gate.spawnPoint != null && gate.room != null)
        {
            photonView.RPC("RpcSwitch", RpcTarget.All, players[playerViewID].photonView.ViewID, gate.room.photonView.ViewID, gate.photonView.ViewID);
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

            //rooms[roomViewID].players.Remove(playerViewID);
            rooms[roomViewID].ExitPlayer(player);
            Debug.Log($"Old: {string.Join(",", rooms[roomViewID].players.Keys)}");
            //gate.connection.room.players[playerViewID] = player;
            gate.connection.room.EnterPlayer(player);
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
