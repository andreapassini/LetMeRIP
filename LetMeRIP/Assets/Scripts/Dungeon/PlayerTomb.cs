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
    protected override void Start()
    {
        base.Start();
        remainingSegments = segments;
    }
    private PlayerController cc;
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
        GameObject what = PhotonView.Find(photonView.ViewID).gameObject;
        Debug.Log(what.name);
        Debug.Log($"THIS SHIT IS DEFINITELY MINE: {photonView.IsMine} | viewID: {photonView.ViewID}");
        if(photonView.IsMine) PhotonNetwork.Instantiate("Prefabs/SpiritCharacter", transform.position, transform.rotation);
        PhotonNetwork.Destroy(gameObject);
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
