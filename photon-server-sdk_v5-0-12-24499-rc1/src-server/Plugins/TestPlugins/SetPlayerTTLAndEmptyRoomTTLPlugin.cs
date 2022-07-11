using Photon.Hive.Plugin;

namespace TestPlugins
{
    class SetPlayerTTLAndEmptyRoomTTLPlugin : TestPluginBase
    {
        public override void OnCreateGame(ICreateGameCallInfo info)
        {
            if (this.PluginHost.GameId.Contains("OnCreate"))
            {
                info.Request.GameProperties = info.Request.GameProperties ?? new System.Collections.Hashtable();
                info.Request.GameProperties.Add(GameParameters.PlayerTTL, 2000);
                info.Request.GameProperties.Add(GameParameters.EmptyRoomTTL, 2000);
            }
            base.OnCreateGame(info);
        }

        public override void OnRaiseEvent(IRaiseEventCallInfo info)
        {
            if (info.Request.EvCode == 1)
            {
                this.PluginHost.SetProperties(0, new System.Collections.Hashtable
                {
                    { GameParameters.PlayerTTL, 2000 },
                    { GameParameters.EmptyRoomTTL, 2000 }
                }, null, false);
            }
            base.OnRaiseEvent(info);
        }
    }
}
