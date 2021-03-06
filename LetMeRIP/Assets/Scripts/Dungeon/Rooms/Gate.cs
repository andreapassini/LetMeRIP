using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// a connection between rooms, its parent must have the Room component
public class Gate : MonoBehaviourPun
{
    [HideInInspector] public Room room;
    private Animator animator;
    public Gate connection;
    public Transform spawnPoint = null;

    public bool IsOpen { get => isOpen && !isBlocked; }
    private bool isOpen = true;
    private Dungeon dungeon;
    
    [SerializeField] public bool isBlocked = false; // a second lock factor that requires another interactio un unlock (eg. a lever)

    private void Awake()
    {
        spawnPoint = transform.Find("spawnPoint"); // default spawn point
        dungeon = FindObjectOfType<Dungeon>();
        room = gameObject.GetComponentInParent<Room>();
        animator = GetComponentInChildren<Animator>();

    }

    private void Start()
    {
        animator.SetBool("isOpen", IsOpen);
    }

    public void Open()
    {
        isOpen = true;
        animator.SetBool("isOpen", IsOpen);
    }
    public void Close()
    {
        isOpen = false;
        animator.SetBool("isOpen", IsOpen);
    }

    public void Unblock()
    {
        isBlocked = false;
        animator.SetBool("isOpen", IsOpen);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && isOpen && !isBlocked)
            dungeon.Switch(this, other.GetComponent<PlayerController>().photonView.ViewID);
    }

    [PunRPC]
    void RpcSendMessage(string msg)
    {
        Debug.Log(msg);
    }
}
