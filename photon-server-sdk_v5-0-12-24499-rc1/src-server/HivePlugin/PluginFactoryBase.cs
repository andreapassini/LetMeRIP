using System.Collections.Generic;

namespace Photon.Hive.Plugin
{
    /// <summary>
    /// Base abstract class of plugin factory pattern.
    /// </summary>
    public abstract class PluginFactoryBase : IPluginFactory2
    {
        #region .flds

        protected IFactoryHost factoryHost;

        #endregion
        /// <summary>
        /// Create and initialize a new plugin instance.
        /// </summary>
        /// <param name="gameHost">The game to host the plugin instance.</param>
        /// <param name="pluginName">The plugin name as requested by client in Op CreateGame.</param>
        /// <param name="config">The plugin assembly key/value configuration entries.</param>
        /// <param name="errorMsg">An eventual error message to return in case something goes wrong.</param>
        /// <returns>The plugin instance or null.</returns>
        public IGamePlugin Create(IPluginHost gameHost, string pluginName, Dictionary<string, string> config, out string errorMsg)
        {
            var plugin = this.CreatePlugin(pluginName);

            if (plugin.SetupInstance(gameHost, config, out errorMsg))
            {
                return plugin;
            }
            return null;
        }

        public void SetFactoryHost(IFactoryHost fHost, FactoryParams factoryParams)
        {
            this.factoryHost = fHost;
        }

        /// <summary>
        /// Returns instance of the plugin.
        /// </summary>
        /// <param name="pluginName">The plugin name as requested by client in Op CreateGame.</param>
        /// <returns>The plugin instance or null.</returns>
        public abstract IGamePlugin CreatePlugin(string pluginName);
    }
}
