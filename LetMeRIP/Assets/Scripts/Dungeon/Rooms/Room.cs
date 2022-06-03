using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Cinemachine;
using Photon.Pun;
/**
* A piece of a dungeon, it can contain enemies or/and rewards.
* It keeps track of a number of things: the enemies in it, their spawn position and rate,
* the time since the player came in and the gates for the next rooms if any.
*/
public class Room : MonoBehaviour
{
    public PhotonView photonView;
    private Dungeon dungeon;
    [SerializeField] private List<Gate> inputGates;
    
    [HideInInspector] public List<Room> connectedRooms;
    public Dictionary<int, Gate> gates;
    public Dictionary<int, PlayerController> players;
    
    private float timeStep = 0.2f;
    protected float timeSpent = 0f;
    protected RoomSpawner spawners;

    #region UI
    [SerializeField] private TextMeshProUGUI timerText;
    #endregion

    private void Awake()
    {
        spawners = gameObject.GetComponentInChildren<RoomSpawner>();
        dungeon = gameObject.GetComponentInParent<Dungeon>();
        photonView = GetComponent<PhotonView>();

        connectedRooms = new List<Room>();
        gates = new Dictionary<int, Gate>();
        players = new Dictionary<int, PlayerController>();
    }

    private void Start()
    {
        foreach(Gate gate in inputGates)
        {
            gates[gate.photonView.ViewID] = gate;
         
            if (connectedRooms.Contains(gate.room)) continue;
            connectedRooms.Add(gate.room);
        }
    }

    /**
     * Called when entered
     */
    public virtual void Init() 
    {
        if (!PhotonNetwork.IsMasterClient) return; // it just means that this gets executed just once, and it'll be from the master
        
        timeSpent = 0f;
        StartCoroutine(Timer());
        if(spawners != null) spawners.Init(); // there might be rooms without enemies
    }
    
    /**
     * Called when exited
     */
    public virtual void Exit() 
    {
        if (!PhotonNetwork.IsMasterClient) return; // it just means that this gets executed just once, and it'll be from the master

        if (spawners != null) spawners.Exit(); // also pretty meh, we should handle multiple players not one so...
        StopAllCoroutines(); // works but it's pretty meh with we have other coroutines
        Debug.Log($"time: {timeSpent}");
    }

    public void CloseGates()
    {
        foreach (Gate gate in gates.Values) gate.Close();
    }

    public void OpenGates()
    {
        foreach (Gate gate in gates.Values) gate.Open();
    }

    private IEnumerator Timer()
    {
        timerText.text = timeSpent.ToString("0.00");
        yield return new WaitForSeconds(timeStep);
        timeSpent += timeStep;
        StartCoroutine(Timer());
    }
}
