using Photon.Hive.Plugin;

namespace TestPlugins
{
    internal class BrokenPropsDuringGameCreationPlugin : TestPluginBase
    {
        public override void OnCreateGame(ICreateGameCallInfo info)
        {
            if (info.Request.GameProperties == null)
            {
                info.Request.GameProperties = new System.Collections.Hashtable();
            }

            if (info.Request.GameId.Contains("UseInternalProperties"))
            {
                info.Request.GameProperties.Add((byte)251, true);
                info.Request.GameProperties.Add((byte)252, 5);
            }
            else if (info.Request.GameId.Contains("WrongExpectedUsers"))
            {
                info.Request.GameProperties[GameParameters.ExpectedUsers] = new string[] { "user1", "user2", "user3", "user4", "user5" };
                info.Request.GameProperties[GameParameters.MaxPlayers] = 3;
            }
            else if (info.Request.GameId.Contains("WrongEmptyRoomTTL"))
            {
                info.Request.GameProperties[GameParameters.EmptyRoomTTL] = int.MaxValue;
            }
            else if (info.Request.GameId.Contains("SetPropertiesFromOnCreateGame"))
            {
                this.PluginHost.SetProperties(0, new System.Collections.Hashtable(), new System.Collections.Hashtable(), true);
            }
            else if (info.Request.GameId.Contains("WrongMasterClientId"))
            {
                info.Request.GameProperties[GameParameters.MasterClientId] = int.MaxValue;
            }
            base.OnCreateGame(info);
        }

        public override void OnRaiseEvent(IRaiseEventCallInfo info)
        {
            switch(info.Request.EvCode)
            {
            case 1:
                    //Set wrong room properties
                var gp = new System.Collections.Hashtable()
                {
                    { GameParameters.ExpectedUsers, new string[] { "user1", "user2", "user3", "user4", "user5" }}
                };
                this.PluginHost.SetProperties(0, gp, new System.Collections.Hashtable(), true);
                break;
            case 2:
                //Set wrong actor properties
                break;
            case 3:
                // Set wrong MasterPlayerId
                break;
            }
            base.OnRaiseEvent(info);
        }
    }
}
