using System.Collections.Generic;
using Photon.Hive.Plugin;

namespace TestPlugins
{
    class HttpLimitTestPlugin : TestPluginBase
    {
        private HttpPluginsTestListener listener;

        public HttpLimitTestPlugin(HttpPluginsTestListener httpTestListener)
        {
            this.listener = httpTestListener;
        }

        public override void OnCreateGame(ICreateGameCallInfo info)
        {
            var httpRequest = new HttpRequest
            {
                Async = false,
                Callback = HttpCallback,
                Url = this.listener.GetTooBigResponse()
            };

            this.PluginHost.HttpRequest(httpRequest, info);
        }

        private static void HttpCallback(IHttpResponse response, object userState)
        {
            response.CallInfo.Continue();
        }
    }
}
