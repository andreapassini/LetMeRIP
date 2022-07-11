using Photon.Plugins.Common;

namespace Photon.Hive.Plugin
{
    public class PluginLogger : Photon.Common.Plugins.PluginLogger, IPluginLogger
    {
        public PluginLogger(string name, IPluginLogMessagesCounter counter = null)
            : base(name)
        {
        }
    }
}