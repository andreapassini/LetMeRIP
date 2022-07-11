using System.Reflection;
using Photon.Hive.Plugin;

namespace TestPlugins
{
    class AllMethosCallHttpTestPlugin : TestPluginBase
    {
        private const string ConfigKey = "config";

        private HttpRequest request;
        private bool callBefore;
        private bool callAfter;
        private bool continueInCallback;

        private IPluginLogger logger;

        private void SetupPlugin(ICreateGameCallInfo info)
        {
            var config = (string) info.Request.GameProperties[ConfigKey];
            callAfter = config.Contains("After");
            callBefore = config.Contains("Before");
            continueInCallback = config.Contains("ContinueInCallback");

            this.logger = this.PluginHost.CreateLogger(typeof(AllMethosCallHttpTestPlugin).FullName);
            this.logger.Warn("Got next config '" + config + "' for game:" + info.Request.GameId);

            request = new HttpRequest
            {
                Async = config.Contains("Async"),
                Url = "https://wt-e4c18d407aa73a40e4182aaf00a2a2eb-0.run.webtask.io/realtime-webhooks-1.2/GameEvent",
            };

        }

        public override void OnCreateGame(ICreateGameCallInfo info)
        {
            this.SetupPlugin(info);

            this.MethodBody(info, MethodBase.GetCurrentMethod().Name);
        }

        public override void BeforeSetProperties(IBeforeSetPropertiesCallInfo info)
        {
            this.MethodBody(info, MethodBase.GetCurrentMethod().Name);
        }

        public override void OnSetProperties(ISetPropertiesCallInfo info)
        {
            this.MethodBody(info, MethodBase.GetCurrentMethod().Name);
        }

        public override void BeforeJoin(IBeforeJoinGameCallInfo info)
        {
            this.MethodBody(info, MethodBase.GetCurrentMethod().Name);
        }

        public override void OnJoin(IJoinGameCallInfo info)
        {
            this.MethodBody(info, MethodBase.GetCurrentMethod().Name);
        }

        public override void OnRaiseEvent(IRaiseEventCallInfo info)
        {
            this.MethodBody(info, MethodBase.GetCurrentMethod().Name);
        }

        public override void OnLeave(ILeaveGameCallInfo info)
        {
            this.MethodBody(info, MethodBase.GetCurrentMethod().Name);
        }

        public override void BeforeCloseGame(IBeforeCloseGameCallInfo info)
        {
            this.MethodBody(info, MethodBase.GetCurrentMethod().Name);
        }

        public override void OnCloseGame(ICloseGameCallInfo info)
        {
            this.MethodBody(info, MethodBase.GetCurrentMethod().Name);
        }

        private void MethodBody(ICallInfo info, string methodName)
        {
            this.logger.Warn("Executing method: " + methodName);

            if (this.callBefore)
            {
                this.logger.Warn("http call before continue method: " + methodName);
                this.DoHttpCall(info, (response, state) =>
                {
                    this.logger.Warn(methodName + " http callback for before continue call");
                    if (this.continueInCallback)
                    {
                        this.logger.Warn("continue from callback for method: " + methodName);
                        info.Continue();
                    }
                    this.BroadcastEvent(123, null);
                });
            }

            if (!this.continueInCallback)
            {
                this.logger.Warn("continue for method: " + methodName);
                info.Continue();
            }

            if (this.callAfter)
            {
                this.logger.Warn("http call before continue method: " + methodName);
                this.DoHttpCall(info, (response, state) =>
                {
                    this.logger.Warn(methodName + " http callback for after continue call");
                    if (this.continueInCallback)
                    {
                        this.logger.Warn("continue from callback for method: " + methodName);
                        info.Continue();
                    }
                    this.BroadcastEvent(123, null);
                });
            }
        }

        private void DoHttpCall(ICallInfo info, HttpRequestCallback callback)
        {
            request.Callback = callback;

            this.PluginHost.HttpRequest(request, info);
        }
    }
}
