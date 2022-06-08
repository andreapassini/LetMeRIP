using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Cinemachine;
using Photon.Pun;
using System;
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
    
    protected float timeStep = 0.2f;
    protected float timeSpent = 0f;
    protected IEnumerator timerCoroutine;

    #region UI
    [SerializeField] private TextMeshProUGUI timerText;
    #endregion

    protected virtual void Awake()
    {
        dungeon = gameObject.GetComponentInParent<Dungeon>();
        photonView = GetComponent<PhotonView>();

        connectedRooms = new List<Room>();
        gates = new Dictionary<int, Gate>();
        players = new Dictionary<int, PlayerController>();
    }

    protected virtual void Start()
    {
        foreach(Gate gate in inputGates)
        {
            gates[gate.photonView.ViewID] = gate;
         
            if (connectedRooms.Contains(gate.room)) continue;
            connectedRooms.Add(gate.room);
        }
    }

    public void EnterPlayer(PlayerController player) 
    {
        int playerViewID = player.photonView.ViewID;
        if (players.ContainsKey(playerViewID)) return;

        players[playerViewID] = player;
        if (players.Count == 1) Init();
        //Debug.Log($"{playerViewID} entered room {photonView.ViewID}");
    }

    public void ExitPlayer(PlayerController player)
    {
        int playerViewID = player.photonView.ViewID;
        if (!players.ContainsKey(playerViewID)) return;

        players.Remove(playerViewID);
        if (players.Count == 0) Exit();
        //Debug.Log($"{playerViewID} exited room {photonView.ViewID}");
    }

    /**
     * Called when entered
     */
    protected virtual void Init() 
    {
        if (!PhotonNetwork.IsMasterClient) return; // it just means that this gets executed just once, and it'll be from the master
        //Debug.Log($"room {photonView.ViewID} Init");

        timeSpent = 0f;
        timerCoroutine = Timer();
        StartCoroutine(timerCoroutine);
    }

    /**
     * Called when exited
     */
    protected virtual void Exit() 
    {
        if (!PhotonNetwork.IsMasterClient) return; // it just means that this gets executed just once, and it'll be from the master
        Debug.Log($"room {photonView.ViewID} Exit");

        StopCoroutine(timerCoroutine); // works but it's pretty meh with we have other coroutines
        Debug.Log($"time: {timeSpent}");
    }

    public void CloseInnerGates()
    {
        foreach (Gate gate in gates.Values) gate.Close();
        // TODO: signal closed gates
    }

    public void CloseOuterGates()
    {
        foreach (Gate gate in gates.Values) gate.connection.Close();
    }

    public void OpenInnerGates()
    {
        foreach (Gate gate in gates.Values) gate.Open();
        // TODO: signal opened gates
    }

    public void OpenOuterGates()
    {
        foreach (Gate gate in gates.Values) gate.connection.Open();
    }

    private IEnumerator Timer()
    {
        for (; ; )
        {
            timerText.text = timeSpent.ToString("0.00");
            yield return new WaitForSeconds(timeStep);
            timeSpent += timeStep;
            //StartCoroutine(Timer());
        }
    }
}
