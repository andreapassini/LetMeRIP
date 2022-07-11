using System;
using System.Collections.Generic;
using Photon.Hive.Plugin;

namespace TestPlugins
{
    class ExpectedTestException : Exception
    {
        public ExpectedTestException(string msg)
            :base(msg)
        {
        }
    }
    abstract class TestPluginBase : PluginBase
    {
        public override string Name
        {
            get { return this.GetType().Name; }
        }

        protected override void ReportError(short errorCode, Exception exception, object state)
        {
            base.ReportError(errorCode, exception, state);

            this.PluginHost.LogError(string.Format("ErrorCode:{0}, exception:{1}", errorCode, exception != null ? exception.ToString() : "<no exception>"));
            this.BroadcastEvent(251, new Dictionary<byte, object>
            {
                {0, errorCode}, 
                {218, exception != null ? exception.ToString(): "no_exception"}
            });
        }
    }
}