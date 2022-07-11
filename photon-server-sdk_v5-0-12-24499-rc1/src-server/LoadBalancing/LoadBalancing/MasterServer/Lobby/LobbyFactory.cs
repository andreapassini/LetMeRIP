
using Photon.LoadBalancing.Common;

namespace Photon.LoadBalancing.MasterServer.Lobby
{
    using System.Collections.Generic;
    using System.Linq;

    using ExitGames.Logging;

    using Photon.LoadBalancing.MasterServer.GameServer;
    using Photon.Hive.Common.Lobby;

    public class LobbyFactory
    {
        #region Constants and Fields

        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private readonly Dictionary<KeyValuePair<string, AppLobbyType>, AppLobby> lobbyDict = new Dictionary<KeyValuePair<string, AppLobbyType>, AppLobby>();

        protected readonly GameApplication application;

        private readonly AppLobbyType defaultLobbyType; 

        private AppLobby defaultLobby;

        #endregion

        #region .ctr

        public LobbyFactory(GameApplication application) 
            : this(application, AppLobbyType.Default)
        {
        }

        protected LobbyFactory(GameApplication application, AppLobbyType defaultLobbyType)
        {
            this.application = application;
            this.defaultLobbyType = defaultLobbyType; 
        }

        #endregion

        #region Publics

        public void Initialize()
        {
            this.defaultLobby = this.CreateAppLobby(string.Empty, defaultLobbyType);

            var defaultLobbyKey = new KeyValuePair<string, AppLobbyType>(string.Empty, defaultLobbyType);
            this.lobbyDict.Add(defaultLobbyKey, this.defaultLobby);
        }
        
        // only returns true
        public bool GetOrCreateAppLobby(string lobbyName, AppLobbyType lobbyType , out AppLobby lobby, out string errorMsg)
        {
            if (string.IsNullOrEmpty(lobbyName))
            {
                lobby = this.defaultLobby;
                errorMsg = string.Empty;
                return true;
            }

            var key = new KeyValuePair<string, AppLobbyType>(lobbyName, lobbyType);

            lock (this.lobbyDict)
            {
                if (this.lobbyDict.TryGetValue(key, out lobby))
                {
                    errorMsg = string.Empty;
                    return true;
                }

                if (this.application.LobbiesCount >= MasterServerSettings.Default.Limits.Lobby.Total)
                {
                    lobby = null;
                    errorMsg = string.Format(LBErrorMessages.LobbiesLimitReached, MasterServerSettings.Default.Limits.Lobby.Total);
                    return false;
                }
                lobby = this.CreateAppLobby(lobbyName, lobbyType);
                this.lobbyDict.Add(key, lobby);
            }

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Created lobby: name={0}, type={1}", lobbyName, lobbyType);
            }

            errorMsg = string.Empty;
            return true;
        }
        
        public void OnGameServerRemoved(GameServerContext gameServer)
        {
            this.defaultLobby.RemoveGameServer(gameServer);

            lock (this.lobbyDict)
            {
                foreach (var lobby in this.lobbyDict.Values)
                {
                    lobby.RemoveGameServer(gameServer);
                }
            }
        }

        public AppLobby[] GetLobbies(int maxItems)
        {
            lock (this.lobbyDict)
            {
                if (maxItems > this.lobbyDict.Count)
                {
                    return this.lobbyDict.Values.ToArray();
                }

                var list = this.lobbyDict.Values.Take(maxItems);
                return list.ToArray();
            }
        }

        public AppLobby[] GetLobbies(string[] lobbyNames, byte[] lobbyTypes)
        {
            if (lobbyNames.Length == 0)
            {
                return new AppLobby[0];
            }

            var  appLobbies = new AppLobby[lobbyNames.Length];

            lock (this.lobbyDict)
            {
                for (int i = 0; i < lobbyNames.Length; i++)
                {
                    var key = new KeyValuePair<string, AppLobbyType>(lobbyNames[i], (AppLobbyType)lobbyTypes[i]);
                    AppLobby lobby;
                    this.lobbyDict.TryGetValue(key, out lobby);
                    appLobbies[i] = lobby;
                }
            }

            return appLobbies;
        }

        public void OnBeginReplication(GameServerContext gameServerContext)
        {
            this.defaultLobby.OnBeginReplication(gameServerContext);

            lock (this.lobbyDict)
            {
                foreach (var lobby in this.lobbyDict.Values)
                {
                    lobby.OnBeginReplication(gameServerContext);
                }
            }
        }

        public void OnStopReplication(GameServerContext gameServerContext)
        {
            this.defaultLobby.OnStopReplication(gameServerContext);

            lock (this.lobbyDict)
            {
                foreach (var lobby in this.lobbyDict.Values)
                {
                    lobby.OnStopReplication(gameServerContext);
                }
            }
        }

        public void OnFinishReplication(GameServerContext gameServerContext)
        {
            this.defaultLobby.OnFinishReplication(gameServerContext);

            lock (this.lobbyDict)
            {
                foreach (var lobby in this.lobbyDict.Values)
                {
                    lobby.OnFinishReplication(gameServerContext);
                }
            }
        }

        #endregion

        #region Methods

        protected virtual AppLobby CreateAppLobby(string lobbyName, AppLobbyType lobbyType)
        {
            return new AppLobby(this.application, lobbyName, lobbyType);
        }

        #endregion
    }
}
