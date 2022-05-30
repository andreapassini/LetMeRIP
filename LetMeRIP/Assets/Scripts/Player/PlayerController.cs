using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    PlayerInputActions playerInputActions;

    [SerializeField] private string playerClass; // archer, mage or warrior
    private FormManager formManager;
    
    public PlayerStats spiritStats;
    public PlayerStats bodyStats;
    [HideInInspector] public PlayerStats currentStats;

    [HideInInspector] public HPManager HPManager;
    [HideInInspector] public SGManager SGManager;

    void Start()
    {
        playerInputActions = new PlayerInputActions();

        currentStats = bodyStats;
        HPManager = gameObject.AddComponent<HPManager>();
        SGManager = gameObject.AddComponent<SGManager>();
        HPManager.Stats = currentStats;
        SGManager.Stats = bodyStats; // we want to keep using the body spirit gauge, since it's always shared, no matter the form

        switch (playerClass.ToLower())
        {
            case "archer":
                formManager = gameObject.AddComponent<ArcherFormManager>();
                break;
            case "warrior":
                formManager = gameObject.AddComponent<WarriorFormManager>();
                break;
            case "mage":
                formManager = gameObject.AddComponent<MageFormManager>();
                break;
            case "sample":
                formManager = gameObject.AddComponent<SampleFormManager>();
                break;
            default:
                break;
        }
        formManager.Init(this);

        playerInputActions.Player.Enable();
        playerInputActions.Player.Movement.Enable();
    }
}
