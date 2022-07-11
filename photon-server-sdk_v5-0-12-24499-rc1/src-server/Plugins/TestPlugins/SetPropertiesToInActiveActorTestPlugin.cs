using Photon.Hive.Plugin;
using System;
using System.Collections;
using System.Collections.Generic;

namespace TestPlugins
{
    class SetPropertiesToInActiveActorTestPlugin : TestPluginBase
    {
        public override void OnLeave(ILeaveGameCallInfo info)
        {
            base.OnLeave(info);

            if (info.ActorNr != 2)
            {
                return;
            }

            var msg = string.Empty;
            try
            {
                this.PluginHost.SetProperties(2, new Hashtable() { { "xx", "yy" } }, null, false);
            }
            catch(Exception e)
            {
                msg = e.Message;
            }

            this.BroadcastEvent(0, new Dictionary<byte, object> { { 0, msg } });
        }
    }
}
