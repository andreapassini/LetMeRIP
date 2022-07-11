// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Game.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the Game type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections;
using System.Linq;

using ExitGames.Logging;

using Photon.Hive;
using Photon.Hive.Caching;
using Photon.Hive.Messages;
using Photon.Hive.Operations;
using Photon.Hive.Plugin;
using Photon.LoadBalancing.Common;
using Photon.LoadBalancing.ServerToServer;
using Photon.LoadBalancing.ServerToServer.Events;
using Photon.SocketServer;
using Photon.SocketServer.Diagnostics;

using SendParameters = Photon.SocketServer.SendParameters;

namespace Photon.LoadBalancing.GameServer
{
    public class Game : HiveHostGame
    {
        #region Fields and Constatnts

        private static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        private static readonly LogCountGuard fullGameReinitLogGuard = new LogCountGuard(new TimeSpan(0, 1, 0));
        private static readonly LogCountGuard inactivePeerLogGuard = new LogCountGuard(new TimeSpan(0, 0, 10));

        private readonly TimeIntervalCounter onGameIsFullErrorsCounter = new TimeIntervalCounter(new TimeSpan(0, 0, 10));
        /// <summary>
        /// does not allow to reinitialize game state on master more then once in allowed period
        /// </summary>
        private readonly TimeIntervalCounter gameUpdaterGuard = new TimeIntervalCounter(new TimeSpan(0, 0, 10), 1);
        /// <summary> 
        ///     Specifies to maximum value for a peers GetLastTouch in milliseconds before a peer will removed from the room.
        ///     If set to zero no check will bew performed.
        /// </summary>
        private readonly int LastTouchLimitMilliseconds;

        private readonly GameApplication Application;

        private bool logRoomRemoval;

        private readonly int ErrorsCountToInitiateUpdate = GameServerSettings.Default.JoinErrorCountToReinitialize;

        public virtual RoomCacheBase GameCache => this.Application.GameCache;

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="Game"/> class.
        /// </summary>
        /// <param name="gameCreateOptions"></param>
        public Game(LBGameCreateOptions gameCreateOptions)
            : base(gameCreateOptions.GameCreateOptions)
        {
            this.Application = gameCreateOptions.Application;
            this.LimitMaxPropertiesSizePerGame = GameServerSettings.Default.Limits.Inbound.Properties.MaxPropertiesSizePerGame;

            this.Application.AppStatsPublisher?.IncrementGameCount();

            this.HttpForwardedOperationsLimit = GameServerSettings.Default.Limits.HttpForwardLimit;

            this.LastTouchLimitMilliseconds = GameServerSettings.Default.LastTouchSecondsDisconnect * 1000;

            this.EventCache.SlicesCountLimit = GameServerSettings.Default.Limits.Inbound.EventCache.SlicesCount;
            this.EventCache.CachedEventsCountLimit = GameServerSettings.Default.Limits.Inbound.EventCache.EventsCount;
            this.ActorEventCache.CachedEventsCountLimit = GameServerSettings.Default.Limits.Inbound.EventCache.ActorEventsCount;
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources
        /// </summary>
        /// <param name="disposing">
        /// <c>true</c> to release both managed and unmanaged resources; 
        /// <c>false</c> to release only unmanaged resources.
        /// </param>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            this.RemoveRoomPath = RemoveState.GameDisposeCalled;

            if (disposing)
            {
                this.RemoveGameStateFromMaster();

                this.Application.AppStatsPublisher?.DecrementGameCount();
            }
            if (this.logRoomRemoval)
            {
                Log.WarnFormat("RoomDisposalCheck: Room disposed. name:{0}", this.Name);
            }
        }

        protected override void OnClose()
        {
            base.OnClose();
            if (this.logRoomRemoval)
            {
                Log.WarnFormat("RoomDisposalCheck: Room closed. name:{0}", this.Name);
            }
        }

        protected override void JoinFailureHandler(byte leaveReason, HivePeer peer, JoinGameRequest request)
        {
            base.JoinFailureHandler(leaveReason, peer, request);
            this.NotifyMasterUserFailedToAdd((GameClientPeer)peer);
        }

        protected override bool ProcessJoin(Actor actor, JoinGameRequest joinRequest, SendParameters sendParameters, ProcessJoinParams parameters, HivePeer peer)
        {
            if (!base.ProcessJoin(actor, joinRequest, sendParameters, parameters, peer))
            {
                return false;
            }

            var gamePeer = (GameClientPeer)peer;
            // update game state at master server
            var userId = gamePeer.UserId ?? string.Empty;

            this.NotifyMasterUserAdded(userId, joinRequest.AddUsers != null ? this.ActorsManager.ExpectedUsers.ToArray() : null);

            return true;
        }

        protected override void HandleCreateGameOperation(HivePeer peer, SendParameters sendParameters, JoinGameRequest createGameRequest)
        {
            createGameRequest.RoomFlags &= GameServerSettings.Default.RoomOptionsAndFlags;
            createGameRequest.RoomFlags |= GameServerSettings.Default.RoomOptionsOrFlags;

            base.HandleCreateGameOperation(peer, sendParameters, createGameRequest);
        }

        protected override void HandleJoinGameOperation(HivePeer peer, SendParameters sendParameters, JoinGameRequest joinGameRequest)
        {
            if (joinGameRequest.CreateIfNotExists)
            {
                joinGameRequest.RoomFlags &= GameServerSettings.Default.RoomOptionsAndFlags;
                joinGameRequest.RoomFlags |= GameServerSettings.Default.RoomOptionsOrFlags;
            }

            base.HandleJoinGameOperation(peer, sendParameters, joinGameRequest);
        }

        protected override bool ProcessCreateGame(HivePeer peer, JoinGameRequest joinRequest, SendParameters sendParameters)
        {
            if (base.ProcessCreateGame(peer, joinRequest, sendParameters))
            {
                // update game state at master server
                this.UpdateGameStateOnMasterOnCreate(joinRequest, peer);
            }
            return true;
        }

        protected override void OnGamePropertiesChanged(SetPropertiesRequest request)
        {
            if (Log.IsDebugEnabled)
            {
                Log.DebugFormat("MaxPlayer={0}, IsOpen={1}, IsVisible={2}, #LobbyProperties={3}, #GameProperties={4}",
                    request.newMaxPlayer, request.newIsOpen, request.newIsVisible,
                    request.newLobbyProperties?.Length ?? 0,
                    request.newGameProperties?.Count ?? 0);
            }

            var expectedPlayers = request.ValuesUpdated.HasFlag(ValuesUpdateFlags.ExpectedUsers) ? request.ExpectedUsers ?? EmptyStringArray : null;
            this.UpdateGameStateOnMaster(request.newMaxPlayer, request.newIsOpen,
                request.newIsVisible, request.newLobbyProperties, request.newGameProperties, null, null, null, null, expectedPlayers);
        }

        protected override void DeactivateActor(Actor actor)
        {
            base.DeactivateActor(actor);
            // The room was not disposed because there are either players left or the
            // room has and EmptyRoomLiveTime set -> update game state on master.
            this.NotifyMasterUserDeactivated(actor.UserId);
        }

        protected override void CleanupActor(Actor actor)
        {
            base.CleanupActor(actor);
            this.NotifyMasterUserLeft(actor.UserId);
        }

        protected override void ProcessMessage(IMessage message)
        {
            try
            {
                switch (message.Action)
                {
                    case (byte)GameMessageCodes.ReinitializeGameStateOnMaster:
                        if (Log.IsDebugEnabled)
                        {
                            Log.DebugFormat("Processing of ReinitializeGameStateOnMaster. Game={0}", this.Name);
                        }

                        if (this.ActorsManager.Count == 0 && this.EmptyRoomLiveTime == 0)
                        {
                            if (Log.IsDebugEnabled)
                            {
                                Log.DebugFormat("replication of empty game. Game:{0}", this);
                            }

                            this.ReinitializeEmptyGameOnMaster();
                        }
                        else
                        {
                            this.ReinitializeGameOnMaster(ReplicationId.Replication);
                        }

                        break;

                    case (byte)GameMessageCodes.CheckGame:
                        this.CheckGame();
                        break;

                    default:
                        base.ProcessMessage(message);
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }

        private void ReinitializeEmptyGameOnMaster()
        {
            this.UpdateGameStateOnMaster(null, null, null, null, null, null, null, null, null, null, null, ReplicationId.ReplicationOfEmptyGame);
        }

        private void ReinitializeGameOnMaster(byte replication = ReplicationId.Renitialization)
        {
            var gameProperties = this.GetLobbyGameProperties(this.Properties.GetProperties());
            object[] lobbyPropertyFilter = null;
            if (this.LobbyProperties != null)
            {
                lobbyPropertyFilter = new object[this.LobbyProperties.Count];
                this.LobbyProperties.CopyTo(lobbyPropertyFilter);
            }

            var excludedActors = this.ActorsManager.ExcludedActors.Count > 0 ? this.ActorsManager.ExcludedActors.ToArray() : null;
            var expectedUsers = this.ActorsManager.ExpectedUsers.Count > 0 ? this.ActorsManager.ExpectedUsers.ToArray() : null;
            this.UpdateGameStateOnMaster(this.MaxPlayers, this.IsOpen, this.IsVisible, lobbyPropertyFilter, gameProperties,
                this.GetActiveUserIds(), null, this.GetInactiveUserIds(), excludedActors, expectedUsers,
                this.RoomState.CheckUserOnJoin, replication);
        }

        private string[] GetActiveUserIds()
        {
            if (this.RoomState.CheckUserOnJoin)
            {
                return this.ActorsManager.ActiveActors.Select(a => a.UserId).ToArray();
            }
            return null;
        }

        private string[] GetInactiveUserIds()
        {
            if (this.RoomState.CheckUserOnJoin)
            {
                return this.ActorsManager.InactiveActors.Select(a => a.UserId).ToArray();
            }
            return null;
        }

        protected virtual void NotifyMasterUserDeactivated(string userId)
        {
            var updateGameEvent = this.GetUpdateGameEvent();
            updateGameEvent.InactiveUsers = new[] { userId ?? string.Empty };
            this.UpdateGameStateOnMaster(updateGameEvent);
        }

        protected virtual void NotifyMasterUserLeft(string userId)
        {
            var updateGameEvent = this.GetUpdateGameEvent();
            updateGameEvent.RemovedUsers = new[] { userId ?? string.Empty };

            this.UpdateGameStateOnMaster(updateGameEvent);
        }

        protected virtual void NotifyMasterUserFailedToAdd(GameClientPeer peer)
        {
            var updateGameEvent = this.GetUpdateGameEvent();
            updateGameEvent.FailedToAdd = new[] { peer.UserId ?? string.Empty };

            this.UpdateGameStateOnMaster(updateGameEvent);
        }

        protected virtual void NotifyMasterUserAdded(string userId, string[] slots)
        {
            var usrList = new[] { userId ?? string.Empty };
            this.NotifyMasterUserAdded(usrList, slots);
        }

        protected virtual void NotifyMasterUserAdded(string[] userIds, string[] slots)
        {
            var updateGameEvent = this.GetUpdateGameEvent();
            updateGameEvent.NewUsers = userIds;
            updateGameEvent.ExpectedUsers = slots;
            this.UpdateGameStateOnMaster(updateGameEvent);
        }

        protected override void OnActorBanned(Actor actor)
        {
            if (Log.IsDebugEnabled)
            {
                Log.DebugFormat("User {0} will be banned", actor.UserId);
            }

            var updateGameEvent = this.GetUpdateGameEvent();
            updateGameEvent.ExcludedUsers = new[]
            {
                new ExcludedActorInfo
                {
                    UserId = actor.UserId ?? string.Empty,
                    Reason = RemoveActorReason.Banned,
                }
            };
            this.UpdateGameStateOnMaster(updateGameEvent);
        }

        protected override void OnActorGlobalBanned(Actor actor)
        {
            if (Log.IsDebugEnabled)
            {
                Log.DebugFormat("User {0} will be global banned", actor.UserId);
            }

            //we need a userId
            if (string.IsNullOrEmpty(actor.UserId))
            {
                if (Log.IsInfoEnabled)
                {
                    Log.Info("OnActorGlobalBanned called with empty userId, not sending update to master");
                }
                return;
            }

            var updateGameEvent = this.GetUpdateGameEvent();
            updateGameEvent.ExcludedUsers = new[]
            {
                new ExcludedActorInfo
                {
                    UserId = actor.UserId ?? string.Empty,
                    Reason = RemoveActorReason.GlobalBanned,
                }
            };
            this.UpdateGameStateOnMaster(updateGameEvent);
        }

        protected UpdateGameEvent GetUpdateGameEvent()
        {
            return new UpdateGameEvent
            {
                GameId = this.Name,
                ActorCount = (byte)this.ActorsManager.ActiveActorsCount,
                IsPersistent = this.IsPersistent,
                InactiveCount = (byte)this.ActorsManager.InactiveActorsCount,
            };
        }

        protected virtual void UpdateGameStateOnMasterOnCreate(JoinGameRequest joinRequest, HivePeer peer)
        {
            if (Log.IsDebugEnabled)
            {
                Log.DebugFormat("UpdateGameStateOnMasterOnCreate: game - '{0}'", this.Name);
            }

            var updateEvent = this.GetUpdateGameEvent();
            updateEvent.MaxPlayers = joinRequest.wellKnownPropertiesCache.MaxPlayer;
            updateEvent.IsOpen = joinRequest.wellKnownPropertiesCache.IsOpen;
            updateEvent.IsVisible = joinRequest.wellKnownPropertiesCache.IsVisible;
            updateEvent.PropertyFilter = joinRequest.wellKnownPropertiesCache.LobbyProperties;
            updateEvent.CheckUserIdOnJoin = this.RoomState.CheckUserOnJoin;
            updateEvent.NewUsers = new[] { peer.UserId ?? string.Empty };
            updateEvent.LobbyId = this.LobbyId;
            updateEvent.LobbyType = (byte)this.LobbyType;
            updateEvent.Reinitialize = true;
            updateEvent.Replication = ReplicationId.Renitialization;

            var properties = this.GetLobbyGameProperties(joinRequest.properties);
            if (properties != null && properties.Count > 0)
            {
                updateEvent.GameProperties = properties;
            }

            if (this.ActorsManager.InactiveActorsCount > 0)
            {
                updateEvent.InactiveUsers = this.ActorsManager.InactiveActors.Select(a => (a.UserId ?? string.Empty)).ToArray();
            }

            if (this.ActorsManager.ExpectedUsers.Count > 0)
            {
                updateEvent.ExpectedUsers = this.ActorsManager.ExpectedUsers.ToArray();
            }

            this.UpdateGameStateOnMaster(updateEvent);
        }

        protected virtual void UpdateGameStateOnMaster(byte? newMaxPlayer = null, bool? newIsOpen = null, bool? newIsVisible = null,
            object[] lobbyPropertyFilter = null, Hashtable gameProperties = null, string[] newUserId = null,
            string removedUserId = null, string[] inactiveList = null, ExcludedActorInfo[] excludedActorInfos = null,
            string[] expectedList = null, bool? checkUserIdOnJoin = null, byte replication = 0)
        {
            if (Log.IsDebugEnabled)
            {
                Log.DebugFormat("UpdateGameStateOnMaster: game - '{0}', reinitialize:{1}, replication:{2}", this.Name, replication > 0, replication);
            }

            var updateGameEvent = new UpdateGameEvent
            {
                GameId = this.Name,
                ActorCount = (byte)this.ActorsManager.ActiveActorsCount,
                Reinitialize = replication > 0,
                MaxPlayers = newMaxPlayer,
                IsOpen = newIsOpen,
                IsVisible = newIsVisible,
                IsPersistent = this.IsPersistent,
                InactiveCount = (byte)this.ActorsManager.InactiveActorsCount,
                PropertyFilter = lobbyPropertyFilter,
                CheckUserIdOnJoin = checkUserIdOnJoin,
                Replication = replication
            };

            // TBD - we have to send this in case we are re-joining and we are creating the room (load)
            if (replication > 0)
            {
                updateGameEvent.LobbyId = this.LobbyId;
                updateGameEvent.LobbyType = (byte)this.LobbyType;
            }

            if (gameProperties != null && gameProperties.Count > 0)
            {
                updateGameEvent.GameProperties = gameProperties;
            }

            if (newUserId != null)
            {
                updateGameEvent.NewUsers = newUserId;
            }

            if (removedUserId != null)
            {
                updateGameEvent.RemovedUsers = new[] { removedUserId };
            }

            if (excludedActorInfos != null)
            {
                updateGameEvent.ExcludedUsers = excludedActorInfos;
            }

            if (expectedList != null)
            {
                updateGameEvent.ExpectedUsers = expectedList;
            }

            this.UpdateGameStateOnMaster(updateGameEvent);
        }

        protected virtual void UpdateGameStateOnMaster(UpdateGameEvent updateEvent)
        {
            this.Application.GameUpdatesBatcher.SendGameUpdate(updateEvent);
            //var eventData = new EventData((byte)ServerEventCode.UpdateGameState, updateEvent);
            //var connection = this.Application.MasterServerConnection;
            //if (connection != null)
            //{
            //    connection.SendEventIfRegistered(eventData, new SendParameters());
            //}
            //else
            //{
            //    if (Log.IsDebugEnabled)
            //    {
            //        Log.DebugFormat("Can not send game state update. Master connection is not set. Game:{0}", this.Name);
            //    }
            //}
        }

        protected virtual void RemoveGameStateFromMaster()
        {
            var connection = this.Application.MasterServerConnection;
            if (connection != null)
            {
                this.Application.GameUpdatesBatcher.OnRemoveGame();
                var removeReason = this.FailedOnCreate ? GameRemoveReason.GameRemoveFailedToCreate : GameRemoveReason.GameRemoveClose;
                connection.RemoveGameState(this.Name, removeReason);
            }
        }

        /// <summary>
        /// Check routine to validate that the game is valid (ie., it is removed from the game cache if it has no longer any actors etc.). 
        /// CheckGame() is called by the Application at regular intervals. 
        /// </summary>
        protected virtual void CheckGame()
        {
            if (this.ActorsManager.ActiveActorsCount == 0 &&
                (DateTime.Now - this.removalStartTimestamp).TotalMinutes > 60)
            {
                // double check if the game is still in cache: 
                if (this.GameCache.TryGetRoomReference(this.Name, null, out RoomReference room))
                {
                    this.logRoomRemoval = true;
                    if (this.removalStartTimestamp == DateTime.MinValue)// that means that we did not start removal
                    {
                        Log.Warn($"Game with 0 Actors is in cache:'{this.roomCache.GetDebugString(this.Name)}'." +
                                 $" Actors Dump:'{this.ActorsManager.DumpActors()}', " +
                                 $"RemovePath:'{this.RemoveRoomPathString}', " +
                                 $"IsDisposed:{this.IsDisposed}, IsClosed:{this.IsFinished}, EmptyRoomLiveTime:{this.EmptyRoomLiveTime}," +
                                 $"RemovalTimeStamp:{this.removalStartTimestamp}");
                    }
                    else // that means that we started removal and room was not removed more than one hour
                    {
                        Log.Error($"Game with 0 Actors is in cache more then hour after removal start:'{this.roomCache.GetDebugString(this.Name)}'." +
                                 $" Actors Dump:'{this.ActorsManager.DumpActors()}', " +
                                 $"RemovePath:'{this.RemoveRoomPathString}', " +
                                 $"IsDisposed:{this.IsDisposed}, IsClosed:{this.IsFinished}, EmptyRoomLiveTime:{this.EmptyRoomLiveTime}," +
                                 $"RemovalTimeStamp:{this.removalStartTimestamp}");
                    }

                    room.Dispose();
                }
            }

            this.CheckPeerStates();
        }

        private void CheckPeerStates()
        {
            if (this.LastTouchLimitMilliseconds <= 0)
            {
                return;
            }

            try
            {
                if (Log.IsDebugEnabled)
                {
                    Log.DebugFormat("Checking peers last touch on room {0} - {1} Actors - Limit: {2}ms, IsDisposed:{3},RemovePath:{4}",
                        this.Name, this.ActorsManager.ActiveActorsCount, this.LastTouchLimitMilliseconds, this.IsDisposed, this.RemoveRoomPathString);
                }

                var currentTime = DateTime.UtcNow;
                var inactivityInterval = new TimeSpan(GameServerSettings.Default.InactivityTimeoutHours, 0, 0);
                var lastTouch = -1;

                foreach (var actor in this.ActiveActors)
                {
                    var peer = actor.Peer;

                    if (peer.ConnectionState == ConnectionState.Disposed)
                    {
                        if (peer.CheckCount > 0)
                        {
                            var msg = $"Check Peer State: Actors peer is in Disposed state. Remove it. Game:{this.Name}, p:'{peer}'";

                            // remove directly from game, because reference in peer is null
                            var message = new RoomMessage((byte)Hive.Messages.GameMessageCodes.RemovePeerFromGame, new object[]
                            {
                                peer, (int)LeaveReason.ClientDisconnect, msg
                            });

                            this.EnqueueMessage(message);

                            Log.Warn(msg);

                            this.roomCache.GetRoomReference(this.Name, peer)?.Dispose();
                        }
                        else
                        {
                            ++peer.CheckCount;
                        }
                    }
                    else
                    {
                        lastTouch = peer.GetLastTouch();
                        if (lastTouch > this.LastTouchLimitMilliseconds)
                        {
                            var msg =
                                $"Check Peer State: Peer last touch {lastTouch} exceeds {this.LastTouchLimitMilliseconds}. Removing peer from room {this.Name}. p:{peer}";

                            Log.Warn(msg);

                            peer.RemovePeerFromCurrentRoom(LeaveReason.PeerLastTouchTimedout, msg);

                            try
                            {
                                peer.AbortConnection();
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex);
                            }

                        }
                        else if (lastTouch == -1)
                        {
                            switch (peer.CheckCount)
                            {
                                case 0:
                                    {
                                        ++peer.CheckCount;
                                        break;
                                    }
                                case 1:
                                    {
                                        ++peer.CheckCount;
                                        peer.GetStats(out var roundTripTime, out var roundTripVariance, out var numOfFailures);
                                        if (roundTripTime == -1)
                                        {
                                            roundTripTime = peer.RoundTripTime;
                                            roundTripVariance = peer.RoundTripTimeVariance;
                                            numOfFailures = peer.NumFailures;
                                        }
                                        // not sure what to do in this case.
                                        var msg = $"Check Peer State: Peer last touch is -1. rtt:{roundTripTime}, " +
                                                  $"rtv:{roundTripVariance}, numOfFailures:{numOfFailures} p:'{peer}'";

                                        var message = new RoomMessage(
                                            (byte)Hive.Messages.GameMessageCodes.RemovePeerFromGame, new object[]
                                            {
                                            peer, (int) LeaveReason.ClientDisconnect, msg
                                            });

                                        this.EnqueueMessage(message);

                                        Log.Warn(msg);
                                        break;
                                    }
                                case 2:
                                    {
                                        // remove directly from game

                                        var msg = string.Format("Check Peer State: Peer last touch is -1. Game:{1}, p:'{0}'", peer, this.Name);

                                        var message = new RoomMessage(
                                            (byte)Hive.Messages.GameMessageCodes.RemovePeerFromGame, new object[]
                                            {
                                            peer, (int) LeaveReason.ClientDisconnect, msg
                                            });

                                        this.EnqueueMessage(message);

                                        Log.Warn(msg);
                                        break;
                                    }
                            }
                        }
                        else
                        {
                            if (Log.IsDebugEnabled)
                            {
                                var msg =
                                    $"Peer is active. LastTouchLimitMilliseconds: {this.LastTouchLimitMilliseconds}, lastTouch={lastTouch}, Room: {this.Name}. p:{peer}";

                                Log.Debug(msg);
                            }
                        }
                    }

                    var gameClientPeer = (GameClientPeer)peer;
                    if (currentTime - gameClientPeer.LastActivity >= inactivityInterval)
                    {
                        var msg = string.Format("Check Peer State: Peer is not active in game more then {0}. LastTouch:{3}. Removing peer from room. game:{1}, p:{2}",
                            inactivityInterval, this.Name, gameClientPeer, lastTouch);

                        Log.Warn(inactivePeerLogGuard, msg);

                        gameClientPeer.RemovePeerFromCurrentRoom(LeaveReason.PeerLastTouchTimedout, msg);

                        try
                        {
                            gameClientPeer.AbortConnection();
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }

        protected override void OnGameFull(HivePeer peer, JoinGameRequest joinGameRequest, SendParameters sendParameters)
        {
            base.OnGameFull(peer, joinGameRequest, sendParameters);

            if (this.onGameIsFullErrorsCounter.Increment(1) >= this.ErrorsCountToInitiateUpdate)
            {
                if (this.gameUpdaterGuard.Increment(1) == 1)
                {
                    if (Log.IsWarnEnabled)
                    {
                        Log.WarnFormat(fullGameReinitLogGuard, "Game '{0}' has sent reinit message to master after getting {1} 'Game is full' errors",
                            this.Name, this.onGameIsFullErrorsCounter.Value);
                    }
                    this.ReinitializeGameOnMaster();
                }
            }
            if (Log.IsDebugEnabled)
            {
                Log.DebugFormat("Game '{0}' got '{1}' errors 'Game full'", this.Name, this.onGameIsFullErrorsCounter.Value);
            }
        }
    }
}