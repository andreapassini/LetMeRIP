using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// a connection between rooms, its parent must have the Room component
public class Gate : MonoBehaviour
{
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

    public void Open() => isOpen = true;
    public void Close() => isOpen = false;

    private void OnTriggerEnter(Collider other)
    {
        if (isOpen && other.CompareTag("Player"))
            dungeon.Switch(this);
    }
}
