using System.Threading;
using Photon.Common.Plugins;
using Photon.LoadBalancing.Common;

namespace Photon.LoadBalancing.MasterServer
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;

    using ExitGames.Logging;
    using ExitGames.Concurrency.Fibers;

    using Photon.SocketServer;
    using Photon.Common.LoadBalancer;
    using Photon.LoadBalancing.MasterServer.GameServer;
    using Photon.LoadBalancing.MasterServer.Lobby;
    using Photon.LoadBalancing.ServerToServer.Events;
    using Photon.Hive.Common.Lobby;
    using Photon.Hive.Plugin;
    using Photon.Hive.Configuration;

    using Photon.Common;
    using Photon.SocketServer.Diagnostics;

    public class GameApplication : IDisposable
    {
        #region Fields

        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        protected readonly PoolFiber fiber;

        private IDisposable expiryCheckDisposable;

        private readonly LinkedList<GameState.ExpiryInfo> expiryList = new LinkedList<GameState.ExpiryInfo>();

        private readonly bool forceGameToRemove;

        public readonly string ApplicationId;

        public readonly string Version;

        public readonly LoadBalancer<GameServerContext> LoadBalancer;

        public readonly PlayerCache PlayerOnlineCache;

        private readonly Dictionary<string, GameState> gameDict = new Dictionary<string, GameState>();

        private int lobbiesCount;

        private readonly List<string> excludedActors = new List<string>();

        #endregion

        #region Constructors/Destructors

        public GameApplication(string applicationId, string version, LoadBalancer<GameServerContext> loadBalancer)
            : this(new PoolFiber(), new PoolFiber(), applicationId, version, loadBalancer)
        {
        }

        protected GameApplication(PoolFiber fiber, PoolFiber playerCacheFiber,
            string applicationId, string version, LoadBalancer<GameServerContext> loadBalancer)
        {
            if (log.IsInfoEnabled)
            {
                log.InfoFormat("Creating application: appId={0}/{1}", applicationId, version);
            }

            this.forceGameToRemove = MasterServerSettings.Default.PersistentGameExpiryMinute == 0;

            this.ApplicationId = applicationId;
            this.LoadBalancer = loadBalancer;
            this.Version = version;
            this.PlayerOnlineCache = new PlayerCache(playerCacheFiber);
            this.LobbyFactory = new LobbyFactory(this);
            this.LobbyFactory.Initialize();
            this.LobbyStatsPublisher = new LobbyStatsPublisher(
                this.LobbyFactory,
                MasterServerSettings.Default.LobbyStatsPublishInterval,
                MasterServerSettings.Default.Limits.Lobby.MaxStatsPublished);

            this.fiber = fiber;
            this.fiber.Start();

            if (MasterServerSettings.Default.PersistentGameExpiryMinute != 0)
            {
                var checkTime = MasterServerSettings.Default.GameExpiryCheckPeriod*60000;
                this.expiryCheckDisposable = this.fiber.Schedule(this.CheckExpiredGames, checkTime);
            }

            this.UpdatePluginTraits();
            PluginSettings.ConfigUpdated += () =>
            {
                this.fiber.Enqueue(() => this.UpdatePluginTraits());
            };
        }

        ~GameApplication()
        {
            this.Dispose(false);
        }

        #endregion

        #region Properties
        public LobbyFactory LobbyFactory { get; protected set; }

        public LobbyStatsPublisher LobbyStatsPublisher { get; protected set; }

        public PluginTraits PluginTraits { get; protected set; }

        public int LobbiesCount
        {
            get { return this.lobbiesCount; }
        }

        public int GamesCount
        {
            get
            {
                lock (this.gameDict)
                {
                    return this.gameDict.Count;
                }
            }
        }

        public LogCountGuard WrongJoinActivityGuard { get; private set; } = new LogCountGuard(new TimeSpan(1, 0, 0));

        #endregion

        #region Publics

        public virtual void OnClientConnected(MasterClientPeer peer)
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("OnClientConnect: peerId={0}, appId={1}", peer.ConnectionId, this.ApplicationId);
            }

            // remove from player cache
            if (this.PlayerOnlineCache != null && string.IsNullOrEmpty(peer.UserId) == false)
            {
                this.PlayerOnlineCache.OnConnectedToMaster(peer);
            }
        }

        public virtual void OnClientDisconnected(MasterClientPeer peer)
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("OnClientDisconnect: peerId={0}, appId={1}", peer.ConnectionId, this.ApplicationId);
            }

            // remove from player cache
            if (this.PlayerOnlineCache != null && string.IsNullOrEmpty(peer.UserId) == false)
            {
                this.PlayerOnlineCache.OnDisconnectFromMaster(peer.UserId);
            }

            // unsubscribe from lobby statistic events
            if (this.LobbyStatsPublisher != null)
            {
                this.LobbyStatsPublisher.Unsubscribe(peer);
            }
        }

        public bool GetOrCreateGame(string gameId, AppLobby lobby, byte maxPlayer, GameServerContext gameServer, out GameState gameState, out ErrorCode errorCode, out string errorMsg)
        {
            return this.TryCreateGame(gameId, lobby, maxPlayer, gameServer, out gameState, out errorCode, out errorMsg);
        }

        public virtual bool TryCreateGame(string gameId, AppLobby lobby, byte maxPlayer, GameServerContext gameServer, out GameState gameState, out ErrorCode errorCode, out string errorMsg)
        {
            bool result = false;
            errorCode = ErrorCode.Ok;
            errorMsg = string.Empty;

            lock (this.gameDict)
            {
                if (this.gameDict.TryGetValue(gameId, out gameState) == false)
                {
                    gameState = new GameState(lobby, gameId, maxPlayer, gameServer);
                    this.gameDict.Add(gameId, gameState);
                    result = true;
                }
            }

            if (result)
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("Created game: gameId={0}, appId={1}", gameId, this.ApplicationId);
                }
            }
            else
            {
                errorCode = ErrorCode.GameIdAlreadyExists;
                errorMsg = LBErrorMessages.GameAlreadyExist;
            }

            return result;
        }

        public bool TryGetGame(string gameId, out GameState gameState)
        {
            lock (this.gameDict)
            {
                return this.gameDict.TryGetValue(gameId, out gameState);
            }
        }

        public void OnGameUpdateOnGameServer(UpdateGameEvent updateGameEvent, GameServerContext gameServer)
        {
            GameState gameState;

            lock (this.gameDict)
            {
                if (!this.gameDict.TryGetValue(updateGameEvent.GameId, out gameState))
                {
                    if (updateGameEvent.Reinitialize)
                    {
                        AppLobby lobby;
                        string errorMsg;
                        if (!this.LobbyFactory.GetOrCreateAppLobby(updateGameEvent.LobbyId, (AppLobbyType) updateGameEvent.LobbyType, out lobby, out errorMsg))
                        {
                            // getting here should never happen
                            if (log.IsWarnEnabled)
                            {
                                log.WarnFormat("Could not get or create lobby: name={0}, type={1}, ErrorMsg:{2}", updateGameEvent.LobbyId,
                                    (AppLobbyType) updateGameEvent.LobbyType, errorMsg);
                            }
                            return;
                        }

                        ErrorCode errorCode;
                        var maxPlayers = updateGameEvent.MaxPlayers.GetValueOrDefault(0);
                        this.GetOrCreateGame(updateGameEvent.GameId, lobby, maxPlayers, gameServer, out gameState, out errorCode, out errorMsg);
                        if (errorCode != ErrorCode.Ok)
                        {
                            log.WarnFormat("Error during game creation initiated by GS. GameId:'{0}', AppId:{2}. ErrorMsg:{1}",
                                updateGameEvent.GameId, errorMsg, this.ApplicationId);
                        }
                    }
                }
            }

            if (gameState != null)
            {
                if (gameState.GameServer != gameServer)
                {
                    return;
                }

                gameState.CreateRequest = updateGameEvent;
                gameState.Lobby.UpdateGameState(updateGameEvent, gameServer);
                return;
            }

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Game to update not found: {0}", updateGameEvent.GameId);
            }
        }

        public void OnGameRemovedOnGameServer(GameServerContext context, string gameId, byte removeReason)
        {
            bool found;
            GameState gameState;

            lock (this.gameDict)
            {
                found = this.gameDict.TryGetValue(gameId, out gameState);
            }

            if (found)
            {
                if (gameState.GameServer != context)
                {
                    if (log.IsDebugEnabled)
                    {
                        log.DebugFormat("Game found, but not removed because it is created on another game server.gameId:{0}, gs:{1}, gs2:{2}",
                            gameId, gameState.GameServer, context);
                    }
                    return;
                }
                if (!this.forceGameToRemove && removeReason == GameRemoveReason.GameRemoveClose && gameState.ShouldBePreservedInList)
                {
                    if (log.IsDebugEnabled)
                    {
                        log.DebugFormat("Game '{0}' will be preserved in game list for {1} minutes", gameState.Id,
                            MasterServerSettings.Default.PersistentGameExpiryMinute);
                    }

                    gameState.Lobby.ResetGameServer(gameState);
                    this.AddGameToExpiryList(gameState);
                }
                else
                {
                    gameState.Lobby.RemoveGame(gameId);
                }
            }
            else if (log.IsDebugEnabled)
            {
                log.DebugFormat("Game to remove not found: gameid={0}, appId={1}", gameId, this.ApplicationId);
            }
        }

        public bool RemoveGameByName(string gameId)
        {
            bool removed;

            lock (this.gameDict)
            {
                removed = this.gameDict.Remove(gameId);
            }

            if (log.IsDebugEnabled)
            {
                if (removed)
                {
                    log.DebugFormat("Removed game: gameId={0}, appId={1}", gameId, this.ApplicationId);
                }
                else
                {
                    log.DebugFormat("Game to remove not found: gameId={0}, appId={1}", gameId, this.ApplicationId);
                }
            }

            return removed;
        }

        public virtual void OnGameServerRemoved(GameServerContext gameServerContext)
        {
            this.LobbyFactory.OnGameServerRemoved(gameServerContext);
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        public virtual void OnBeginReplication(GameServerContext gameServerContext)
        {
            this.LobbyFactory.OnBeginReplication(gameServerContext);
        }

        public virtual void OnFinishReplication(GameServerContext gameServerContext)
        {
            this.LobbyFactory.OnFinishReplication(gameServerContext);
        }

        public virtual void OnStopReplication(GameServerContext gameServerContext)
        {
            this.LobbyFactory.OnStopReplication(gameServerContext);
        }

        public virtual void OnSendChangedGameList(Hashtable gameList)
        {
        }

        public virtual void OnSendGameList(Hashtable gameList)
        {
        }

        public virtual void IncrementLobbiesCount()
        {
            Interlocked.Increment(ref this.lobbiesCount);
        }

        public virtual void DecrementLobbiesCount()
        {
            Interlocked.Decrement(ref this.lobbiesCount);
        }

        public void AddToExcludedActors(string userId)
        {
            bool contains;
            lock (this.excludedActors)
            {
                contains = excludedActors.Contains(userId);

                if (!contains)
                {
                    excludedActors.Add(userId);
                    //remove from list after token is expired (at some extra time for safety)
                    this.fiber.Schedule(() => RemoveFromExcludedActors(userId), Photon.Common.Authentication.Settings.Default.AuthTokenExpirationSeconds * 1000 * 2);
                }                
            }

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("AddToExcludedActors, userId '{0}', was already in list: {1}", userId, contains);
            }
        }

        //coud be private
        public void RemoveFromExcludedActors(string userId)
        {
            bool removed;
            lock (this.excludedActors)
            {
                removed = excludedActors.Remove(userId);
            }

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("RemoveFromExcludedActors, userId '{0}', removed: {1}", userId, removed);
            }
        }

        public bool IsActorExcluded(string userId)
        {
            bool excluded;
            lock (this.excludedActors)
            {
                excluded = excludedActors.Contains(userId);
            }
            return excluded;
        }

        #endregion

        #region Privates

        private static byte[] SerializeNew(object obj)
        {
            byte[] data;
            var photonRpc = Protocol.GpBinaryV162;
            using (var stream = new MemoryStream(4096))
            {
                photonRpc.Serialize(stream, obj);
                data = stream.ToArray();
            }
            return data;
        }

        private static object DeserializeNew(byte[] data)
        {
            var photonRpc = Protocol.GpBinaryV162;
            object obj;
            string errorMsg;
            photonRpc.TryParse(data, 0, data.Length, out obj, out errorMsg);
            return obj;
        }

        private void CheckExpiredGames()
        {
            var now = DateTime.UtcNow;
            var timeout = new TimeSpan(0, 0, MasterServerSettings.Default.GameExpiryCheckPeriod, 0);
            lock (this.expiryList)
            {
                var node = this.expiryList.First;
                while (node != null)
                {
                    var state = node.Value.Game;
                    if (state.GameServer != null)
                    {
                        if (log.IsDebugEnabled)
                        {
                            log.DebugFormat("Game '{0}' excluded from expiry list because has game server", state.Id);
                        }
                        node = this.RemoveFromExpiryList(state, node);
                    }
                    else if (now - node.Value.ExpiryStart > timeout)
                    {
                        if (log.IsDebugEnabled)
                        {
                            log.DebugFormat("Game '{0}' removed from lobby game list by timeout", state.Id);
                        }

                        state.Lobby.RemoveGame(state.Id);
                        node = this.RemoveFromExpiryList(state, node);
                    }
                    else
                    {
                        node = node.Next;
                    }
                }
            }

            if (ApplicationBase.Instance.Running)
            {
                var checkTime = MasterServerSettings.Default.GameExpiryCheckPeriod*60000;
                this.expiryCheckDisposable = this.fiber.Schedule(this.CheckExpiredGames, checkTime);
            }
        }

        private LinkedListNode<GameState.ExpiryInfo> RemoveFromExpiryList(GameState state, LinkedListNode<GameState.ExpiryInfo> node)
        {
            state.ExpiryListNode = null;
            var remove = node;
            node = node.Next;
            this.expiryList.Remove(remove);
            return node;
        }

        private void AddGameToExpiryList(GameState gameState)
        {
            lock (this.expiryList)
            {
                if (gameState.ExpiryListNode != null)
                {
                    if (log.IsDebugEnabled)
                    {
                        log.DebugFormat("Expiry time for game '{0}' updated", gameState.Id);
                    }

                    gameState.ExpiryListNode.Value.ExpiryStart = DateTime.UtcNow;
                }
                else
                {
                    if (log.IsDebugEnabled)
                    {
                        log.DebugFormat("Game '{0}' added to expiry list", gameState.Id);
                    }
                    gameState.ExpiryListNode = this.expiryList.AddLast(new GameState.ExpiryInfo(gameState, DateTime.UtcNow));
                }
            }
        }

        protected virtual void Dispose(bool dispose)
        {
            if (this.expiryCheckDisposable != null)
            {
                if (log.IsInfoEnabled && dispose) // we do not log during GC
                {
                    log.InfoFormat("Disposing game application:{0}/{1}", this.ApplicationId, this.Version);
                }

                this.expiryCheckDisposable.Dispose();
                this.expiryCheckDisposable = null;
            }

            if (this.fiber != null)
            {
                this.fiber.Enqueue(() => this.fiber.Dispose());
            }
        }

        private void UpdatePluginTraits()
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

                var pluginInfo = new PluginInfo
                {
                    Name = pluginSettings.Name,
                    Version = pluginSettings.Version,
                    AssemblyName = pluginSettings.AssemblyName,
                    Type = pluginSettings.Type,
                    ConfigParams = pluginSettings.CustomAttributes
                };

                this.PluginTraits = PluginTraits.Create(pluginInfo);
                return;
            }

            this.PluginTraits = PluginTraits.Create(new PluginInfo());
        }

        #endregion
    }
}
