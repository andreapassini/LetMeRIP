using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
public class TrailerSpawner : MonoBehaviour
{
    [SerializeField] private InputAction action;
    RoomSpawner spawner;
    private void OnEnable()
    {
        action.Enable();
    }

    private void OnDisable()
    {
        action.Disable();
    }

    void Start()
    {
        spawner = GetComponent<RoomSpawner>();
        action.performed += _ => Spawn();
    }

    private void Spawn()
    {
        spawner.Spawn();
    }
}
