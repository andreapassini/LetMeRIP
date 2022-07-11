// --------------------------------------------------------------------------------------------------------------------
// <copyright file="AppLobby.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the AppLobby type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System.Text;
using Newtonsoft.Json;
using Photon.Common;
using Photon.LoadBalancing.Common;

using System;
using System.Collections;
using System.Collections.Generic;

using ExitGames.Concurrency.Fibers;
using ExitGames.Logging;

using Photon.LoadBalancing.Events;
using Photon.LoadBalancing.MasterServer.ChannelLobby;
using Photon.LoadBalancing.MasterServer.GameServer;
using Photon.LoadBalancing.Operations;
using Photon.LoadBalancing.ServerToServer.Events;
using Photon.SocketServer;
using Photon.Hive.Operations;

using OperationCode = Photon.LoadBalancing.Operations.OperationCode;
using EventCode = Photon.LoadBalancing.Events.EventCode;
using Photon.Hive.Common.Lobby;
using Photon.Hive.Plugin;
using SendParameters = Photon.SocketServer.SendParameters;

namespace Photon.LoadBalancing.MasterServer.Lobby
{

    public class AppLobby
    {
        #region Constants and Fields

        public static readonly ILogger log = LogManager.GetCurrentClassLogger();

        public readonly GameApplication Application;

        public readonly string LobbyName;

        public readonly AppLobbyType LobbyType;

        public readonly TimeSpan JoinTimeOut;

        public readonly int MaxPlayersDefault; 

        internal readonly IGameList GameList;

        private readonly HashSet<PeerBase> peers = new HashSet<PeerBase>();

        private readonly int gameChangesPublishInterval = 1000;

        private readonly int allLobbiesMaxGamesInJoinResponse;

        private IDisposable schedule;

        private IDisposable checkJoinTimeoutSchedule;

        #endregion

        #region Constructors and Destructors

        public AppLobby(GameApplication application, string lobbyName, AppLobbyType lobbyType, bool useLegacyLobbies = false, int? limitGameList = null, int? limitGameListUpdate = null, int? limitSqlFilterResults = null, string matchmakingStoredProcedure = null)
            : this(application, lobbyName, lobbyType, 0, TimeSpan.FromSeconds(15), useLegacyLobbies, limitGameList, limitGameListUpdate, limitSqlFilterResults, matchmakingStoredProcedure)
        {
        }

        private AppLobby(GameApplication application, string lobbyName, AppLobbyType lobbyType, int maxPlayersDefault, TimeSpan joinTimeOut, bool useLegacyLobbies, int? limitGameList, int? limitGameListUpdate, int? limitSqlFilterResults, string matchmakingStoredProcedure = null)
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Creating lobby: name={0}, type={1}", lobbyName, lobbyType);
                string limitGameListStr = limitGameList != null ? limitGameList.ToString() : "null";
                string limitGameListUpdateStr = limitGameListUpdate != null ? limitGameListUpdate.ToString() : "null";
                string limitSqlFilterResultsStr = limitSqlFilterResults != null ? limitSqlFilterResults.ToString() : "null";

                log.DebugFormat("AppLobby - useLegacyLobbies {0}, limitGameList {1}, limitGameListUpdate {2}, limitSqlFilterResults {3}",
                    useLegacyLobbies, limitGameListStr, limitGameListUpdateStr, limitSqlFilterResultsStr);
                log.DebugFormat("MasterServerSettings - limitGameList {0}, limitGameListUpdate {1}, limitSqlFilterResults {2}",
                   MasterServerSettings.Default.Limits.Lobby.MaxGamesOnJoin, MasterServerSettings.Default.Limits.Lobby.MaxGamesInUpdates, MasterServerSettings.Default.Limits.Lobby.MaxGamesInGetGamesListResponse);
            }

            this.Application = application;
            this.LobbyName = lobbyName;
            this.LobbyType = lobbyType;
            this.MaxPlayersDefault = maxPlayersDefault;
            this.JoinTimeOut = joinTimeOut;
            this.allLobbiesMaxGamesInJoinResponse = limitGameList ?? MasterServerSettings.Default.Limits.Lobby.MaxGamesOnJoin;

            application.IncrementLobbiesCount();

            if (MasterServerSettings.Default.GameChangesPublishInterval > 0)
            {
                this.gameChangesPublishInterval = MasterServerSettings.Default.GameChangesPublishInterval;
            }

            switch (lobbyType)
            {
                default:
                    this.GameList = new LimitedGameList(this, useLegacyLobbies, limitGameList, limitGameListUpdate);
                    break;

                case AppLobbyType.ChannelLobby:
                    this.GameList = new GameChannelList(this);
                    break;

                case AppLobbyType.SqlLobby:
                    this.GameList = new SqlFilterGameList(this, useLegacyLobbies, limitSqlFilterResults, matchmakingStoredProcedure);
                    break;

                case AppLobbyType.AsyncRandomLobby:
                    this.GameList = new AsyncRandomGameList(this);
                    break;
            }

            this.InitUpdateLobbyLimits(application);
            this.InitUpdateMatchmakingStoredProcedure(application);

            this.ExecutionFiber = new PoolFiber();
            this.ExecutionFiber.Start();
        }

        #endregion

        #region Properties

        protected PoolFiber ExecutionFiber { get; set; }

        /// <summary>
        /// Gets the number of peers in the lobby.
        /// </summary>
        public int PeerCount
        {
            get
            {
                return this.peers.Count;
            }
        }

        /// <summary>
        /// Gets the total number of players in all games in this lobby.
        /// </summary>
        public int PlayerCount
        {
            get
            {
                return this.GameList.PlayerCount;
            }
        }

        public int GameCount
        {
            get
            {
                return this.GameList.Count;
            }
        }

        #endregion

        #region Public Methods

        public void EnqueueOperation(MasterClientPeer peer, OperationRequest operationRequest, SendParameters sendParameters)
        {
            this.ExecutionFiber.Enqueue(() => this.ExecuteOperation(peer, operationRequest, sendParameters));
        }

        public void EnqueueTask(Action task)
        {
            this.ExecutionFiber.Enqueue(task);
        }

        public void RemoveGame(string gameId)
        {
            this.ExecutionFiber.Enqueue(() => this.HandleRemoveGameState(gameId));
        }

        public void RemoveGameServer(GameServerContext gameServer)
        {
            this.ExecutionFiber.Enqueue(() => this.HandleRemoveGameServer(gameServer));
        }

        public void UpdateGameState(UpdateGameEvent operation, GameServerContext incomingGameServer)
        {
            this.ExecutionFiber.Enqueue(() => this.HandleUpdateGameState(operation, incomingGameServer));
        }

        public void JoinLobby(MasterClientPeer peer, JoinLobbyRequest joinLobbyrequest, SendParameters sendParameters)
        {
            this.ExecutionFiber.Enqueue(() => this.HandleJoinLobby(peer, joinLobbyrequest, sendParameters));
        }

        public void LeaveLobby(MasterClientPeer peer)
        {
            this.ExecutionFiber.Enqueue(() => this.HandleLeaveLobby(peer));
        }

        public void ResetGameServer(GameState gameState)
        {
            this.ExecutionFiber.Enqueue(() => this.HandleResetGameServer(gameState));
        }

        public override string ToString()
        {
            return string.Format("Name:{0},Type:{1}, GamesCount:{2}", this.LobbyName, this.LobbyType, this.GameCount);
        }

        public void OnBeginReplication(GameServerContext gameServerContext)
        {
            this.ExecutionFiber.Enqueue(() => this.HandleReplicationBegin(gameServerContext));
        }

        public void OnFinishReplication(GameServerContext gameServerContext)
        {
            this.ExecutionFiber.Enqueue(() => this.HandleReplicationFinish(gameServerContext));
        }

        public void OnStopReplication(GameServerContext gameServerContext)
        {
            this.ExecutionFiber.Enqueue(() => this.HandleReplicationStop(gameServerContext));
        }

        public void OnSendChangedGameList(Hashtable gameList)
        {
            this.Application.OnSendChangedGameList(gameList);
        }

        public virtual void InitUpdateLobbyLimits(GameApplication gameApplication)
        {
            
        }
        public virtual void InitUpdateMatchmakingStoredProcedure(GameApplication gameApplication)
        {

        }

        public void UpdateLobbyLimits(bool gameListUseLegacyLobbies, int? gameListLimit, int? gameListLimitUpdates, int? gameListLimitSqlFilterResults)
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("UpdateLobbyLimits ({4}/{5}), GameListUseLegacyLobbies {0}, GameListLimit {1}, GameListLimitUpdates {2}, GameListLimitSqlFilterResults {3}",
                    gameListUseLegacyLobbies, gameListLimit, gameListLimitUpdates, gameListLimitSqlFilterResults, this.LobbyName, this.LobbyType);
            }

            this.ExecutionFiber.Enqueue(() => this.GameList.UpdateLobbyLimits(gameListUseLegacyLobbies, gameListLimit, gameListLimitUpdates, gameListLimitSqlFilterResults));
        }

        public void UpdateMatchmakingStoredProcedure(string matchmakingStoredProcedure)
        {
            if (this.LobbyType == AppLobbyType.SqlLobby)
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("UpdateMatchmakingStoredProcedure ({0}/{1}):\n###\n{2}\n###\n", this.LobbyName, this.LobbyType, matchmakingStoredProcedure);
                }
                this.ExecutionFiber.Enqueue(() => ((SqlGameList) this.GameList).UpdateMatchmakingStoredProcedure(matchmakingStoredProcedure));
            }
        }

        #endregion

        #region Methods

        protected virtual void ExecuteOperation(MasterClientPeer peer, OperationRequest operationRequest, SendParameters sendParameters)
        {
            OperationResponse response;

            try
            {
                switch ((OperationCode)operationRequest.OperationCode)
                {
                    default:
                        response = new OperationResponse(operationRequest.OperationCode)
                        {
                            ReturnCode = (short)ErrorCode.OperationInvalid,
                            DebugMessage = HiveErrorMessages.UnknownOperationCode
                        };
                        break;

                    case OperationCode.CreateGame:
                        response = this.HandleCreateGame(peer, operationRequest);
                        break;

                    case OperationCode.JoinGame:
                        response = this.HandleJoinGame(peer, operationRequest);
                        break;

                    case OperationCode.JoinRandomGame:
                        response = this.HandleJoinRandomGame(peer, operationRequest);
                        break;

                    case OperationCode.DebugGame:
                        if (peer.AllowDebugGameOperation)
                        {
                            response = this.HandleDebugGame(peer, operationRequest);
                        }
                        else
                        {
                            response = new OperationResponse(operationRequest.OperationCode)
                            {
                                ReturnCode = (short)ErrorCode.OperationDenied,
                                DebugMessage = LBErrorMessages.NotAuthorized
                            };
                        }
                        break;

                    case OperationCode.GetGameList:
                        response = this.HandleGetGameList(peer, operationRequest);
                        break;
                }

            }
            catch (Exception ex)
            {
                response = new OperationResponse(operationRequest.OperationCode)
                {
                    ReturnCode = (short)ErrorCode.InternalServerError,
                    DebugMessage = ex.Message
                };
                log.Error(string.Format("Exception in AppLobby:{0}, Exception Msg:{1}", this, ex.Message), ex);
            }
            try
            {
                if (response != null)
                {
                    peer.SendOperationResponse(response, sendParameters);

                    switch((OperationCode)response.OperationCode)
                    {
                        case OperationCode.CreateGame:
                        case OperationCode.JoinGame:
                        case OperationCode.JoinRandomGame:
                        {
                            peer.IncConcurrentJoinRequest(-1);
                            if (response.ReturnCode == (short)ErrorCode.Ok)
                            {
                                peer.IncTotalSuccessfulJoinRequest();
                            }
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Exception in AppLobby:{0}, Exception Msg:{1}", this, ex.Message), ex);
            }
        }

        protected object GetCreateGameResponse(MasterClientPeer peer, GameState gameState)
        {
            return new CreateGameResponse
            {
                GameId = gameState.Id, 
                Address = gameState.GetServerAddress(peer),
                AuthenticationToken = peer.GetEncryptedTokenForGSAndGame(gameState.GameServer.AddressInfo.Hostname, gameState.Id),
            };
        }

        protected object GetJoinGameResponse(MasterClientPeer peer, GameState gameState)
        {
            return new JoinGameResponse
            {
                Address = gameState.GetServerAddress(peer),
                AuthenticationToken = peer.GetEncryptedTokenForGSAndGame(gameState.GameServer.AddressInfo.Hostname, gameState.Id),
            };
        }

        protected object GetJoinRandomGameResponse(MasterClientPeer peer, GameState gameState)
        {
            return new JoinRandomGameResponse 
            { 
                GameId = gameState.Id, 
                Address = gameState.GetServerAddress(peer),
                AuthenticationToken = peer.GetEncryptedTokenForGSAndGame(gameState.GameServer.AddressInfo.Hostname, gameState.Id),
            };
        }

        protected virtual DebugGameResponse GetDebugGameResponse(MasterClientPeer peer, GameState gameState)
        {
            return new DebugGameResponse
                {
                    Address = gameState.GetServerAddress(peer), 
                    Info = gameState.ToString()
                };
        }

        protected virtual GetGameListResponse GetGetGameListResponse(MasterClientPeer peer, Hashtable gameList)
        {
            return new GetGameListResponse
            {
                GameList = gameList
            };
        }

        protected virtual OperationResponse HandleCreateGame(MasterClientPeer peer, OperationRequest operationRequest)
        {
            // validate the operation request
            var operation = new CreateGameRequest(peer.Protocol, operationRequest, peer.UserId, peer.MaxPropertiesSizePerRequest);
            if (OperationHelper.ValidateOperation(operation, log, out var response) == false)
            {
                return response;
            }

            // if no gameId is specified by the client generate a unique id 
            if (string.IsNullOrEmpty(operation.GameId))
            {
                operation.GameId = Guid.NewGuid().ToString();
            }
           
            // try to create game
            if (!this.TryCreateGame(operation, peer.ExpectedProtocol, false, out var gameCreated, out var gameState, out response, peer.AuthCookie))
            {
                return response;
            }

            if (!gameState.CheckSlots(peer.UserId, operation.AddUsers, out var errMsg))
            {
                if (gameCreated)
                {
                    this.GameList.RemoveGameStateByName(operation.GameId);
                }

                return new OperationResponse
                {
                    OperationCode = operationRequest.OperationCode,
                    ReturnCode = (short)ErrorCode.SlotError,
                    DebugMessage = errMsg
                };
            }

            // add peer to game
            gameState.AddPeer(peer);
            gameState.AddSlots(operation);

            this.ScheduleCheckJoinTimeOuts();

            // publish operation response
            var createGameResponse = this.GetCreateGameResponse(peer, gameState);
            return new OperationResponse(operationRequest.OperationCode, createGameResponse);
        }

        protected virtual OperationResponse HandleJoinGame(MasterClientPeer peer, OperationRequest operationRequest)
        {
            // validate operation
            var operation = new JoinGameRequest(peer.Protocol, operationRequest, peer.UserId, peer.MaxPropertiesSizePerRequest);
            if (OperationHelper.ValidateOperation(operation, log, out var response) == false)
            {
                return response;
            }


            // try to find game by id
            GameState gameState;
            bool gameCreated = false;
            bool deferJoinResponse = false;
            if (operation.JoinMode == JoinModeConstants.JoinOnly && !this.Application.PluginTraits.AllowAsyncJoin)
            {
                // The client does not want to create the game if it does not exist.
                // In this case the game must have been created on the game server before it can be joined.
                if (this.GameList.TryGetGame(operation.GameId, out gameState) == false)
                {
                    return new OperationResponse
                    {
                        OperationCode = operationRequest.OperationCode,
                        ReturnCode = (short)ErrorCode.GameIdNotExists,
                        DebugMessage = HiveErrorMessages.GameIdDoesNotExist
                    };
                }

                if (gameState.HasBeenCreatedOnGameServer == false)
                {
                    deferJoinResponse = true;
                }
            }
            else
            {
                // The client will create the game if it does not exist already.
                if (!this.GameList.TryGetGame(operation.GameId, out gameState))
                {
                    if (!this.TryCreateGame(operation, peer.ExpectedProtocol, true, out gameCreated, out gameState, out response, peer.AuthCookie))
                    {
                        return response;
                    }
                }
            }

            if (gameState.IsUserInExcludeList(peer.UserId))
            {
                //ciao
                return new OperationResponse
                { 
                    OperationCode = operationRequest.OperationCode,
                    ReturnCode = (short)ErrorCode.JoinFailedFoundExcludedUserId, 
                    DebugMessage = HiveErrorMessages.JoinFailedFoundExcludedUserId
                };
            }
            // ValidateGame checks isOpen and maxplayers 
            // and does not apply to new games & rejoins
            //var actorIsRejoining = operation.ActorNr != 0;
            //if (gameCreated == false && !actorIsRejoining)
            if (gameCreated == false && !operation.IsRejoining)
            {
                // check if max players of the game is already reached1
                if (gameState.MaxPlayer > 0 && gameState.PlayerCount >= gameState.MaxPlayer && !gameState.IsUserExpected(peer.UserId))
                {
                    return new OperationResponse
                    {
                        OperationCode = operationRequest.OperationCode, 
                        ReturnCode = (short)ErrorCode.GameFull, DebugMessage = HiveErrorMessages.GameFull
                    };
                }

                // check if the game is open
                if (gameState.IsOpen == false)
                {
                    return new OperationResponse
                    {
                        OperationCode = operationRequest.OperationCode, 
                        ReturnCode = (short)ErrorCode.GameClosed, 
                        DebugMessage = HiveErrorMessages.GameClosed
                    };
                }

                if (operation.CheckUserOnJoin && gameState.ContainsUser(peer.UserId))
                {
                    return new OperationResponse 
                    { 
                        OperationCode = operationRequest.OperationCode,
                        ReturnCode = (short)ErrorCode.JoinFailedPeerAlreadyJoined, 
                        DebugMessage = string.Format(HiveErrorMessages.UserAlreadyJoined, peer.UserId, operation.JoinMode)
                    };
                }
            }

            if (!gameState.CheckSlots(peer.UserId, operation.AddUsers, out var errMsg))
            {
                if (gameCreated)
                {
                    this.GameList.RemoveGameStateByName(operation.GameId);
                }
                return new OperationResponse
                {
                    OperationCode = operationRequest.OperationCode, 
                    ReturnCode = (short)ErrorCode.SlotError, 
                    DebugMessage = errMsg
                };
            }

            if (deferJoinResponse)
            {
                gameState.AddPlayerToWaitList(peer, operation);
                return null;
            }

            // add peer to game
            return this.AddPlayerToGameAndGenerateResponse(peer, operation, gameState);
        }

        private OperationResponse AddPlayerToGameAndGenerateResponse(MasterClientPeer peer, JoinGameRequest operation, GameState gameState)
        {
            gameState.AddPeer(peer);
            gameState.AddSlots(operation);

            this.ScheduleCheckJoinTimeOuts();

            // publish operation response
            var joinResponse = this.GetJoinGameResponse(peer, gameState);
            return new OperationResponse(operation.OperationCode, joinResponse);
        }

        protected virtual void HandleJoinLobby(MasterClientPeer peer, JoinLobbyRequest operation, SendParameters sendParameters)
        {
            try
            {
                if (operation.GameListCount > 0)
                {
                    if (operation.GameListCount > this.allLobbiesMaxGamesInJoinResponse)
                    {
                        operation.GameListCount = this.allLobbiesMaxGamesInJoinResponse;
                    }
                }

                if (log.IsDebugEnabled)
                {
                    log.Debug($"lobby got Join Lobby request. GameListCount:{operation.GameListCount}, " +
                              $"allLobbiesMaxGamesInJoinResponse:{allLobbiesMaxGamesInJoinResponse}, userId:{peer.UserId}, p:{peer}");
                }
              
                var subscription = this.GameList.AddSubscription(peer, operation.GameProperties, operation.GameListCount);
                peer.GameChannelSubscription = subscription;
                peer.SendOperationResponse(new OperationResponse(operation.OperationRequest.OperationCode), sendParameters);

                if (subscription != null)
                {
                    // publish game list to peer after the response has been sent
                    var gameList = subscription.GetGameList();

                    if (gameList.Count != 0)
                    {
                        if (log.IsDebugEnabled)
                        {
                            var sb = new StringBuilder();
                            foreach (var game in gameList.Keys)
                            {
                                sb.AppendFormat("{0};", game);
                            }

                            log.DebugFormat("Game list is: {0}", sb.ToString());
                        }
                    }

                    if (log.IsDebugEnabled)
                    {
                        log.Debug($"lobby send data to client. gamesCount:{gameList.Count}, userId:{peer.UserId}, p:{peer}");
                    }

                    var e = new GameListEvent { Data = gameList };
                    var eventData = new EventData((byte)EventCode.GameList, e);
                    peer.SendEvent(eventData, new SendParameters());

                    this.OnSendGameList(gameList);
                }
                else
                {
                    if (log.IsDebugEnabled)
                    {
                        log.Debug($"lobby failed to subscribe peer to lobby. userId:{peer.UserId}, p:{peer}");
                    }
                }

                this.peers.Add(peer);
            }
            catch (Exception ex)
            {
                log.Error($"Exception in AppLobby:{this}, Exception Msg:{ex.Message}", ex);
            }
        }

        protected virtual void HandleLeaveLobby(MasterClientPeer peer)
        {
            this.RemovePeer(peer);
        }

        protected virtual OperationResponse HandleJoinRandomGame(MasterClientPeer peer, OperationRequest operationRequest)
        {
            // validate the operation request
            var operation = new JoinRandomGameRequest(peer.Protocol, operationRequest);
            if (OperationHelper.ValidateOperation(operation, log, out var response) == false)
            {
                return response;
            }

            // try to find a match
            var result = this.GameList.TryGetRandomGame(operation, peer, out var game, out var errorMessage);
            //create game
            if (result == ErrorCode.NoMatchFound && operation.CreateIfNotExists)
            {
                //random GameId if not set
                if (string.IsNullOrEmpty(operation.GameId))
                {
                    operation.GameId = Guid.NewGuid().ToString();
                    operationRequest.Parameters[(byte)ParameterKey.GameId] = operation.GameId;
                }

                //replace GameProperties (used for filter) with values for game creation
                if (operation.Properties != null)
                {
                    operationRequest.Parameters[(byte)ParameterKey.GameProperties] = operation.Properties;
                }

                //create - we try to use current HandleCreateGame implementation
                var createGameResponse = this.HandleCreateGame(peer, operationRequest);

                if (createGameResponse.ReturnCode != 0)
                {
                    return new OperationResponse { OperationCode = operationRequest.OperationCode, ReturnCode = createGameResponse.ReturnCode, DebugMessage = createGameResponse.DebugMessage };
                }

                var joinRandomGameResponse = new JoinRandomGameResponse
                {
                    GameId  = operation.GameId,
                    Address = (string)createGameResponse.Parameters[(byte) ParameterKey.Address],
                    AuthenticationToken   = createGameResponse[(byte)ParameterCode.Token],
                };

                //NodeId is not used anywhere atm
                if (createGameResponse.Parameters.ContainsKey((byte) ParameterKey.NodeId))
                {
                    joinRandomGameResponse.NodeId = (byte)createGameResponse.Parameters[(byte) ParameterKey.NodeId];
                }

                return new OperationResponse(operationRequest.OperationCode, joinRandomGameResponse)
                {
                    //TODO other ReturnCode if game was created
                    ReturnCode = (byte) ErrorCode.Ok
                };
            }

            if (result != ErrorCode.Ok)
            {
                if (string.IsNullOrEmpty(errorMessage))
                {
                    errorMessage = "No match found";
                }

                response = new OperationResponse { OperationCode = operationRequest.OperationCode, ReturnCode = (short)result, DebugMessage = errorMessage };
                return response;
            }

            // match found, add peer to game and notify the peer
            game.AddPeer(peer);

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Found match: connectionId={0}, userId={1}, gameId={2}", peer.ConnectionId, peer.UserId, game.Id);
            }

            this.ScheduleCheckJoinTimeOuts();

            var joinResponse = this.GetJoinRandomGameResponse(peer, game);
            return new OperationResponse(operationRequest.OperationCode, joinResponse);
        }

        protected virtual OperationResponse HandleDebugGame(MasterClientPeer peer, OperationRequest operationRequest)
        {
            var operation = new DebugGameRequest(peer.Protocol, operationRequest);
            if (OperationHelper.ValidateOperation(operation, log, out var response) == false)
            {
                return response; 
            }

            if (this.GameList.TryGetGame(operation.GameId, out var gameState) == false)
            {
                return new OperationResponse
                    {
                        OperationCode = operationRequest.OperationCode, 
                        ReturnCode = (short)ErrorCode.GameIdNotExists, 
                        DebugMessage = HiveErrorMessages.GameIdDoesNotExist
                    };
            }

            var debugGameResponse = this.GetDebugGameResponse(peer, gameState); 

            log.InfoFormat("DebugGame: {0}", debugGameResponse.Info);

            return new OperationResponse(operationRequest.OperationCode, debugGameResponse);
        }

        protected virtual OperationResponse HandleGetGameList(MasterClientPeer peer, OperationRequest operationRequest)
        {
            var operation = new GetGameListRequest(peer.Protocol, operationRequest);
            if (OperationHelper.ValidateOperation(operation, log, out var response) == false)
            {
                return response;
            }

            //check is already done in MasterClientPeer
            if (this.LobbyType != AppLobbyType.SqlLobby)
            {
                return new OperationResponse
                {
                    OperationCode = operationRequest.OperationCode,
                    ReturnCode = (short)ErrorCode.OperationInvalid,
                    DebugMessage = string.Format("Invalid lobby type: {0}", this.LobbyType)
                };
            }

            var gameList = (SqlGameList) this.GameList;

            var games = gameList.GetGameList(operation.QueryData, out var errorCode, out var message);

            if (errorCode != ErrorCode.Ok)
            {
                return new OperationResponse
                {
                    OperationCode = operationRequest.OperationCode,
                    ReturnCode = (short)errorCode,
                    DebugMessage = message
                };
            }

            var getGameListResponse = this.GetGetGameListResponse(peer, games);

            return new OperationResponse(operationRequest.OperationCode, getGameListResponse);
        }

        protected virtual void OnGameStateChanged(GameState gameState)
        {
        }

        protected virtual void OnRemovePeer(MasterClientPeer peer)
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("peer removed from lobby:l:'{0}',p:'{1}',u:'{2}'", this, peer, peer.UserId);
            }
        }

        private void OnSendGameList(Hashtable gameList)
        {
            this.Application.OnSendGameList(gameList);
        }

        private void ScheduleCheckJoinTimeOuts()
        {
            if (this.checkJoinTimeoutSchedule == null)
            {
                this.checkJoinTimeoutSchedule = this.ExecutionFiber.Schedule(this.CheckJoinTimeOuts, (int)this.JoinTimeOut.TotalMilliseconds / 2);
            }
        }

        private void CheckJoinTimeOuts()
        {
            try
            {
                this.checkJoinTimeoutSchedule.Dispose();
                var joiningPlayersLeft = this.GameList.CheckJoinTimeOuts(this.JoinTimeOut);
                if (joiningPlayersLeft > 0)
                {
                    this.ExecutionFiber.Schedule(this.CheckJoinTimeOuts, (int)this.JoinTimeOut.TotalMilliseconds / 2);
                }
                else
                {
                    this.checkJoinTimeoutSchedule = null;
                }
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Exception in AppLobby:{0}, Exception Msg:{1}", this, ex.Message), ex);
            }
        }

        private void HandleRemoveGameServer(GameServerContext gameServer)
        {
            try
            {
                this.GameList.RemoveGameServer(gameServer);
                this.SchedulePublishGameChanges();
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Exception in AppLobby:{0}, Exception Msg:{1}", this, ex.Message), ex);
            }
        }

        private void HandleRemoveGameState(string gameId)
        {
            try
            {
                if (this.GameList.TryGetGame(gameId, out var gameState) == false)
                {
                    if (log.IsDebugEnabled)
                    {
                        log.DebugFormat("HandleRemoveGameState: Game not found - gameId={0}", gameId);
                    }

                    return;
                }

                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("HandleRemoveGameState: gameId={0}, joiningPlayers={1}", gameId, gameState.JoiningPlayerCount);
                }

                this.GameList.RemoveGameStateByName(gameId);
                this.SchedulePublishGameChanges();
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Exception in AppLobby:{0}, Exception Msg:{1}", this, ex.Message), ex);
            }
        }


        public void NotifyWaitListOnGameCreated(GameState gameState)
        {
            if (gameState.WaitList.Count == 0)
            {
                return;
            }

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Lobby {0} notifying '{1}' waiting players that game '{2}' is created", this.LobbyName, gameState.WaitList.Count, gameState.Id);
            }
            foreach (var p in gameState.WaitList)
            {
                p.Peer.SendOperationResponse(this.AddPlayerToGameAndGenerateResponse(p.Peer, p.JoinRequest, gameState), new SendParameters());
            }
        }

        public void NotifyWaitListOnGameRemoved(GameState gameState)
        {
            if (gameState.WaitList.Count == 0)
            {
                return;
            }

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Lobby {0} notifying '{1}' waiting players that game '{2}' is removed", this.LobbyName, gameState.WaitList.Count, gameState.Id);
            }

            foreach (var p in gameState.WaitList)
            {
                p.Peer.SendOperationResponse(
                    new OperationResponse
                    {
                        OperationCode = (byte)OperationCode.JoinGame,
                        ReturnCode = (short)ErrorCode.GameIdNotExists,
                        DebugMessage = HiveErrorMessages.GameIdDoesNotExist
                    }, 
                    new SendParameters());
            }
        }

        private void RemovePeer(MasterClientPeer peer)
        {
            try
            {
                this.peers.Remove(peer);
                this.OnRemovePeer(peer);
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Exception in AppLobby:{0}, Exception Msg:{1}", this, ex.Message), ex);
            }
        }

        private void HandleUpdateGameState(UpdateGameEvent operation, GameServerContext incomingGameServer)
        {
            try
            {
                if (this.GameList.UpdateGameState(operation, incomingGameServer, out var gameState) == false)
                {
                    return;
                }

                this.SchedulePublishGameChanges();

                this.OnGameStateChanged(gameState);
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Exception in AppLobby:{0}, Exception Msg:{1}", this, ex.Message), ex);
            }
        }

        private void SchedulePublishGameChanges()
        {
            if (this.schedule == null)
            {
                this.schedule = this.ExecutionFiber.Schedule(this.PublishGameChanges, this.gameChangesPublishInterval);
            }
        }

        private void PublishGameChanges()
        {
            try
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("Publishing game changes. AppLobby:{0}, type:{1}", this.LobbyName, this.LobbyType);
                }

                this.schedule = null;
                this.GameList.PublishGameChanges();
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Exception in AppLobby:{0}, Exception Msg:{1}", this, ex.Message), ex);
            }
        }

        internal static readonly JsonSerializerSettings serializeSettings = new JsonSerializerSettings()
        {
            NullValueHandling = NullValueHandling.Ignore,
            TypeNameHandling = TypeNameHandling.None,
        };

        protected virtual bool TryCreateGame(JoinGameRequest operation, NetworkProtocolType expectedProtocol, bool createIfNotExists, 
            out bool gameCreated, out GameState gameState, out OperationResponse errorResponse, Dictionary<string, object> authCookie)
        {
            var gameId = operation.GameId;
            var properties = operation.GameProperties;

            gameState = null;
            gameCreated = false;

            Func<GameServerContext, bool> filter = ctx =>
            {
                if (ctx.SupportedProtocols == null)
                {
                    return true;
                }
                return ctx.SupportedProtocols.Contains((byte)expectedProtocol);
            };

            // try to get a game server instance from the load balancer
            if (!this.Application.LoadBalancer.TryGetServer(out var gameServerContext, filter))
            {
                errorResponse = new OperationResponse(operation.OperationRequest.OperationCode)
                    {
                        ReturnCode = (short)ErrorCode.ServerFull,
                        DebugMessage = LBErrorMessages.FailedToGetServerInstance,
                    };

                return false;
            }

            ErrorCode errorCode;
            string errorMsg;
            // try to create or get game state
            if (createIfNotExists)
            {
                gameCreated = this.Application.GetOrCreateGame(gameId, this, (byte)this.MaxPlayersDefault, gameServerContext, out gameState, out errorCode, out errorMsg);
                if (errorCode != ErrorCode.Ok)
                {
                    errorResponse = new OperationResponse(operation.OperationRequest.OperationCode)
                    {
                        ReturnCode = (short)errorCode,
                        DebugMessage = errorMsg,
                    };

                    return false;
                }
            }
            else
            {
                if (!this.Application.TryCreateGame(gameId, this, (byte)this.MaxPlayersDefault, gameServerContext, out gameState, out errorCode, out errorMsg))
                {
                    errorResponse = new OperationResponse(operation.OperationRequest.OperationCode)
                    {
                        ReturnCode = (short)errorCode,
                        DebugMessage = errorMsg,
                    };

                    return false;
                }

                gameCreated = true;
            }

            if (gameCreated)
            {
                gameState.CreateRequest = operation;
            }

            if (properties != null)
            {
                if (!gameState.TrySetProperties(properties, out var changed, out var debugMessage))
                {
                    if (gameCreated)
                    {
                        this.Application.RemoveGameByName(gameId);
                    }

                    errorResponse = new OperationResponse(operation.OperationRequest.OperationCode)
                        {
                            ReturnCode = (short)ErrorCode.OperationInvalid,
                            DebugMessage = debugMessage
                        };
                    return false;
                }
            }

            try
            {
                this.GameList.AddGameState(gameState, authCookie);
            }
            catch (Exception)
            {
                log.ErrorFormat("New game state:{0}", gameState.ToString());
                log.ErrorFormat("Request Params for new state:{0}", JsonConvert.SerializeObject(operation, serializeSettings));
                log.ErrorFormat("CreateIfNotExists: {0}, GameCreated: {1}, Game Properties:{2}", 
                    createIfNotExists, gameCreated, JsonConvert.SerializeObject(properties, serializeSettings));

                if (this.Application.TryGetGame(gameId, out var gameInApp))
                {
                    log.ErrorFormat("Game state in app:{0}", gameInApp.ToString());
                    log.ErrorFormat("Request Params for Game in App:{0}", JsonConvert.SerializeObject(gameInApp.CreateRequest, serializeSettings));
                }

                this.Application.RemoveGameByName(gameState.Id);
                gameCreated = false;

                if (this.GameList.TryGetGame(gameState.Id, out var gameStateInList))
                {
                    log.ErrorFormat("Game state in list:{0}", gameStateInList.ToString());
                    log.ErrorFormat("Request Params for Game in list:{0}",
                        JsonConvert.SerializeObject(gameStateInList.CreateRequest, serializeSettings));
                }
                else
                {
                    log.ErrorFormat("Game state {0} not found in list", gameState.Id);
                }
                throw;
            }

            this.SchedulePublishGameChanges();

            errorResponse = null;
            return true;
        }

        private void HandleResetGameServer(GameState gameState)
        {
            gameState.ResetGameServer();
        }

        private void HandleReplicationBegin(GameServerContext gameServerContext)
        {
            try
            {
                this.GameList.SetExpectReplicationFlag(true, gameServerContext);
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Exception in AppLobby:{0}, Exception Msg:{1}", this, ex.Message), ex);
            }
        }

        private void HandleReplicationFinish(GameServerContext gameServerContext)
        {
            try
            {
                this.GameList.RemoveNotReplicatedGames(gameServerContext);
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Exception in AppLobby:{0}, Exception Msg:{1}", this, ex.Message), ex);
            }
        }

        private void HandleReplicationStop(GameServerContext gameServerContext)
        {
            try
            {
                this.GameList.SetExpectReplicationFlag(false, gameServerContext);
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Exception in AppLobby:{0}, Exception Msg:{1}", this, ex.Message), ex);
            }
        }

        #endregion
    }
}