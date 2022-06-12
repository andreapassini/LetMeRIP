using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;

public class PlayerListItem : MonoBehaviourPunCallbacks
{
    [SerializeField] private TMP_Text text;
    private Player player;
    
    
    public void Init(Player player)
    {
        this.player = player;
        this.text.text = player.NickName;

    }

    public override void OnLeftRoom()
    {
        Destroy(gameObject);
    }


    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if(player.Equals(otherPlayer)) Destroy(gameObject);
    }
}
