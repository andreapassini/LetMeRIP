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
    // enemy count
    // time
    private float timeStep = 0.2f;
    protected float timeSpent = 0f;

    #region UI
    [SerializeField] private TextMeshProUGUI timerText;
    #endregion

    private void Start()
    {
        connectedRooms = new List<Room>();
        foreach(Gate gate in gates)
        {
            if (connectedRooms.Contains(gate.room)) continue;
            connectedRooms.Add(gate.room);
        }
        Debug.Log(gates[0].room.name);
    }

    /**
     * Called when entered
     */
    public virtual void Init() 
    {
        timeSpent = 0f;
        StartCoroutine(Timer());
    }
    
    /**
     * Called when exited
     */
    public virtual void Exit() 
    {
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
