// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PluginSettings.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;

using ExitGames.Logging;

using Microsoft.Extensions.Configuration;

using Photon.SocketServer;

namespace Photon.Hive.Configuration
{

    public class PluginSettings 
    {
        #region Constants and Fields
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();
        private static PluginSettings defaultInstance;
        private static readonly string configPath = ApplicationBase.Instance.BinaryPath + @"/plugin.config";
        private static readonly object syncRoot = new object();
        private static string pluginSettingsHash = string.Empty;

        #endregion

        public delegate void ConfUpdatedEventHandler();
        public static event ConfUpdatedEventHandler ConfigUpdated;

        #region Constructors and Destructors

        static PluginSettings()
        {
            UpdateSettings();
            var watcher = new FileSystemWatcher
            {
                Path = Path.GetDirectoryName(configPath),
                Filter = Path.GetFileName(configPath),
                NotifyFilter = NotifyFilters.LastWrite
            };
            watcher.Changed += PluginConfigurationChanged;
            watcher.EnableRaisingEvents = true;
        }

        private static bool UpdateSettings()
        {
            lock (syncRoot)
            {
                try
                {
                    if (!File.Exists(configPath))
                    {
                        if (log.IsInfoEnabled)
                        {
                            log.Info($"Plugin config file was not found. Default config is used path={configPath}");
                        }
                        defaultInstance = new PluginSettings();
                        return false;
                    }

                    using (var stream = new FileStream(configPath, FileMode.Open, FileAccess.Read))
                    {
                        var newHash = BitConverter.ToString(System.Security.Cryptography.SHA256.Create().ComputeHash(stream));
                        if (pluginSettingsHash == newHash)
                        {
                            return false;
                        }
                        pluginSettingsHash = newHash;
                    }

                    var cb = new ConfigurationBuilder();
                    cb.AddXmlFile(configPath, true);

                    var configuration = cb.Build();

                    var section = configuration.GetSection("PluginSettings").Get<PluginSettings>();
                    if (section != null)
                    {
                        section.Plugins.Clear();

                        var plugins = configuration.GetSection("PluginSettings:Plugins:Plugin");
                        foreach (var pluginSection in plugins.GetChildren())
                        {
                            var plugin = pluginSection.Get<PluginElement>();

                            foreach (var attribute in pluginSection.GetChildren())
                            {
                                switch (attribute.Key)
                                {
                                    case "Name": 
                                    case "Version": 
                                    case "Type": 
                                    case "AssemblyName":
                                        break;
                                    default:
                                        plugin.CustomAttributes.Add(attribute.Key, attribute.Value);
                                        break;
                                }
                            }

                            if (plugin != null)
                            {
                                section.Plugins.Add(plugin);
                            }
                        }

                        var validationResults = new List<ValidationResult>();
                        if (!Validator.TryValidateObject(section, new ValidationContext(section), validationResults, true))
                        {
                            var errorMsg = "Plugins Config is invalid!\n";
                            foreach (var validationResult in validationResults)
                            {
                                errorMsg += $"- {validationResult.ErrorMessage}\n";
                            }

                            throw new Exception(errorMsg);
                        }


                        defaultInstance = section;
                    }
                    else
                    {
                        defaultInstance = new PluginSettings();
                    }
                }
                catch (Exception e)
                {
                    log.ErrorFormat("Failed to load plugin settings from file, using default one. File: {0} e: {1}", configPath, e);
                };
            }
            return true;
        }

        private static void PluginConfigurationChanged(object sender, FileSystemEventArgs e)
        {
            var result = UpdateSettings();
            if (ConfigUpdated != null && result)
            {
                ConfigUpdated();
            }
        }

        #endregion

        #region Properties

        public static PluginSettings Default
        {
            get
            {
                lock (syncRoot)
                {
                    return defaultInstance;
                }
            }
        }

        public bool Enabled { get; set; } = false;

        public List<PluginElement> Plugins { get; } = new List<PluginElement>();

        #endregion
    }
}