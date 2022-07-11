using System;
using Photon.Hive.Plugin;

namespace TestPlugins
{
    class HttpRequestNullCallInfoPlugin : TestPluginBase
    {
        public HttpRequestNullCallInfoPlugin()
        {
        }

        public override void OnRaiseEvent(IRaiseEventCallInfo info)
        {
            switch (info.Request.EvCode)
            {
                case 0:// all good
                {
                        var request = new HttpRequest
                        {
                            Async = true,
                            Callback = HttpCallback,
                            Url = "https://wt-e4c18d407aa73a40e4182aaf00a2a2eb-0.run.webtask.io/auth/auth-demo/",
                        };

                        this.PluginHost.HttpRequest(request, null);
                        info.Continue();
                }
                break;
                case 1:// no continue call
                {
                        var request = new HttpRequest
                        {
                            Async = true,
                            Callback = HttpCallback,
                            Url = "https://wt-e4c18d407aa73a40e4182aaf00a2a2eb-0.run.webtask.io/auth/auth-demo/",
                        };

                        this.PluginHost.HttpRequest(request, null);
                }
                break;
                case 2:// sync call
                {
                    var request = new HttpRequest
                    {
                        Async = false,
                        Callback = HttpCallback,
                        Url = "https://wt-e4c18d407aa73a40e4182aaf00a2a2eb-0.run.webtask.io/auth/auth-demo/",
                    };

                    this.PluginHost.HttpRequest(request, null);
                    info.Continue();
                }
                break;

                default:
                    base.OnRaiseEvent(info);
                    break;
            }
        }

        protected override void ReportError(short errorCode, Exception exception, object state)
        {
            string msg;
            if (errorCode == Photon.Plugins.Common.ErrorCodes.UnhandledException)
            {
                msg = string.Format("Got error report with exception {0}", exception);
            }
            else
            {
                msg = string.Format("Got error report with code: {0}", errorCode);
            }

            this.PluginHost.BroadcastErrorInfoEvent(msg);
        }

        private void HttpCallback(IHttpResponse response, object userState)
        {
        }
    }
}
