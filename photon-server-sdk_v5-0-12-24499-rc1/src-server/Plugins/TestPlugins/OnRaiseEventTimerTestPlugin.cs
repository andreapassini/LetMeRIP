using Photon.Hive.Plugin;

namespace TestPlugins
{
    class OnRaiseEventTimerTestPlugin : TestPluginBase
    {
        private const string ConfigKey = "config";
        private HttpRequest request;

        private bool noCallback;

        private void SetupPlugin(ICreateGameCallInfo info)
        {
            var config = (string)info.Request.GameProperties[ConfigKey];
            this.PluginHost.LogDebug("Got next config '" + config + "' for game:" + info.Request.GameId);

            this.noCallback = config.Contains("NoCallback");
            request = new HttpRequest
            {
                Async = config.Contains("Async"),
                Url = "https://wt-e4c18d407aa73a40e4182aaf00a2a2eb-0.run.webtask.io/realtime-webhooks-1.2/GameEvent",
            };

        }

        public override void OnCreateGame(ICreateGameCallInfo info)
        {
            this.SetupPlugin(info);
            base.OnCreateGame(info);
        }

        public override void OnRaiseEvent(IRaiseEventCallInfo info)
        {
            this.DoHttpCall(info, (response, state) =>
            {
                this.PluginHost.LogDebug("Executing http callback for OnRaiseEvent");
                this.PluginHost.CreateOneTimeTimer(info, () =>
                {
                    this.PluginHost.LogDebug("Executing timer callback for OnRaiseEvent");
                    if (!this.noCallback)
                    {
                        info.Continue();
                    }
                    this.BroadcastEvent(123, null);
                }, 1000);
            });
        }

        private void DoHttpCall(ICallInfo info, HttpRequestCallback callback)
        {
            request.Callback = callback;

            this.PluginHost.HttpRequest(request, info);
        }
    }
}
