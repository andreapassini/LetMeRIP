using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/**
 * A set of linked rooms, with a start and an end.
 * Handles the switch between rooms
 */
public class Dungeon : MonoBehaviour
{
    public List<PlayerController> players;
    [SerializeField] private List<Room> rooms;
    private Room current;
    
    private void Start()
    {
        players = new List<PlayerController>(FindObjectsOfType<PlayerController>()); // quite slow but it's done only once
        rooms = new List<Room>(gameObject.GetComponentsInChildren<Room>());
        current = rooms[0];
        current.Init();
    }

    public void Switch(Gate gate)
    {
        Debug.Log("Switching");
        if(gate != null && gate.spawnPoint != null && gate.room != null)
        {
            current.Exit();
            foreach(PlayerController player in players)
            {
                player.transform.position = gate.connection.spawnPoint.position;
            }
            current = gate.room;
            current.Init();
        }
    }


}
