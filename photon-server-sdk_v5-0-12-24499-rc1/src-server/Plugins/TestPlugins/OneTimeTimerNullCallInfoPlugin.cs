using System;
using Photon.Hive.Plugin;

namespace TestPlugins
{
    class OneTimeTimerNullCallInfoPlugin : TestPluginBase
    {
        public OneTimeTimerNullCallInfoPlugin()
        {
        }
        public override void OnRaiseEvent(IRaiseEventCallInfo info)
        {
            switch (info.Request.EvCode)
            {
                case 0:// all good
                {
                    this.PluginHost.CreateOneTimeTimer(null, () => { }, 10); 
                    info.Continue();
                }
                break;
                case 1:// no continue call
                {
                    this.PluginHost.CreateOneTimeTimer(null, () => { }, 10); 
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
    }
}
