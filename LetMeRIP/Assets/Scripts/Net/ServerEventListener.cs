using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Bolt;

public class ServerEventListener : GlobalEventListener
{
    public override void OnEvent(PlayerJoinedEvent evnt)
    {
        Debug.Log(evnt.Message);
    }
}
