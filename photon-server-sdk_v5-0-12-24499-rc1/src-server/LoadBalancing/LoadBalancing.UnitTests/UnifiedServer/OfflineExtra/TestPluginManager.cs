using System;
using System.Collections.Generic;
using ExitGames.Logging;
using Photon.Hive.Configuration;
using Photon.Hive.Plugin;
using Photon.Plugins.Common;
using TestPlugins;
using EnvironmentVersion = Photon.Hive.Plugin.EnvironmentVersion;

namespace Photon.LoadBalancing.UnitTests.UnifiedServer.OfflineExtra
{
    public class TestPluginManager : IPluginManager
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private readonly IPluginLogMessagesCounter logMessagesCounter = NullPluginLogMessageCounter.Instance;
        private readonly PluginFactory factory;

        private readonly Dictionary<string, string> pluginConfig;

        public TestPluginManager()
        {
            var settings = PluginSettings.Default;
            if (settings.Enabled & settings.Plugins.Count > 0)
            {
                var pluginSettings = settings.Plugins[0];

                if (log.IsInfoEnabled)
                {
                    log.InfoFormat("Plugin configured: name={0}", pluginSettings.Name);

                    if (pluginSettings.CustomAttributes.Count > 0)
                    {
                        foreach (var att in pluginSettings.CustomAttributes)
                        {
                            log.InfoFormat("\tAttribute: {0}={1}", att.Key, att.Value);
                        }
                    }
                }

                this.pluginConfig = pluginSettings.CustomAttributes;
                this.factory = new PluginFactory();
                this.factory.SetFactoryHost(new FactoryHost(this.logMessagesCounter), new FactoryParams { PluginConfig = new Dictionary<string, string>() });
            }
        }

        public IPluginInstance GetGamePlugin(IPluginHost sink, string pluginName)
        {
            string errorMsg;
            try
            {
                if (this.factory == null)
                {
                    if (string.IsNullOrEmpty(pluginName) || pluginName == "Default")
                    {
                        return GetDefaultPlugin(sink);
                    }

                    return GetErrorPlugin(sink, "PluginManager initialization failed.");
                }


                var plugin = this.factory.Create(sink, pluginName, this.pluginConfig, out errorMsg);
                if (plugin != null)
                {
                    return new PluginInstance {Plugin = plugin, Version = GetEnvironmentVersion()};
                }
                log.ErrorFormat("Plugin {0} creation failed with message: {1}", pluginName, errorMsg);
            }
            catch (Exception e)
            {
                errorMsg = e.Message;
                log.ErrorFormat("Plugin {0} creation failed with exception. Exception Msg:{1}", pluginName, errorMsg);
            }

            return new PluginInstance { Plugin = new ErrorPlugin(errorMsg), Version = GetEnvironmentVersion()};
        }

        private static EnvironmentVersion GetEnvironmentVersion()
        {
            var currentPluginsVersion = typeof(PluginBase).Assembly.GetName().Version;
            return new EnvironmentVersion { BuiltWithVersion = currentPluginsVersion, HostVersion = currentPluginsVersion };
        }

        private static IPluginInstance GetDefaultPlugin(IPluginHost sink)
        {
            string errorMsg;
            IGamePlugin plugin = new PluginBase();
            plugin.SetupInstance(sink, null, out errorMsg);
            return new PluginInstance {Plugin = plugin, Version = new EnvironmentVersion()};
        }

        private static IPluginInstance GetErrorPlugin(IPluginHost sink, string msg)
        {
            string errorMsg;
            IGamePlugin plugin = new ErrorPlugin(msg);
            plugin.SetupInstance(sink, null, out errorMsg);
            return new PluginInstance {Plugin = plugin, Version = new EnvironmentVersion()};
        }

    }
}
