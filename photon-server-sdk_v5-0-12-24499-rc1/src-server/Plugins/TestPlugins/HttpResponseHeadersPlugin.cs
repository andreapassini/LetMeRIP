using System.Collections.Generic;
using System.Text;
using Photon.Hive.Plugin;

namespace TestPlugins
{
    class HttpResponseHeadersPlugin : TestPluginBase
    {
        public override void OnCreateGame(ICreateGameCallInfo info)
        {
            Dictionary<string, string> expectedHeaders = new Dictionary<string, string>();
            // you can add as many as you want or even send them through unit test as actor or game properties
            expectedHeaders.Add("GameId", info.Request.GameId);
            expectedHeaders.Add("LobbyType", info.Request.LobbyType.ToString());
            expectedHeaders.Add("ActorNr", info.Request.ActorNr.ToString());
            expectedHeaders.Add("JoinMode", info.Request.JoinMode.ToString());
            expectedHeaders.Add("AppId", AppId);
            expectedHeaders.Add("Region", Region);
            expectedHeaders.Add("AppVersion", AppVersion);
            expectedHeaders.Add("UserId", info.UserId);
            expectedHeaders.Add("Nickname", info.Nickname);
            StringBuilder url = new StringBuilder("https://httpbin.org/response-headers?");
            foreach (var key in expectedHeaders.Keys)
            {
                url.AppendFormat("{0}={1}&", key, expectedHeaders[key]);
            }
            url.Remove(url.Length - 1, 1);

            HttpRequest request = new HttpRequest();
            request.Url = url.ToString();
            request.Callback = Callback;
            request.UserState = expectedHeaders;

            PluginHost.HttpRequest(request, info);
        }

        private void Callback(IHttpResponse response, object userState)
        {
            Dictionary<string, string> expectedHeaders = userState as Dictionary<string, string>;
            string errorMsg;
            foreach (var k in expectedHeaders.Keys)
            {
                string headerValue = response.Headers[k];
                if (headerValue == null)
                {
                    errorMsg = string.Format("Missing Response Header {0}", k);
                } else if (!headerValue.Equals(expectedHeaders[k]))
                {
                    errorMsg = string.Format("Wrong Response Header {0} value, expected {1} != received {2}", k,
                        headerValue, expectedHeaders[k]);
                }
                else
                {
                    continue;
                }
                response.CallInfo.Fail(errorMsg);
            }
            response.CallInfo.Continue();
        }
    }
}
