using Photon.Hive.Plugin;

namespace TestPlugins
{
    class OnLeaveExceptionsPlugin : TestPluginBase
    {
        public override void OnLeave(ILeaveGameCallInfo info)
        {
            if (this.PluginHost.GameId.StartsWith("OnLeaveFailsInPlugins"))
            {
                throw new ExpectedTestException("Expected test exception");
            }

            var oldValue = this.PluginHost;
            if (this.PluginHost.GameId.StartsWith("OnLeaveNullsPluginHost"))
            {
                this.PluginHost = null;
                this.fireAssert = false;
            }

            try
            {
                base.OnLeave(info);
            }
            finally
            {
                this.PluginHost = oldValue;
                this.fireAssert = true;
            }
        }
    }
}
