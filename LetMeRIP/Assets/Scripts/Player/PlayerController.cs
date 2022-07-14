using Cinemachine;
using Photon.Pun;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviourPunCallbacks, IPunInstantiateMagicCallback
{
    [Serializable]
    public class Stats
    {
        public string formName;
        public float health;
        public float maxHealth;
        public float spiritGauge;
        public float maxSpiritGauge;
        public float strength;
        public float dexterity;
        public float intelligence;

        public float defense;
        public float swiftness;

        public float critDamage;
        public float critChance;
        public bool isDead = false;
    }

    PlayerInputActions playerInputActions;

    [SerializeField] public string playerClass; // archer, mage or warrior
    public FormManager formManager;

    [SerializeField] private PlayerStats spiritStatsSrc;
    [SerializeField] private PlayerStats bodyStatsSrc;
    private PlayerStats currentStatsSrc;

    public Stats stats;
    public LookAtMouse lam;
    [HideInInspector] public Movement movement;
    [HideInInspector] public Rigidbody rb;

    [HideInInspector] public HPManager HPManager;
    [HideInInspector] public SGManager SGManager;

    // Name of the game object with the virtual camera
    // Name of the game object to follow with the camera
    [SerializeField] private Transform followPoint;
    
    private PlayerBillboard healthBar;
    public PlayerManager playerManager;

    public int ViewID { get => photonView.ViewID; }
    public bool IsMine { get => photonView.IsMine; }
    private bool isSpirit;
    public bool IsSpirit { get => isSpirit; }

    void Start()
    {
        //playerManager = new List<PlayerManager>(FindObjectsOfType<PlayerManager>()).Find(match => match.photonView.IsMine);
        if (photonView.IsMine)
        {
            photonView.RPC(nameof(RpcSetPlayerManager), RpcTarget.AllBuffered, new List<PlayerManager>(FindObjectsOfType<PlayerManager>()).Find(match => match.photonView.IsMine).photonView.ViewID);
        }
        PopulateStats();

        SetupCamera();
        lam = GetComponent<LookAtMouse>();
        rb = GetComponent<Rigidbody>();
        playerInputActions = new PlayerInputActions();

        HPManager ??= gameObject.AddComponent<HPManager>();
        SGManager ??= gameObject.AddComponent<SGManager>();


        formManager = playerClass.ToLower() switch
        {
            "spirit" => gameObject.AddComponent<SpiritFormManager>(),
            "archer" => gameObject.AddComponent<ArcherFormManager>(),
            "warrior" => gameObject.AddComponent<WarriorFormManager>(),
            "mage" => gameObject.AddComponent<MageFormManager>(),
            _ => gameObject.AddComponent<SampleFormManager>()
        };

        formManager.Init(this);

        HPManager.stats = stats;
        SGManager.stats = stats; // we want to keep using the body spirit gauge, since it's always shared, no matter the form

        movement ??= gameObject.AddComponent<Movement>();
        playerInputActions.Player.Enable();
        playerInputActions.Player.Movement.Enable();

        // hud subscription
        healthBar = this.GetComponentInChildren<PlayerBillboard>();
        healthBar.Init(this);

        if (photonView.IsMine) HudController.Instance.InitPlayerInfoBar(playerClass.ToLower(), this);
    }

    /**
     * fa cagare non giudicatemi, ho sonno
     */
    private void PopulateStats()
    {
        Stats currentStatsPM = new Stats(); currentStatsPM.formName = "";
        //bool spirit = playerClass.ToLower().Equals("spirit");


        if (isSpirit)
        {
            currentStatsSrc = spiritStatsSrc;
            if (playerManager.spiritStats.formName != "")
            {
                currentStatsPM = playerManager.spiritStats;
            }

        }
        else
        {
            currentStatsSrc = bodyStatsSrc;
            if (playerManager.bodyStats.formName != "")
            {
                currentStatsPM = playerManager.bodyStats;
            }

        }

        if (currentStatsPM.formName != "")
        {
            if (isSpirit)
            {
                currentStatsPM.spiritGauge = playerManager.bodyStats.spiritGauge;
                currentStatsPM.maxSpiritGauge = playerManager.bodyStats.maxSpiritGauge;
            }
            stats = currentStatsPM;
        }
        else
        {
            playerManager.bodyStats.formName = bodyStatsSrc.formName;
            playerManager.bodyStats.health = bodyStatsSrc.health;
            playerManager.bodyStats.maxHealth = bodyStatsSrc.maxHealth;
            playerManager.bodyStats.spiritGauge = bodyStatsSrc.spiritGauge;
            playerManager.bodyStats.maxSpiritGauge = bodyStatsSrc.maxSpiritGauge;
            playerManager.bodyStats.strength = bodyStatsSrc.strength;
            playerManager.bodyStats.dexterity = bodyStatsSrc.dexterity;
            playerManager.bodyStats.intelligence = bodyStatsSrc.intelligence;
            playerManager.bodyStats.defense = bodyStatsSrc.defense;
            playerManager.bodyStats.swiftness = bodyStatsSrc.swiftness;
            playerManager.bodyStats.critDamage = bodyStatsSrc.critDamage;
            playerManager.bodyStats.critChance = bodyStatsSrc.critChance;

            playerManager.spiritStats.formName = spiritStatsSrc.formName;
            playerManager.spiritStats.health = spiritStatsSrc.health;
            playerManager.spiritStats.maxHealth = spiritStatsSrc.maxHealth;
            playerManager.spiritStats.spiritGauge = bodyStatsSrc.spiritGauge;
            playerManager.spiritStats.maxSpiritGauge = bodyStatsSrc.maxSpiritGauge;
            playerManager.spiritStats.strength = spiritStatsSrc.strength;
            playerManager.spiritStats.dexterity = spiritStatsSrc.dexterity;
            playerManager.spiritStats.intelligence = spiritStatsSrc.intelligence;
            playerManager.spiritStats.defense = spiritStatsSrc.defense;
            playerManager.spiritStats.swiftness = spiritStatsSrc.swiftness;
            playerManager.spiritStats.critDamage = spiritStatsSrc.critDamage;
            playerManager.spiritStats.critChance = spiritStatsSrc.critChance;

            stats = new Stats();
            stats = isSpirit ? playerManager.spiritStats : playerManager.bodyStats;
        }
    }

    public void Init() 
    {
        if (photonView.IsMine)
        {
            photonView.RPC(nameof(RpcSetPlayerManager), RpcTarget.AllBuffered, new List<PlayerManager>(FindObjectsOfType<PlayerManager>()).Find(match => match.photonView.IsMine).photonView.ViewID);
        }
        
        SetupCamera();

        movement ??= gameObject.AddComponent<Movement>();
        lam ??= GetComponent<LookAtMouse>();
        lam.enabled = true;
        movement.enabled = true;

        if (formManager != null && photonView.IsMine) 
        {
            formManager.BindAbilities();
            formManager.EnableAbilities();
        }
    }

    public void Exit()
    {
        lam.enabled = false;
        movement.enabled = false;
        formManager.UnbindAbilities();
        formManager.DisableAbilities();

        if (isSpirit)
            playerManager.spiritStats = stats;
        else
            playerManager.bodyStats = stats;
    }

    // Set up the local virtual camera to follow this player character 
    private void SetupCamera()
    {
        if (!photonView.IsMine) return;
        
        CinemachineVirtualCamera vCam = FindObjectOfType<CinemachineVirtualCamera>();

        vCam.Follow = followPoint;
        vCam.LookAt = transform;
    }

    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        isSpirit = playerClass.ToLower().Equals("spirit");
        Init();
    }

    [PunRPC]
    private void RpcSetPlayerManager(int playerManagerViewID)
    {
        playerManager = PhotonView.Find(playerManagerViewID).GetComponent<PlayerManager>();
        PopulateStats();
    }

    private IEnumerator WaitAndRetry()
    {
        yield return new WaitForSeconds(0.1f);
        Init();
    }

    public void DisableAll()
    {
        lam.DisableLookAround();
        movement.Disable();
        formManager.DisableAbilities();
    }

    public void EnableAll()
    {
        lam.EnableLookAround();
        movement.Enable();
        formManager.EnableAbilities();
    }
}