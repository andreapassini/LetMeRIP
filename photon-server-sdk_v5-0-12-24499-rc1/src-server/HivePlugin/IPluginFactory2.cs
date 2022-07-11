using System.Collections.Generic;

namespace Photon.Hive.Plugin
{
    public class FactoryParams
    {
        public Dictionary<string, string> PluginConfig;
    }

    public interface IPluginFactory2 : IPluginFactory
    {
        void SetFactoryHost(IFactoryHost factoryHost, FactoryParams factoryParams);
    }
}