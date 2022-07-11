using System.Threading;
using Photon.Hive.Plugin;

namespace TestPlugins.OldHttp
{
    public class SyncAsyncHttpTestPluginOldHttp : PluginBase
    {
        public override string Name
        {
            get { return this.GetType().Name; }
        }

        public override void OnRaiseEvent(IRaiseEventCallInfo info)
        {
            if (info.Request.EvCode != 3)
            {
                var request = new HttpRequest
                {
                    Async = info.Request.EvCode == 1,
                    Url = "https://wt-e4c18d407aa73a40e4182aaf00a2a2eb-0.run.webtask.io/realtime-webhooks-1.2/GameEvent",
                    Callback = this.HttpRequestCallback,
                    UserState = info,
                };
                // just to give next event time to reach plugin
                Thread.Sleep(100);
#pragma warning disable CS0612 // Type or member is obsolete
                this.PluginHost.HttpRequest(request);
#pragma warning restore CS0612 // Type or member is obsolete
            }
            else
            {
                this.PluginHost.BroadcastEvent(ReciverGroup.All, 0, 0, info.Request.EvCode, null, 0);
            }
        }

        private void HttpRequestCallback(IHttpResponse response, object userState)
        {
            var info = (IRaiseEventCallInfo) userState;
            this.PluginHost.BroadcastEvent(ReciverGroup.All, 0, 0, info.Request.EvCode, null, 0);
        }
    }
}
