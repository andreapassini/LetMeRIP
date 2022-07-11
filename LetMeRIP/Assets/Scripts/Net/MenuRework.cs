using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Bolt;
using Photon.Bolt.Matchmaking;
using UdpKit;
using System;

public class MenuRework : GlobalEventListener
{
    public void StartServer()
    {
        BoltLauncher.StartServer();
    }
    public override void BoltStartDone()
    {
        base.BoltStartDone();
        // Create a session

        BoltMatchmaking.CreateSession(sessionID: "test", sceneToLoad: "TestNet");
    }

    public void StartClient()
    {
        BoltLauncher.StartClient();
    }

    public override void SessionListUpdated(Map<Guid, UdpSession> sessionList)
    {
        foreach (var session in sessionList)
        {
            UdpSession photonSession = session.Value as UdpSession;

            if(photonSession.Source == UdpSessionSource.Photon)
            {
                BoltMatchmaking.JoinSession(photonSession);
            }
        }
    }
}
