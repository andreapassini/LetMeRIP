using System.IO;
using Photon.Pun;
using UnityEngine;

public class PlayerManager : MonoBehaviourPun
{
    public PlayerController.Stats spiritStats;
    public PlayerController.Stats bodyStats;

    [SerializeField] private string character = "Observer";

    private void Awake()
    {
        spiritStats = new PlayerController.Stats();
        bodyStats = new PlayerController.Stats();
        spiritStats.formName = "";
        bodyStats.formName = "";
        character = ClassSelector.Instance.SelectedClass.ToString();
    }

    private void Start()
    {
        if (!photonView.IsMine) return;
        CreateController();
    }

    private void CreateController()
    {
        if (character.ToLower().Equals("observer"))
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            CinemachineSwitcher.Instance.SetState(1);
            GameObject.Find("HUD").SetActive(false);
        } else
        {
            CinemachineSwitcher.Instance.SetState(0);
            PhotonNetwork.Instantiate(Path.Combine("Prefabs", $"{character}Character"), Vector3.zero, Quaternion.identity);
            Destroy(GameObject.Find("ObserverPlayer"));
        }
    }


}