using System.Threading;
using Photon.Hive.Plugin;

namespace TestPlugins
{
    public class SyncAsyncHttpTestPlugin : PluginBase
    {
        public override string Name
        {
            get { return this.GetType().Name; }
        }
        

        public override void OnCreateGame(ICreateGameCallInfo info)
        {
            if (info.Request.GameId == "FirstCase" ||  info.Request.GameId == "SecondCase")
            {
                var callback = info.Request.GameId == "FirstCase" ? (HttpRequestCallback)this.HttpRequestCallbackForOnCreate 
                    : this.HttpRequestCallbackForOnCreate2;

                var async = info.Request.GameId == "FirstCase";
                var request = new HttpRequest
                {
                    Async = async,
                    Url = "https://wt-e4c18d407aa73a40e4182aaf00a2a2eb-0.run.webtask.io/realtime-webhooks-1.2/GameEvent",
                    Callback = callback,
                };
                this.PluginHost.HttpRequest(request, info);
            }
            else
            {
                base.OnCreateGame(info);
            }
        }


        private void HttpRequestCallbackForOnCreate(IHttpResponse response, object userstate)
        {
            response.CallInfo.Continue();
        }

        private void HttpRequestCallbackForOnCreate2(IHttpResponse response, object userstate)
        {
            this.PluginHost.CreateOneTimeTimer((ICallInfo)response.CallInfo, () => TimerCallback((ICallInfo)response.CallInfo), 100);
        }

        private void TimerCallback(ICallInfo info)
        {
            info.Continue();
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
                };
                // just to give next event time to reach plugin
                Thread.Sleep(100);
                this.PluginHost.HttpRequest(request, info);
            }
            else
            {
                this.PluginHost.BroadcastEvent(ReciverGroup.All, 0, 0, info.Request.EvCode, null, 0);
                info.Continue();
            }
        }

        private void HttpRequestCallback(IHttpResponse response, object userState)
        {
            var info = (IRaiseEventCallInfo)response.CallInfo;
            this.PluginHost.BroadcastEvent(ReciverGroup.All, 0, 0, info.Request.EvCode, null, 0);
            info.Continue();
        }
    }
}
