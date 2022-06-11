using Cinemachine;
using Photon.Pun;
using UnityEngine;

public class PlayerController : MonoBehaviourPunCallbacks, IPunInstantiateMagicCallback
{
    PlayerInputActions playerInputActions;

    [SerializeField] private string playerClass; // archer, mage or warrior
    public FormManager formManager;

    public PlayerStats spiritStats;
    public PlayerStats bodyStats;
    public PlayerStats currentStats;
    public PlayerStats wtf;
    private LookAtMouse lam;
    [HideInInspector] public Movement movement;
    [HideInInspector] public Rigidbody rb;

    [HideInInspector] public HPManager HPManager;
    [HideInInspector] public SGManager SGManager;

    // Name of the game object with the virtual camera
    // Name of the game object to follow with the camera
    [SerializeField] private Transform followPoint;

    public int ViewID { get => photonView.ViewID; }
    public bool IsMine { get => photonView.IsMine; }

    void Start()
    {
        currentStats = playerClass.ToLower().Equals("spirit") ? spiritStats : bodyStats;

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

        HPManager.Stats = currentStats;
        SGManager.stats = bodyStats; // we want to keep using the body spirit gauge, since it's always shared, no matter the form

        movement ??= gameObject.AddComponent<Movement>();
        playerInputActions.Player.Enable();
        playerInputActions.Player.Movement.Enable();

        // hud subscription
        HudController.Instance.InitPlayerInfoBar(playerClass.ToLower(), this);
    }

    public void Init() 
    {
        SetupCamera();

        movement ??= gameObject.AddComponent<Movement>();
        lam ??= GetComponent<LookAtMouse>();
        lam.enabled = true;
        movement.enabled = true;

        if (formManager != null)
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
        Init();
    }
}