// --------------------------------------------------------------------------------------------------------------------
// <copyright file="HiveGameCache.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using Photon.SocketServer;

namespace Photon.Hive.Caching
{
    using System;
    using ExitGames.Logging;
    using Photon.Hive.Plugin;

    /// <summary>
    /// The cache for <see cref="HiveGame"/>s.
    /// </summary>
    public class HiveGameCache : RoomCacheBase
    {
        #region Constants and Fields

        /// <summary>
        /// The singleton instance.
        /// </summary>
        public static readonly HiveGameCache Instance = new HiveGameCache();
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();
        private PluginManager pluginManager;
        private readonly object syncRoot = new object();

        #endregion

        #region Properties

        public PluginManager PluginManager
        {
            get {
                lock(this.syncRoot)
                {
                    return this.pluginManager;
                }
            }
        }

        #endregion

        #region Construction

        public HiveGameCache()
        {
            this.pluginManager = new PluginManager(ApplicationBase.Instance.ApplicationRootPath);
            Photon.Hive.Configuration.PluginSettings.ConfigUpdated += () => OnPluginSettingsUpdated();
        }

        private void OnPluginSettingsUpdated()
        {
            lock(this.syncRoot)
            {
                if(log.IsInfoEnabled)
                {
                    log.Info("Plugin config updated, trying to reload");
                }
                var newPluginManager = new PluginManager(ApplicationBase.Instance.ApplicationRootPath);
                if(newPluginManager.Initialized)
                {
                    this.pluginManager = newPluginManager;
                    if (log.IsInfoEnabled)
                    {
                        log.Info("Plugin config updated successfully");
                    }
                }
                else
                {
                    log.Error("Plugin config update failed");
                }
            }
        }

        #endregion

        #region Methods

        protected override Room CreateRoom(string roomId, params object[] args)
        {
             return new HiveHostGame(new GameCreateOptions(roomId, this, this.PluginManager));
        }

        #endregion
    }
}