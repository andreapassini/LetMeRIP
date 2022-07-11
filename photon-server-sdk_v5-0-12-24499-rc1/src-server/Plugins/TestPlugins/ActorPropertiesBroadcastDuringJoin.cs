using System;
using System.Collections;
using Photon.Hive.Plugin;

namespace TestPlugins
{
    class ActorPropertiesBroadcastDuringJoin : TestPluginBase
    {
        public override void OnCreateGame(ICreateGameCallInfo info)
        {
            if (this.PluginHost.GameId.Contains("SetOnCreateBeforeContinue"))
            {
                this.PluginHost.SetProperties(1, new Hashtable {{"xxx", true}}, null, true);
            }
            base.OnCreateGame(info);
            if (this.PluginHost.GameId.Contains("SetOnCreateAfterContinue"))
            {
                this.PluginHost.SetProperties(1, new Hashtable { { "xxx", true } }, null, true);
            }
        }

        public override void BeforeJoin(IBeforeJoinGameCallInfo info)
        {
            if (this.PluginHost.GameId.Contains("SetBeforeJoinBeforeContinue"))
            {
                this.PluginHost.SetProperties(2, new Hashtable { { "xxx", true } }, null, true);
            }
            base.BeforeJoin(info);

            if (this.PluginHost.GameId.Contains("SetBeforeJoinAfterContinue"))
            {
                this.PluginHost.SetProperties(2, new Hashtable { { "xxx", true } }, null, true);
            }
        }

        public override void OnJoin(IJoinGameCallInfo info)
        {
            if (this.PluginHost.GameId.Contains("SetOnJoinBeforeContinue"))
            {
                this.PluginHost.SetProperties(info.ActorNr, new Hashtable { { "xxx", true } }, null, true);
            }
            base.OnJoin(info);
            if (this.PluginHost.GameId.Contains("SetOnJoinAfterContinue"))
            {
                this.PluginHost.SetProperties(info.ActorNr, new Hashtable { { "xxx", true } }, null, true);
            }
        }

        protected override void ReportError(short errorCode, Exception exception, object state)
        {
            this.BroadcastEvent(124, null);
            base.ReportError(errorCode, exception, state);
            this.PluginHost.LogError("ReportError");
        }
    }
}
