using System;
using Photon.Hive.Plugin;

namespace TestPlugins.OldHttp
{
    class StrictModeFailurePluginOldHttp : TestPluginBase
    {
        private object timer;

        public StrictModeFailurePluginOldHttp()
        {
        }

        public override void OnRaiseEvent(IRaiseEventCallInfo info)
        {
            if (info.Request.EvCode == 0)
            {
                base.OnRaiseEvent(info);
            }
            else if (info.Request.EvCode == 1)
            {
#pragma warning disable CS0618 // Type or member is obsolete
                info.Defer();
#pragma warning restore CS0618 // Type or member is obsolete
            }
            else if (info.Request.EvCode == 2)
            {
                info.Cancel();
            }
            else if (info.Request.EvCode == 3)
            {
                info.Fail();
                this.PluginHost.BroadcastErrorInfoEvent("We called fail method");
            }
            else if (info.Request.EvCode == 4)
            {
                throw new Exception("Event 4 exception");
            }
            else if (info.Request.EvCode == 5)
            {
                var request = new HttpRequest
                {
                    Async = true,
                    Callback = HttpCallbackWithException,
                    Url = "https://wt-e4c18d407aa73a40e4182aaf00a2a2eb-0.run.webtask.io/auth/auth-demo/",
                };

#pragma warning disable CS0612 // Type or member is obsolete
                this.PluginHost.HttpRequest(request);
#pragma warning restore CS0612 // Type or member is obsolete
#pragma warning disable CS0618 // Type or member is obsolete
                info.Defer();
#pragma warning restore CS0618 // Type or member is obsolete
            }
            else if (info.Request.EvCode == 6)
            {
                var request = new HttpRequest
                {
                    Async = false,
                    Callback = HttpCallbackWithException,
                    Url = "https://wt-e4c18d407aa73a40e4182aaf00a2a2eb-0.run.webtask.io/auth/auth-demo/",
                };

#pragma warning disable CS0612 // Type or member is obsolete
                this.PluginHost.HttpRequest(request);
#pragma warning restore CS0612 // Type or member is obsolete
                info.Continue();
            }
            else if (info.Request.EvCode == 7)
            {
#pragma warning disable CS0612 // Type or member is obsolete
                this.timer = this.PluginHost.CreateOneTimeTimer(this.TimerAction, 100);
#pragma warning restore CS0612 // Type or member is obsolete
#pragma warning disable CS0618 // Type or member is obsolete
                info.Defer();
#pragma warning restore CS0618 // Type or member is obsolete
            }
            else if (info.Request.EvCode == 8)
            {
                this.timer = this.PluginHost.CreateTimer(this.TimerAction, 100, 100);
#pragma warning disable CS0618 // Type or member is obsolete
                info.Defer();
#pragma warning restore CS0618 // Type or member is obsolete
            }
        }

        private void TimerAction()
        {
            this.PluginHost.StopTimer(this.timer);
            throw new Exception("Timer callback exception simulation");
        }

        private void HttpCallbackWithException(IHttpResponse response, object userstate)
        {
            throw new Exception("Simulation of exception in http callback");
        }

        protected override void ReportError(short errorCode, Exception exception, object state)
        {
            string msg;
            if (errorCode == Photon.Hive.Plugin.ErrorCodes.UnhandledException)
            {
                msg = string.Format("Got error report with exception {0}", exception);
            }
            {
                msg = string.Format("Got error report with code: {0}", errorCode);
            }

            this.PluginHost.BroadcastErrorInfoEvent(msg);
        }

        public override void BeforeSetProperties(IBeforeSetPropertiesCallInfo info)
        {
            if (info.Request.ActorNumber == 0)
            {
                base.BeforeSetProperties(info);
            }
            else if (info.Request.ActorNumber == 1)
            {
                info.Cancel();
            }
            else if (info.Request.ActorNumber == 2)
            {
#pragma warning disable CS0618 // Type or member is obsolete
                info.Defer();
#pragma warning restore CS0618 // Type or member is obsolete
            }
            else if (info.Request.ActorNumber == 3)
            {
                info.Fail();
                this.PluginHost.BroadcastErrorInfoEvent("We called fail method");
            }
        }

        public override void OnSetProperties(ISetPropertiesCallInfo info)
        {
            if (!this.PluginHost.GameId.EndsWith("OnSetPropertiesForgotCall"))
            {
                base.OnSetProperties(info);
            }
        }

        public override void BeforeJoin(IBeforeJoinGameCallInfo info)
        {
            if (!this.PluginHost.GameId.EndsWith("BeforeJoinForgotCall"))
            {
                base.BeforeJoin(info);
            }
        }

        public override void OnJoin(IJoinGameCallInfo info)
        {
            if (!this.PluginHost.GameId.EndsWith("OnJoinForgotCall"))
            {
                base.OnJoin(info);
            }
        }

        public override void OnLeave(ILeaveGameCallInfo info)
        {
            if (!this.PluginHost.GameId.EndsWith("OnLeaveForgotCall"))
            {
                base.OnLeave(info);
            }
        }
    }
}
