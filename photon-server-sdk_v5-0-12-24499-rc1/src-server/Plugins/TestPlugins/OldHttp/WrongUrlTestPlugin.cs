using Photon.Hive.Plugin;

namespace TestPlugins.OldHttp
{
    class WrongUrlTestPluginOldHttp : TestPluginBase
    {
        public override void OnRaiseEvent(IRaiseEventCallInfo info)
        {
#pragma warning disable CS0612 // Type or member is obsolete
            this.PluginHost.HttpRequest(new HttpRequest
            {
                Url = "WrongUrl",
                Callback = this.HttpRequestCallback
            });
#pragma warning restore CS0612 // Type or member is obsolete
            base.OnRaiseEvent(info);
        }

        void HttpRequestCallback(IHttpResponse response, object userState)
        {
            if (response.Status == HttpRequestQueueResult.Error)
            {
                this.PluginHost.BroadcastErrorInfoEvent(response.Reason);
            }
        }

    }
}
