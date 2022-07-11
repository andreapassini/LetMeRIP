namespace Photon.LoadBalancing.MasterServer
{
    using System;
    using System.Collections.Generic;

    using ExitGames.Concurrency.Fibers;
    using ExitGames.Logging;

    using Photon.LoadBalancing.MasterServer.Lobby;
    using Photon.LoadBalancing.Operations;
    using Photon.SocketServer;
    using Photon.SocketServer.Diagnostics;

    public class PlayerCache : IDisposable
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private static readonly LogCountGuard MaxJoinGamesGurad = new LogCountGuard(new TimeSpan(0, 1, 0), 60);
        private readonly LogCountGuard maxGamesLogGuard = new LogCountGuard(new TimeSpan(0, 0, 1));

        private readonly PoolFiber fiber = null;

        private IDisposable maxJoinedGamesStatsTimer;

        private int maxGamesCount = 0;

        private string playerIdWithMaxGamesCount = string.Empty;

        private readonly Dictionary<string, PlayerState> playerDict = new Dictionary<string,PlayerState>();

        public PlayerCache(PoolFiber playerCacheFiber)
        {
            this.fiber = playerCacheFiber;
            this.fiber.Start();

            this.maxJoinedGamesStatsTimer = this.fiber.ScheduleOnInterval(this.LogMaxJoinedGames, 60000, 60000);
        }

        public void OnConnectedToMaster(ILobbyPeer peer)
        {
            this.fiber.Enqueue(() => this.HandleOnConnectedToMaster(peer));
        }

        public void OnDisconnectFromMaster(string playerId)
        {
            this.fiber.Enqueue(() => this.HandleOnDisconnectFromMaster(playerId));
        }

        public void OnDisconnectFromGameServer(string playerId, GameState gameState)
        {
            this.fiber.Enqueue(() => this.HandleOnDisconnectFromGameServer(playerId, gameState));
        }

        public void OnJoiningGame(ILobbyPeer peer, GameState gameState)
        {
            this.fiber.Enqueue(() => this.HandleOnJoiningGame(peer, gameState));
        }

        public void OnJoinedGame(string playerId, GameState gameState)
        {
            this.fiber.Enqueue(() => this.HandleOnJoinedGame(playerId, gameState));
        }

        public void FiendFriends(PeerBase peer, FindFriendsRequest request, SendParameters sendParameters)
        {
            this.fiber.Enqueue(() => this.HandleFiendFriends(peer, request, sendParameters));
        }

        public void Dispose()
        {
            var poolFiber = this.fiber;
            this.maxJoinedGamesStatsTimer?.Dispose();
            this.maxJoinedGamesStatsTimer = null;

            if (poolFiber != null)
            {
                poolFiber.Dispose();
            }
        }


        private void HandleOnConnectedToMaster(ILobbyPeer peer)
        {
            try
            {
                // only peers with userid set can be handled
                if (string.IsNullOrEmpty(peer.UserId))
                {
                    return;
                }

                var playerState = this.GetOrAddPlayerState(peer.UserId);

                playerState.IsConnectedToMaster = true;

                if (log.IsDebugEnabled)
                {
                    string gameId = playerState.ActiveGame == null ? string.Empty : playerState.ActiveGame.Id;
                    log.DebugFormat("Player state changed: pid={0}, master={1}, gid={2}", peer.UserId, playerState.IsConnectedToMaster, gameId);
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }

        private void HandleOnDisconnectFromMaster(string playerId)
        {
            try
            {
                // only peers with userid set can be handled
                if (string.IsNullOrEmpty(playerId))
                {
                    return;
                }

                PlayerState playerState;
                if (this.playerDict.TryGetValue(playerId, out playerState) == false)
                {
                    return;
                }

                playerState.IsConnectedToMaster = false;
                if (playerState.ActiveGame != null)
                {
                    return;
                }

                this.playerDict.Remove(playerId);
                if (log.IsDebugEnabled)
                {
                    string gameId = playerState.ActiveGame == null ? string.Empty : playerState.ActiveGame.Id;
                    log.DebugFormat("Player removed: pid={0}, master={1}, gid={2}", playerId, playerState.IsConnectedToMaster, gameId);
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }

        private void HandleOnJoinedGame(string playerId, GameState gameState)
        {
            try
            {
                // only peers with userid set can be handled
                if (string.IsNullOrEmpty(playerId))
                {
                    return;
                }

                var playerState = this.GetOrAddPlayerState(playerId);

                playerState.ActiveGame = gameState;

                if (log.IsDebugEnabled)
                {
                    string gameId = gameState == null ? string.Empty : gameState.Id;
                    log.DebugFormat("Player state changed: pid={0}, master={1}, gid={2}", playerId, playerState.IsConnectedToMaster, gameId);
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }

        private void HandleOnJoiningGame(ILobbyPeer peer, GameState gameState)
        {
            try
            {
                // only peers with userid set can be handled
                if (string.IsNullOrEmpty(peer.UserId))
                {
                    return;
                }

                var playerState = this.GetOrAddPlayerState(peer.UserId);

                playerState.ActiveGame = gameState;

                if (peer.CustomAuthUserIdUsed)
                {
                    var result = playerState.AddUserSessionInfo(gameState, peer);

                    if (result > MasterServerSettings.Default.Limits.Inbound.MaxJoinedGames)
                    {
                        log.Warn(this.maxGamesLogGuard, 
                            $"Player joined more games than allowed maximum. pid:{peer.UserId}, count:{result}, limit:{MasterServerSettings.Default.Limits.Inbound.MaxJoinedGames}");
                    }

                    if (this.maxGamesCount < result)
                    {
                        this.maxGamesCount = result;
                        this.playerIdWithMaxGamesCount = peer.UserId;
                    }
                }

                if (log.IsDebugEnabled)
                {
                    string gameId = gameState == null ? string.Empty : gameState.Id;
                    log.DebugFormat("Player state changed: pid={0}, master={1}, gid={2}", peer.UserId, playerState.IsConnectedToMaster, gameId);
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }

        private PlayerState GetOrAddPlayerState(string playerId)
        {
            if (this.playerDict.TryGetValue(playerId, out PlayerState playerState) == false)
            {
                playerState = new PlayerState(playerId);
                this.playerDict.Add(playerId, playerState);
            }

            return playerState;
        }

        private void HandleOnDisconnectFromGameServer(string playerId, GameState gameState)
        {
            try
            {
                // only peers with userid set can be handled
                if (string.IsNullOrEmpty(playerId))
                {
                    return;
                }

                PlayerState playerState;
                if (this.playerDict.TryGetValue(playerId, out playerState) == false)
                {
                    return;
                }

                playerState.ActiveGame = null;
                playerState.RemoveGame(gameState);
                if (playerState.IsConnectedToMaster)
                {
                    return;
                }

                this.playerDict.Remove(playerId);
                if (log.IsDebugEnabled)
                {
                    string gameId = playerState.ActiveGame == null ? string.Empty : playerState.ActiveGame.Id;
                    log.DebugFormat("Player removed: pid={0}, master={1}, gid={2}", playerId, playerState.IsConnectedToMaster, gameId);
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }

        private void HandleFiendFriends(PeerBase peer, FindFriendsRequest request, SendParameters sendParameters)
        {
            try
            {
                var onlineList = new bool[request.UserList.Length];
                var gameIds = new string[request.UserList.Length];

                for (int i = 0; i < request.UserList.Length; i++)
                {
                    gameIds[i] = string.Empty;
                    PlayerState playerState;
                    if (this.playerDict.TryGetValue(request.UserList[i], out playerState))
                    {
                        onlineList[i] = true;
                        if (playerState.ActiveGame != null)
                        {
                            if ((request.OperationOptions & FindFriendsOptions.CreatedOnGS) == FindFriendsOptions.CreatedOnGS)
                            {
                                if (!playerState.ActiveGame.HasBeenCreatedOnGameServer)
                                {
                                    continue;
                                }
                            }

                            if ((request.OperationOptions & FindFriendsOptions.Visible) == FindFriendsOptions.Visible)
                            {
                                if (!playerState.ActiveGame.IsVisible)
                                {
                                    continue;
                                }
                            }

                            if ((request.OperationOptions & FindFriendsOptions.Open) == FindFriendsOptions.Open)
                            {
                                if (!playerState.ActiveGame.IsOpen)
                                {
                                    continue;
                                }
                            }
                            gameIds[i] = playerState.ActiveGame.Id;
                        }
                    }
                }

                var response = new FindFriendsResponse { IsOnline = onlineList, UserStates = gameIds };
                var opResponse = new OperationResponse((byte)OperationCode.FindFriends, response);
                peer.SendOperationResponse(opResponse, sendParameters);
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }

        private void LogMaxJoinedGames()
        {
            if (this.maxGamesCount == 0 ||
                this.maxGamesCount == 1)
            {
                return;
            }

            log.Warn(MaxJoinGamesGurad, $"Maximum joined games for last minute is {this.maxGamesCount} for player:{this.playerIdWithMaxGamesCount}");
            this.maxGamesCount = 0;
            this.playerIdWithMaxGamesCount = string.Empty;
        }
    }

    public class PlayerState
    {
        public class SessionInfo
        {
            public GameState GameState { get; set; }

            public string SessionId { get; set; }
        }

        private readonly List<SessionInfo> sessions = new List<SessionInfo>();

        public readonly string PlayerId;
 
        public PlayerState(string playerId)
        {
            this.PlayerId = playerId;
        }

        public bool IsConnectedToMaster { get; set; }

        public GameState ActiveGame { get; set; }

        public void RemoveGame(GameState gameState)
        {
            //only one should be removed because game name is uniq string
            this.sessions.RemoveAll(s => s.GameState.Id == gameState.Id);
        }

        public int AddUserSessionInfo(GameState gameState, ILobbyPeer peer)
        {
            var session = this.sessions.Find(s => s.GameState.Id == gameState.Id && s.SessionId == peer.SessionId);
            if (session != null)
            {
                return -1;
            }

            this.sessions.Add(new SessionInfo {SessionId = peer.SessionId, GameState = gameState});
            return this.sessions.Count;
        }
    }
}
