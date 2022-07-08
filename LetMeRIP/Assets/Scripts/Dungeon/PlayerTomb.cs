using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerTomb : Interactable, IOnEventCallback
{
    private float segmentCost = 2f;
    private int segments = 4;
    private int remainingSegments;
    private byte remainingSegmentsEventCode = 1;
    private PlayerController cc;

    protected override void Start()
    {
        base.Start();
        remainingSegments = segments;
    }
    
    public override void Effect(PlayerController characterController)
    {
        base.Effect(characterController);
        cc = characterController;
        if (characterController.IsMine)
        {
            bool consumedSP = characterController.SGManager.ConsumeSP(segmentCost);
            if (consumedSP)
            {
                remainingSegments--;
                Debug.Log("preparing event");
                object[] content = new object[] { remainingSegments, photonView.ViewID };
                RaiseEventOptions raiseEventOptions = new RaiseEventOptions { Receivers = ReceiverGroup.All };
                PhotonNetwork.RaiseEvent(remainingSegmentsEventCode, content, raiseEventOptions, SendOptions.SendReliable);
            }
        }
    }

    private void ReviveSpirit()
    {
        if (!photonView.IsMine) return;

        GameObject player = PhotonNetwork.Instantiate("Prefabs/SpiritCharacter", transform.position, transform.rotation);
        StartCoroutine(LateHeal(player));
        PhotonNetwork.Destroy(gameObject);
    }

    private IEnumerator LateHeal(GameObject player)
    {
        yield return new WaitForSeconds(0.2f);
        PlayerController p = player.GetComponent<PlayerController>();
        p.HPManager.Heal(p.stats.maxHealth);
    }

    [PunRPC]
    private void RpcSetConsumedSP(int remainingSegments) 
    {
        this.remainingSegments = remainingSegments;
    }
    private void OnEnable()
    {
        PhotonNetwork.AddCallbackTarget(this);
    }

    private void OnDisable()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    public void OnEvent(EventData photonEvent)
    {
        byte eventCode = photonEvent.Code;
        if (eventCode == remainingSegmentsEventCode)
        {
            object[] data = (object[])photonEvent.CustomData;
            this.remainingSegments = (int)data[0];
            Debug.Log($"Data: {remainingSegments}");
            if (this.remainingSegments <= 0 && photonView.IsMine && photonView.ViewID == (int)data[1])
            {
                ReviveSpirit();
            }
        }
    
    }
}
