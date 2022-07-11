using System.Collections.Generic;
using Photon.Hive.Plugin;

namespace TestPlugins
{
    class CorrectOnLeaveTestPlugin : TestPluginBase
    {
        public override void OnLeave(ILeaveGameCallInfo info)
        {
            base.OnLeave(info);
            var msg = string.Empty;
            if (!this.PluginHost.GameId.Contains("NonZero"))
            {
                if (info.IsInactive)
                {
                    msg = "ILeaveCallInfo.IsInactive should be false";
                }
            }
            else
            {
                if (!info.IsInactive)
                {
                    msg = "ILeaveCallInfo.IsInactive should be true";
                }
            }
            this.BroadcastEvent(1, new Dictionary<byte, object> { {0, msg} });
        }
    }
}
