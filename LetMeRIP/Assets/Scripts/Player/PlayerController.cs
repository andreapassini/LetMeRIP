using Cinemachine;
using Photon.Pun;
using UnityEngine;

public class PlayerController : MonoBehaviourPun
{
    PlayerInputActions playerInputActions;

    [SerializeField] private string playerClass; // archer, mage or warrior
    private FormManager formManager;

    public PlayerStats spiritStats;
    public PlayerStats bodyStats;
    [HideInInspector] public PlayerStats currentStats;

    [HideInInspector] public Movement movement;

    [HideInInspector] public HPManager HPManager;
    [HideInInspector] public SGManager SGManager;

    // Name of the game object with the virtual camera
    [SerializeField] private string playerCameraGOName = "PlayerCamera";
    // Name of the game object to follow with the camera
    [SerializeField] private string followPointGOName = "FollowPoint";

    public int ViewID { get => photonView.ViewID; }
    public bool IsMine { get => photonView.IsMine; }

    void Start()
    {
        SetupCamera();
        
        playerInputActions = new PlayerInputActions();
        currentStats = bodyStats;
        HPManager = gameObject.AddComponent<HPManager>();
        SGManager = gameObject.AddComponent<SGManager>();
        HPManager.Stats = currentStats;
        SGManager.Stats = bodyStats; // we want to keep using the body spirit gauge, since it's always shared, no matter the form
        
        formManager = playerClass.ToLower() switch
        {
            "archer" => gameObject.AddComponent<ArcherFormManager>(),
            "warrior" => gameObject.AddComponent<WarriorFormManager>(),
            "mage" => gameObject.AddComponent<MageFormManager>(),
            _ => gameObject.AddComponent<SampleFormManager>()
        };
        
        formManager.Init(this);

        movement = gameObject.AddComponent<Movement>();
        playerInputActions.Player.Enable();
        playerInputActions.Player.Movement.Enable();
    }

    // Set up the local virtual camera to follow this player character 
    private void SetupCamera()
    {
        if (!photonView.IsMine) return;
        
        Transform thisPlayerTransform = this.transform;
        
        GameObject vCam = GameObject.Find(playerCameraGOName);
        vCam.SetActive(true);

        var vCamComponent = vCam.GetComponent<CinemachineVirtualCamera>();
        vCamComponent.Follow = thisPlayerTransform.Find(followPointGOName).transform;
        vCamComponent.LookAt = thisPlayerTransform.transform;
    }
}