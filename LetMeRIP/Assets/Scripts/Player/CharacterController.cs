using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CharacterController : MonoBehaviour
{
    public PlayerStats spiritStats;
    public PlayerStats bodyStats;
    [HideInInspector] public PlayerStats currentStats;
    [SerializeField] private string playerClass; // archer, mage or warrior
    private FormManager formManager;

    // Start is called before the first frame update
    void Start()
    {
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
    }
}
