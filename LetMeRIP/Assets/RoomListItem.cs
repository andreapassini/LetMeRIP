using Photon.Realtime;
using TMPro;
using UnityEngine;

public class RoomListItem : MonoBehaviour
{
    [SerializeField] private TMP_Text text;

    private RoomInfo info;

    public void Init(RoomInfo info)
    {
        this.info = info;
        this.text.text = info.Name;
    }

    public void OnClick()
    {
        PhotonLauncher.Instance.JoinRoom(info);
    }
}