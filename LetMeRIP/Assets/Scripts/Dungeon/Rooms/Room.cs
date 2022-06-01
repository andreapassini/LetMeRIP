using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
/**
 * A piece of a dungeon, it can contain enemies or/and rewards.
 * It keeps track of a number of things: the enemies in it, their spawn position and rate,
 * the time since the player came in and the gates for the next rooms if any.
 */
public class Room : MonoBehaviour
{
    /*[HideInInspector] */public List<Room> connectedRooms;
    public List<Gate> gates;
    private float timeStep = 0.2f;
    protected float timeSpent = 0f;
    protected RoomSpawner spawners;

    #region UI
    [SerializeField] private TextMeshProUGUI timerText;
    #endregion

    private void Awake()
    {
        spawners = gameObject.GetComponentInChildren<RoomSpawner>();
    }

    private void Start()
    {
        connectedRooms = new List<Room>();
        foreach(Gate gate in gates)
        {
            if (connectedRooms.Contains(gate.room)) continue;
            connectedRooms.Add(gate.room);
        }
    }

    /**
     * Called when entered
     */
    public virtual void Init() 
    {
        timeSpent = 0f;
        StartCoroutine(Timer());
        spawners.Init(); // there might be rooms without enemies
    }
    
    /**
     * Called when exited
     */
    public virtual void Exit() 
    {
        spawners.Exit(); // also pretty meh, we should handle multiple players not one so...
        StopAllCoroutines(); // works but it's pretty meh with we have other coroutines
        Debug.Log($"time: {timeSpent}");
    }

    public void CloseGates()
    {
        foreach (Gate gate in gates) gate.Close();
    }

    public void OpenGates()
    {
        foreach (Gate gate in gates) gate.Open();
    }

    private IEnumerator Timer()
    {
        timerText.text = timeSpent.ToString("0.00");
        yield return new WaitForSeconds(timeStep);
        timeSpent += timeStep;
        StartCoroutine(Timer());
    }
}
