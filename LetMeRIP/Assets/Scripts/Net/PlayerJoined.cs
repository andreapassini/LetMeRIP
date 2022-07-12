using Photon.Bolt;
using UnityEngine;

public class PlayerJoined : EntityBehaviour<ICustomCubeState>
{
    public override void Attached()
    {
        var evnt = PlayerJoinedEvent.Create();
        evnt.Message = "Hello there";
        
        evnt.Send();
    }
}
