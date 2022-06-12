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
                object[] content = new object[] { remainingSegments };
                RaiseEventOptions raiseEventOptions = new RaiseEventOptions { Receivers = ReceiverGroup.All };
                PhotonNetwork.RaiseEvent(remainingSegmentsEventCode, content, raiseEventOptions, SendOptions.SendReliable);
            }
        }
    }

    private void ReviveSpirit()
    {
        float spawnDistance = 0f;
        //if (Physics.Raycast(transform.position, transform.forward, out RaycastHit info, 50f))
        //{
        //    if (info.collider.CompareTag("Obstacle") && (transform.position - info.transform.position).magnitude < 4f)
        //        spawnDistance *= -1;
        //}
        if(!cc.IsMine)
        PhotonNetwork.Instantiate("Prefabs/SpiritCharacter", transform.position + spawnDistance * transform.forward, transform.rotation);
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
            if (this.remainingSegments <= 0 && photonView.IsMine)
            {
                ReviveSpirit();
                PhotonNetwork.Destroy(gameObject);
            }
        }
    
    }
}
