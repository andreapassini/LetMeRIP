using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using ExitGames.Logging;

using Photon.Common;
using Photon.Hive.Operations;
using Photon.LoadBalancing.Events;
using Photon.LoadBalancing.MasterServer.GameServer;
using Photon.LoadBalancing.Operations;
using Photon.LoadBalancing.ServerToServer.Events;
using Photon.SocketServer;

using EventCode = Photon.LoadBalancing.Events.EventCode;


namespace Photon.LoadBalancing.MasterServer.Lobby
{
    public abstract class GameListBase : IGameList
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        public readonly AppLobby Lobby;

        protected readonly Dictionary<string, GameState> changedGames;

        protected readonly LinkedListDictionary<string, GameState> gameDict;

        protected readonly HashSet<string> removedGames;

        protected readonly HashSet<PeerBase> peers = new HashSet<PeerBase>();

        protected LinkedListNode<GameState> nextJoinRandomStartNode;

        #region Constructors and Destructors

        protected GameListBase(AppLobby lobby)
        {
            this.Lobby = lobby;
            this.gameDict = new LinkedListDictionary<string, GameState>();
            this.changedGames = new Dictionary<string, GameState>();
            this.removedGames = new HashSet<string>();
        }

        #endregion


        #region Properties

        public int ChangedGamesCount
        {
            get
            {
                return this.changedGames.Count + this.removedGames.Count;
            }
        }

        public int Count
        {
            get
            {
                return this.gameDict.Count;
            }
        }

        public int PlayerCount { get; protected set; }

        #endregion

        #region Publics

        public virtual void AddGameState(GameState gameState, Dictionary<string, object> authCookie = null)
        {
            this.gameDict.Add(gameState.Id, gameState);
        }

        public int CheckJoinTimeOuts(TimeSpan timeOut)
        {
            DateTime minDate = DateTime.UtcNow.Subtract(timeOut);
            return this.CheckJoinTimeOuts(minDate);
        }

        public int CheckJoinTimeOuts(DateTime minDateTime)
        {
            int oldJoiningCount = 0;
            int joiningPlayerCount = 0;

            var toRemove = new List<GameState>();

            foreach (GameState gameState in this.gameDict)
            {
                if (gameState.JoiningPlayerCount > 0)
                {
                    oldJoiningCount += gameState.JoiningPlayerCount;
                    gameState.CheckJoinTimeOuts(minDateTime);

                    // check if there are still players left for the game
                    if (gameState.PlayerCount - gameState.YetExpectedUsersCount <= 0)
                    {
                        toRemove.Add(gameState);
                    }

                    joiningPlayerCount += gameState.JoiningPlayerCount;
                }
            }

            // remove all games where no players left
            foreach (GameState gameState in toRemove)
            {
                this.RemoveGameStateByName(gameState.Id);
                gameState.GameServer.IncrementGameCreationTimeouts();
            }

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Checked join timeouts: before={0}, after={1}", oldJoiningCount, joiningPlayerCount);
            }

            return joiningPlayerCount;
        }

        public bool ContainsGameId(string gameId)
        {
            return this.gameDict.ContainsKey(gameId);
        }

        public virtual void OnPlayerCountChanged(GameState gameState, int oldPlayerCount)
        {
            this.PlayerCount = this.PlayerCount - oldPlayerCount + gameState.PlayerCount;
            if (this.PlayerCount < 0)
            {
                log.WarnFormat("Got negative player count for lobby:'{0}/{1}', appId:{2}/{3}, PlayerCount:{4}, state debug:{5}", 
                    this.Lobby.LobbyName, this.Lobby.LobbyType, this.Lobby.Application.ApplicationId, 
                    this.Lobby.Application.Version, this.PlayerCount, gameState.GetDebugData());
                this.PlayerCount = 0;
            }
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("PlayerCount updated: in list={0}, in game oldPlayerCount={1}, playerCount={2}, DebugData:{3}", 
                    this.PlayerCount, oldPlayerCount, gameState.PlayerCount, gameState.GetDebugData());
            }
        }

        public virtual void OnGameJoinableChanged(GameState gameState)
        {
        }

        public void SetExpectReplicationFlag(bool flag, GameServerContext gameServerContext)
        {
            foreach (var gameState in this.gameDict.Values)
            {
                if (gameState.GameServer == gameServerContext)
                {
                    gameState.ExpectsReplication = flag;
                }
            }
        }

        public void RemoveNotReplicatedGames(GameServerContext gameServerContext)
        {
            var games4remove = new List<GameState>(100);
            foreach (var gameState in this.gameDict.Values)
            {
                if (gameState.GameServer == gameServerContext && gameState.ExpectsReplication)
                {
                    games4remove.Add(gameState);
                }
            }

            foreach (var game in games4remove)
            {
                this.RemoveGameStateByState(game);
            }
        }

        public virtual void UpdateLobbyLimits(bool gameListUseLegacyLobbies, int? gameListLimit, int? gameListLimitUpdates, int? gameListLimitSqlFilterResults)
        {

        }

        public virtual void PublishGameChanges()
        {
            if (this.ChangedGamesCount > 0)
            {
                var gameList = this.GetChangedGames();

                var e = new GameListUpdateEvent { Data = gameList };
                var eventData = new EventData((byte)EventCode.GameListUpdate, e);
                ApplicationBase.Instance.BroadCastEvent(eventData, this.peers, new SendParameters());
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("Game changes published. Peers Count:{0}, GamesCount:{1}", this.peers.Count, gameList.Count);
                }
                this.Lobby.OnSendChangedGameList(gameList);
            }
        }

        public virtual IGameListSubscription AddSubscription(MasterClientPeer peer, Hashtable gamePropertyFilter, int maxGameCount)
        {
            var subscription = new Subscription(this, maxGameCount, peer);
            this.peers.Add(peer);
            return subscription;
        }

        public void RemoveGameServer(GameServerContext gameServer)
        {
            // find games belonging to the game server instance
            var instanceGames = this.gameDict.Where(gameState => gameState.GameServer == gameServer).ToList();

            // remove game server instance games
            foreach (var gameState in instanceGames)
            {
                this.RemoveGameStateByState(gameState);
            }
        }

        public bool RemoveGameStateByName(string gameId)
        {
            if (!this.gameDict.TryGet(gameId, out var gameState))
            {
                //we did not found game in this game list, but still try to remove it from applications dict
                if (this.Lobby.Application.RemoveGameByName(gameId))
                {
                    log.ErrorFormat("Game successfully removed from application and not found in lobby. Game:{0}", gameId);
                }
                return false;
            }

            return this.RemoveGameStateByState(gameState);
        }

        public bool TryGetGame(string gameId, out GameState gameState)
        {
            return this.gameDict.TryGet(gameId, out gameState);
        }

        public abstract ErrorCode TryGetRandomGame(JoinRandomGameRequest joinRequest, ILobbyPeer peer, out GameState gameState, out string message);

        public virtual bool UpdateGameState(UpdateGameEvent updateOperation, GameServerContext incomingGameServer, out GameState gameState)
        {
            if (!this.GetOrAddUpdatedGameState(updateOperation, out gameState))
            {
                return false;
            }

            bool oldVisible = gameState.IsVisbleInLobby;
            bool changed = gameState.Update(updateOperation);

            if (!changed)
            {
                return false;
            }

            if (log.IsDebugEnabled)
            {
                LogGameState("UpdateGameState: ", gameState);
            }

            this.HandleVisibility(gameState, oldVisible);

            return true;
        }

        #endregion

        #region Protected and Privates

        protected virtual Hashtable GetAllGames(int maxCount)
        {
            int preAllocSize = maxCount;
            if (maxCount <= 0)
            {
                maxCount = this.gameDict.Count;
                preAllocSize = Math.Min(100, maxCount);
            }

            var hashTable = new Hashtable(preAllocSize);

            int i = 0;
            foreach (GameState game in this.gameDict)
            {
                if (game.IsVisbleInLobby)
                {
                    Hashtable gameProperties = game.ToHashTable();
                    hashTable.Add(game.Id, gameProperties);
                    i++;
                }

                if (i == maxCount)
                {
                    break;
                }
            }

            return hashTable;
        }

        protected virtual Hashtable GetChangedGames()
        {
            if (this.changedGames.Count == 0 && this.removedGames.Count == 0)
            {
                return null;
            }

            var hashTable = new Hashtable(this.changedGames.Count + this.removedGames.Count);

            foreach (GameState gameInfo in this.changedGames.Values)
            {
                if (gameInfo.IsVisbleInLobby)
                {
                    Hashtable gameProperties = gameInfo.ToHashTable();
                    hashTable.Add(gameInfo.Id, gameProperties);
                }
            }

            foreach (string gameId in this.removedGames)
            {
                hashTable.Add(gameId, new Hashtable { { (byte)GameParameter.Removed, true } });
            }

            this.changedGames.Clear();
            this.removedGames.Clear();

            return hashTable;
        }

        protected virtual void HandleVisibility(GameState gameState, bool oldVisible)
        {
            if (gameState.IsVisbleInLobby)
            {
                this.changedGames[gameState.Id] = gameState;

                if (oldVisible == false)
                {
                    this.removedGames.Remove(gameState.Id);
                }
            }
            else if (oldVisible)
            {
                this.changedGames.Remove(gameState.Id);
                this.removedGames.Add(gameState.Id);
            }
        }

        private GameState GetGameState(string gameId)
        {
            this.gameDict.TryGetValue(gameId, out var result);
            return result;
        }

        private bool GetOrAddUpdatedGameState(UpdateGameEvent updateOperation, out GameState gameState)
        {
            // try to get the game state 
            gameState = this.GetGameState(updateOperation.GameId);
            if (gameState == null)
            {
                if (updateOperation.Reinitialize)
                {
                    // if game is empty, than we sent this message just to support replication
                    if (updateOperation.IsEmptyRoomReplication)
                    {
                        if (log.IsDebugEnabled)
                        {
                            log.DebugFormat("Got replication for empty game. game:{0}, appId:{1}/{2}, ActorsCount:{3}, InactiveActors:{4}",
                                updateOperation.GameId, updateOperation.ApplicationId, updateOperation.ApplicationVersion,
                                updateOperation.ActorCount, updateOperation.InactiveCount);
                        }
                        return false;
                    }

                    if (log.IsDebugEnabled)
                    {
                        log.DebugFormat("Reinitialize: Add Game State {0}", updateOperation.GameId);
                    }

                    if (!this.Lobby.Application.TryGetGame(updateOperation.GameId, out gameState))
                    {
                        if (log.IsDebugEnabled)
                        {
                            log.DebugFormat("Could not find game to reinitialize: {0}", updateOperation.GameId);
                        }

                        return false;
                    }

                    this.AddGameState(gameState);
                }
                else
                {
                    if (log.IsDebugEnabled)
                    {
                        log.DebugFormat("Game not found: {0}", updateOperation.GameId);
                    }

                    return false;
                }
            }
            return true;
        }

        private void AdvanceNextJoinRandomStartNode()
        {
            if (this.nextJoinRandomStartNode == null)
            {
                return;
            }

            if (log.IsDebugEnabled)
            {
                log.DebugFormat(
                    "Changed last join random match: oldGameId={0}, newGameId={1}",
                    this.nextJoinRandomStartNode.Value.Id,
                    this.nextJoinRandomStartNode.Next == null ? "{null}" : this.nextJoinRandomStartNode.Value.Id);
            }

            this.nextJoinRandomStartNode = this.nextJoinRandomStartNode.Next;
        }

        private bool RemoveGameStateByState(GameState gameState)
        {
            var gameId = gameState.Id;
            var removeResult = this.Lobby.Application.RemoveGameByName(gameId);

            if (!this.RemoveGameState(gameState))
            {
                log.ErrorFormat("Game found in game list and not removed from it. RemovedFromApp:{0}. Game:{1}", removeResult, gameId);
                return false;
            }
            return true;
        }

        // override in GameChannelList, SqlGameList
        protected virtual bool RemoveGameState(GameState gameState)
        {
            if (log.IsDebugEnabled)
            {
                LogGameState("RemoveGameState:", gameState);
            }

            if (this.nextJoinRandomStartNode != null && this.nextJoinRandomStartNode.Value == gameState)
            {
                this.AdvanceNextJoinRandomStartNode();
            }

            this.PlayerCount -= gameState.PlayerCount;

            gameState.OnRemoved();

            var gameId = gameState.Id;
            this.gameDict.Remove(gameId);
            this.changedGames.Remove(gameId);
            if (gameState.IsVisbleInLobby)
            {
                this.removedGames.Add(gameId);
            }

            if (this.PlayerCount < 0)
            {
                log.WarnFormat("Got negative player count for lobby:'{0}/{1}', appId:{2}/{3}, PlayerCount:{4}, GameStage:{5}",
                    this.Lobby.LobbyName, this.Lobby.LobbyType, this.Lobby.Application.ApplicationId, this.Lobby.Application.Version,
                    this.PlayerCount, gameState.GetDebugData());
                this.PlayerCount = 0;
            }

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("PlayerCount changed after game remove. New Value={0}", this.PlayerCount);
            }
            return true;
        }

        protected static void LogGameState(string prefix, GameState gameState)
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat(
                    "{0}id={1}, peers={2}, max={3}, open={4}, visible={5}, peersJoining={6}, inactive={7}, ispersistent={8}", 
                    prefix, 
                    gameState.Id, 
                    gameState.GameServerPlayerCount, 
                    gameState.MaxPlayer, 
                    gameState.IsOpen, 
                    gameState.IsVisible, 
                    gameState.JoiningPlayerCount,
                    gameState.InactivePlayerCount,
                    gameState.IsPersistent
                );
            }
        }

        private class Subscription : GameSubscriptionBase
        {
            private readonly GameListBase gameList;

            public Subscription(GameListBase gameList, int maxGameCount, MasterClientPeer peer): base(peer, maxGameCount)
            {
                this.gameList = gameList;
            }

            protected override void Dispose(bool b)
            {
                if (this.disposed)
                {
                    return;
                }
                this.gameList.Lobby.EnqueueTask(()=> this.gameList.peers.Remove(this.peer));
            }

            public override Hashtable GetGameList()
            {
                var gl = this.gameList;
                if (gl == null)
                {
                    // subscription has been disposed (client has disconnect) during the request handling
                    return new Hashtable();
                }

                return gl.GetAllGames(this.maxGamesCount);
            }
        }

        #endregion

        protected static bool IsGameJoinable(JoinRandomGameRequest joinRequest, ILobbyPeer peer, GameState gameState)
        {
            if (!gameState.IsJoinable)
            {
                return false;
            }

            if (!gameState.SupportsProtocol(peer.NetworkProtocol))
            {
                return false;
            }

            return !gameState.CheckUserIdOnJoin
                   || (!gameState.ContainsUser(peer.UserId)
                       && !gameState.IsUserInExcludeList(peer.UserId)
                       && gameState.CheckSlots(peer.UserId, joinRequest.AddUsers));
        }
    }
}