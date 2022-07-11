using System;
using System.Reflection;
using Photon.Hive.Plugin;

namespace TestPlugins
{
    class OneTimeTimerTestPlugin : TestPluginBase
    {
        private const string ConfigKey = "config";

        private bool callBefore;
        private bool callAfter;
        private bool continueInCallback;
        private bool fromHttpCallback;
        private HttpRequest request;
        private bool doSyncHttpAndTimer;

        private void SetupPlugin(ICreateGameCallInfo info)
        {
            var config = (string)info.Request.GameProperties[ConfigKey];
            this.doSyncHttpAndTimer = config.Contains("FromRaiseInfoHttpCallAndTimer");
            if (!this.doSyncHttpAndTimer)
            {
                this.callAfter = config.Contains("After");
                this.callBefore = config.Contains("Before");
                this.continueInCallback = config.Contains("ContinueInCallback");
                this.fromHttpCallback = config.Contains("FromHttpCallback");

                
            }
            this.PluginHost.LogDebug("Got next config '" + config + "' for game:" + info.Request.GameId);

            request = new HttpRequest
            {
                Async = config.Contains("Async"),
                Url = "https://wt-e4c18d407aa73a40e4182aaf00a2a2eb-0.run.webtask.io/realtime-webhooks-1.2/GameEvent",
            };

        }

        public override void OnCreateGame(ICreateGameCallInfo info)
        {
            this.SetupPlugin(info);
            if (this.doSyncHttpAndTimer)
            {
                info.Continue();
                return;
            }
            this.MethodBody(info, MethodBase.GetCurrentMethod().Name);
        }

        private void MethodBody(ICallInfo info, string methodName)
        {
            this.PluginHost.LogDebug("Executing method: " + methodName);
            if (this.callBefore)
            {
                this.PluginHost.LogDebug("Creating timer before 'Continue'. method: " + methodName);
                this.CreateTimer(info, methodName);
            }

            if (!this.continueInCallback)
            {
                this.PluginHost.LogDebug("calling 'Continue' from method body . method: " + methodName);
                info.Continue();
            }

            if (this.callAfter)
            {
                this.PluginHost.LogDebug("Creating timer after 'Continue'. method: " + methodName);
                this.CreateTimer(info, methodName);
            }

            if (this.fromHttpCallback)
            {
                this.PluginHost.LogDebug("Http request. method: " + methodName);
                this.DoHttpCall(info, (response, state) =>
                {
                    this.PluginHost.LogDebug("Executing http callback for method:" + methodName);
                    this.CreateTimer(info, methodName);
                });
            }
        }

        public override void BeforeJoin(IBeforeJoinGameCallInfo info)
        {
            if (this.doSyncHttpAndTimer)
            {
                return;
            }
            this.MethodBody(info, MethodBase.GetCurrentMethod().Name);
        }

        public override void OnRaiseEvent(IRaiseEventCallInfo info)
        {
            this.MethodBody(info, MethodBase.GetCurrentMethod().Name);
        }

        private void DoHttpCall(ICallInfo info, HttpRequestCallback callback)
        {
            request.Callback = callback;

            this.PluginHost.HttpRequest(request, info);
        }

        private void CreateTimer(ICallInfo info, string methodName)
        {
            Action timerCallback = () =>
            {
                this.PluginHost.LogDebug("Executing timer callback for method:" + methodName);
                if (this.continueInCallback)
                {
                    info.Continue();
                }
                this.BroadcastEvent(123, null);
            };
            this.PluginHost.CreateOneTimeTimer(info, timerCallback, 100);
        }
    }
}
