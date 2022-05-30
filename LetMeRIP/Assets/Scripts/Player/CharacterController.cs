using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CharacterController : MonoBehaviour
{
    [SerializeField] private string playerClass; // archer, mage or warrior
    private FormManager formManager;
    
    public PlayerStats spiritStats;
    public PlayerStats bodyStats;
    [HideInInspector] public PlayerStats currentStats;

    public HPManager HPManager;
    public SGManager SGManager;
    // Start is called before the first frame update
    void Start()
    {
        currentStats = bodyStats;
        HPManager = gameObject.AddComponent<HPManager>();
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
    }
}
