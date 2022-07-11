using System;
using System.Collections.Generic;
using Photon.Hive.Plugin;

namespace TestPlugins
{
    class ApiConsistenceTestPlugin : TestPluginBase
    {
        public override void OnRaiseEvent(IRaiseEventCallInfo info)
        {
            var request = new HttpRequest
            {
                Url = "https://httpbin.org/response-headers",
                UserState = info,
                Method = info.Request.EvCode == 1 ? "GET" : "POST",
                Callback = this.Callback
            };

            if (this.PluginHost.GameId.Contains("OldHttp"))
            {

#pragma warning disable CS0612 // Type or member is obsolete
                PluginHost.HttpRequest(request);
#pragma warning restore CS0612 // Type or member is obsolete

                base.OnRaiseEvent(info);
            }
            else
            {
                PluginHost.HttpRequest(request, info);

                if (this.PluginHost.GameId.Contains("Async"))
                {
                    base.OnRaiseEvent(info);
                }
            }
        }

        private void Callback(IHttpResponse response, object userState)
        {
            if (!this.PluginHost.GameId.Contains("OldHttp") &&
                !this.PluginHost.GameId.Contains("Async"))
            {
                response.CallInfo.Continue();
            }

            try
            {
                // different checks to see that userState is correct
                var callInfo = (ICallInfo)userState;
                var callInfo2 = (ICallInfo) response.Request.UserState;
                if (!callInfo2.Equals(callInfo))
                {
                    this.BroadcastEvent(1, new Dictionary<byte, object> { { 0, "method parameter 'userState' is different from response.Request.UserState" } });
                    return;
                }

                // different checks to see that IHttpResponse.CallInfo is correct. for new http only
                var callInfo3 = (IRaiseEventCallInfo)response.CallInfo;
                if (!this.PluginHost.GameId.Contains("OldHttp") && callInfo3 == null)
                {
                    this.BroadcastEvent(1, new Dictionary<byte, object>
                    {
                        { 0, "callInfo can not be null" }
                    });
                    return;
                }
                this.BroadcastEvent(1, new Dictionary<byte, object> { {0, ""} });
            }
            catch (Exception e)
            {
                this.BroadcastEvent(1, new Dictionary<byte, object> { { 0, e.Message } });
            }
        }
    }
}
