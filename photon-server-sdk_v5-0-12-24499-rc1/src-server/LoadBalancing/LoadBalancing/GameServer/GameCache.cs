// --------------------------------------------------------------------------------------------------------------------
// <copyright file="GameCache.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the GameCache type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Photon.LoadBalancing.GameServer
{
    using ExitGames.Logging;
    #region using directives

    using Photon.Hive;
    using Photon.Hive.Caching;
    using Photon.Hive.Plugin;
    using Photon.SocketServer;

    #endregion

    public class GameCache : RoomCacheBase
    {
        public GameApplication Application { get; protected set; }
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();
        private PluginManager pluginManager;
        private readonly object syncRoot = new object();


        public GameCache(GameApplication application)
        {
            this.Application = application;
            this.pluginManager = new PluginManager(application.ApplicationRootPath);
            Photon.Hive.Configuration.PluginSettings.ConfigUpdated += () => OnPluginSettingsUpdated();
        }

        private void OnPluginSettingsUpdated()
        {
            lock (this.syncRoot)
            {
                if (log.IsInfoEnabled)
                {
                    log.Info("Plugin config updated, trying to reload");
                }
                var newPluginManager = new PluginManager(this.Application.ApplicationRootPath);
                if (newPluginManager.Initialized)
                {
                    this.pluginManager = newPluginManager;
                    if (log.IsInfoEnabled)
                    {
                        log.Info("Plugin config updated succesfully");
                    }
                }
                else
                {
                    log.Error("Plugin config update failed");
                }
            }
        }

        public PluginManager PluginManager
        {
            get
            {
                lock (this.syncRoot)
                {
                    return this.pluginManager;
                }
            }
        }

        protected override Room CreateRoom(string roomId, params object[] args)
        {
            return new Game(new LBGameCreateOptions(this.Application, roomId, this, this.PluginManager));
        }
    }
}