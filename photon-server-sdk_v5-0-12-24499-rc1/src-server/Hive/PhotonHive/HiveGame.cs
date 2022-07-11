// --------------------------------------------------------------------------------------------------------------------
// <copyright file="HiveGame.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------
//#define MAXPLAYERS_STRICK_ABOUT_EXPECTED_USERS
using Photon.Common;
using Photon.Hive.Collections;
using Photon.SocketServer.Diagnostics;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ExitGames.Concurrency.Fibers;
using ExitGames.Logging;

using Photon.Hive.Caching;
using Photon.Hive.Common.Lobby;
using Photon.Hive.Diagnostics;
using Photon.Hive.Diagnostics.OperationLogging;
using Photon.Hive.Events;
using Photon.Hive.Messages;
using Photon.Hive.Operations;
using Photon.Hive.Plugin;
using Photon.SocketServer;
using Photon.SocketServer.Rpc;
using SendParameters = Photon.SocketServer.SendParameters;

namespace Photon.Hive
{

    /// <summary>
    ///   A <see cref = "Room" /> that supports the following requests:
    ///   <list type = "bullet">
    ///     <item>
    ///       <see cref = "JoinGameRequest" />
    ///     </item>
    ///     <item>
    ///       <see cref = "RaiseEventRequest" />
    ///     </item>
    ///     <item>
    ///       <see cref = "SetPropertiesRequest" />
    ///     </item>
    ///     <item>
    ///       <see cref = "GetPropertiesRequest" />
    ///     </item>
    ///     <item>
    ///       <see cref = "LeaveRequest" />
    ///     </item>
    ///   </list>
    /// </summary>
    public abstract class HiveGame : Room
    {
        #region Constants and Fields

        protected readonly LogQueue LogQueue;

        private static readonly ILogger Log = LogManager.GetCurrentClassLogger();

        private static readonly LogCountGuard forceGameCloseLogCountGuard = new LogCountGuard(new TimeSpan(0, 0, 10));
        private static readonly LogCountGuard exceptionLogGuard = new LogCountGuard(new TimeSpan(0, 1, 0));
        private static readonly LogCountGuard noExpectedUsersLogGuard = new LogCountGuard(new TimeSpan(0, 0, 10));
#if MAXPLAYERS_STRICK_ABOUT_EXPECTED_USERS
        private static readonly LogCountGuard maxPlayersTooSmallLogGuard = new LogCountGuard(new TimeSpan(0, 0, 10));
#endif

        protected IHiveGameAppCounters gameAppCounters = NullHiveGameAppCounters.Instance;

        protected static readonly string[] EmptyStringArray = {};

        private readonly LogCountGuard updateEventCacheLogCountGuard = new LogCountGuard(new TimeSpan(0, 0, 0, 10));

        private readonly LogCountGuard peersActorIsNull = new LogCountGuard(new TimeSpan(0, 0, 0, 1));

        #endregion

#region Constructors and Destructors

        /// <summary>
        ///   Initializes a new instance of the <see cref = "HiveGame" /> class.
        /// </summary>
        /// <param name = "gameName">
        ///     The name of the game.
        /// </param>
        /// <param name="roomCache">
        ///     The <see cref="RoomCacheBase"/> instance to which the room belongs.
        /// </param>
        /// <param name="gameStateFactory">Custom factory for GameState. if null is set, default factory is used</param>
        /// <param name="maxEmptyRoomTTL">
        ///     A value indicating how long the room instance will be kept alive 
        ///     in the room cache after all peers have left the room.
        /// </param>
        /// <param name="executionFiber"></param>
        public HiveGame(string gameName, RoomCacheBase roomCache, IGameStateFactory gameStateFactory = null, 
            int maxEmptyRoomTTL = 0, ExtendedPoolFiber executionFiber = null)
            : base(gameName, executionFiber, roomCache, gameStateFactory, maxEmptyRoomTTL)
        {
            this.ExecutionFiber.Start();

            this.MasterClientId = 0;
            this.IsOpen = true;
            this.IsVisible = true;

            this.EventCache.SetGameAppCounters(NullHiveGameAppCounters.Instance);
            this.EventCache.Slice = 0;

            this.LogQueue = new LogQueue("Game " + gameName, LogQueue.DefaultCapacity);

        }

#endregion

#region Properties

        public IHiveGameAppCounters GameAppCounters
        {
            get => this.gameAppCounters;
            set
            {
                if (value == null)
                {
                    value = NullHiveGameAppCounters.Instance;
                }

                this.gameAppCounters = value;
                this.EventCache.SetGameAppCounters(value);
            }
        }

        public int MasterClientId { get; private set; }

        public virtual bool IsPersistent => false;

        protected IEnumerable<Actor> InactiveActors => this.ActorsManager.InactiveActors;
        protected IEnumerable<Actor> ActiveActors => this.ActorsManager.ActiveActors;

        private bool PublishUserId
        {
            set => this.RoomState.PublishUserId = value;
            get => this.RoomState.PublishUserId;
        }

        public bool IsOpen
        {
            set => this.RoomState.IsOpen = value;
            get => this.RoomState.IsOpen;
        }

        public bool IsVisible
        {
            set => this.RoomState.IsVisible = value;
            get => this.RoomState.IsVisible;
        }

        /// <summary>
        /// whether finished. it is not same as IsOpen == false.
        /// </summary>
        protected bool IsFinished { get; set; }

        public string LobbyId
        {
            set => this.RoomState.LobbyId = value;
            get => this.RoomState.LobbyId;
        }

        /// <summary>
        /// Gets or sets a value indicating whether cached events are automatically deleted for 
        /// actors which are leaving a room.
        /// </summary>
        public bool DeleteCacheOnLeave
        {
            set => this.RoomState.DeleteCacheOnLeave = value;
            get => this.RoomState.DeleteCacheOnLeave;
        }

        /// <summary>
        /// Contains the keys of the game properties hashtable which should be listed in the lobby.
        /// </summary>
        public HashSet<object> LobbyProperties
        {
            set => this.RoomState.LobbyProperties = value;
            get => this.RoomState.LobbyProperties;
        }

        public AppLobbyType LobbyType
        {
            set => this.RoomState.LobbyType = value;
            get => this.RoomState.LobbyType;
        }

        public byte MaxPlayers
        {
            set => this.RoomState.MaxPlayers = value;
            get => this.RoomState.MaxPlayers;
        }

        /// <summary>
        /// Player live time
        /// </summary>
        public int PlayerTTL
        {
            set => this.RoomState.PlayerTTL = value;
            get => this.RoomState.PlayerTTL;
        }

        /// <summary>
        /// Gets or sets a value indicating if common room events (Join, Leave) will suppressed.
        /// </summary>
        public bool SuppressRoomEvents
        {
            set => this.RoomState.SuppressRoomEvents = value;
            get => this.RoomState.SuppressRoomEvents;
        }

        public bool SuppressPlayerInfo
        {
            set => this.RoomState.SuppressPlayerInfo = value;
            get => this.RoomState.SuppressPlayerInfo;
        }

        public RoomEventCacheManager EventCache => this.RoomState.EventCache;

        /// <summary> 
        ///   Contains <see cref = "Caching.EventCache" />s for all actors.
        /// </summary>
        public EventCacheDictionary ActorEventCache => this.RoomState.ActorEventCache;

        protected GroupManager GroupManager => this.RoomState.GroupManager;

        public ActorsManager ActorsManager => this.RoomState.ActorsManager;

        protected GameState RoomState => this.roomState;

        public bool CheckUserOnJoin
        {
            get => this.RoomState.CheckUserOnJoin;
            set => this.RoomState.CheckUserOnJoin = value;
        }

        private bool BroadcastPropsChangesToAll
        {
            get => this.RoomState.BroadcastPropsChangesToAll;
            set => this.RoomState.BroadcastPropsChangesToAll = value;
        }

        public bool CacheDiscarded => this.EventCache.Discarded || this.ActorEventCache.Discarded;

        public int LimitMaxPropertiesSizePerGame { get; set; } = 0;

#endregion

#region Public Methods

        public override string ToString()
        {
            string value;
            try
            {
                value =
                    $"HiveGame: name={this.Name} actorNumberCounter={this.ActorsManager.ActorNumberCounter} DeleteCacheOnLeave={this.DeleteCacheOnLeave} RoomTTL={this.EmptyRoomLiveTime} IsOpen={this.IsOpen} " +
                    $"IsVisible={this.IsVisible} LobbyId={this.LobbyId} LobbyType={this.LobbyType} MaxPlayers={this.MaxPlayers} PlayerTTL={this.PlayerTTL} SuppressRoomEvents={this.SuppressRoomEvents} " +
                    $"Active={this.ActiveActors.Count()} Inactive={this.InactiveActors.Count()} IsClosed={this.IsFinished} UtcCreated={this.UtcCreated:yyyy-MM-dd--HH:mm:ss.fff} RemovePath={this.RemoveRoomPath}";

            }
            catch (Exception e)
            {
                Log.Error(e);
                value = $"HiveGame: name={this.Name} UtcCreated={this.UtcCreated:yyyy-MM-dd--HH:mm:ss.fff}";
            }
            return value;
        }

        public virtual void RemoveInactiveActor(Actor actor)
        {
            this.ActorsManager.RemoveInactiveActor(this, actor);
        }

        internal ActorGroup GetActorGroup(byte groupId)
        {
            return this.GroupManager.GetActorGroup(groupId);
        }

        public void OnActorRemoved(Actor actor)
        {
            this.CleanupActor(actor);
            actor.Peer?.ScheduleDisconnect((int)ErrorCode.ActorRemoved, PeerBase.DefaultDisconnectInterval);
        }

        public void OnActorDeactivated(Actor actor)
        {
            // player disconnected
            this.PublishEventDisconnected(actor, this.UpdateMasterClientId(actor));
            this.DeactivateActor(actor);
        }

#endregion

#region Methods

        protected override void OnClose()
        {
            if (this.EventCache.IsTotalLimitExceeded)
            {
                Log.WarnFormat("Game exceeded cached events TOTAL limit. Game:'{0}', Limit:{1}, Count:{2}",
                    this.Name, this.EventCache.CachedEventsCountLimit, this.EventCache.MaxCachedEventsInTotal);
            }

            if (this.EventCache.IsSlicesLimitExceeded)
            {
                Log.WarnFormat("Game exceeded cached events limit for SliceS count. Game:'{0}', Limit:{1}, Count:{2}",
                    this.Name, this.EventCache.SlicesCountLimit, this.EventCache.MaxSlicesCount);
            }

            if (this.ActorEventCache.IsTotalLimitExceeded)
            {
                Log.WarnFormat("Game exceeded cached events 'per actor' TOTAL limit. Game:'{0}', Limit:{1}, Count:{2}",
                    this.Name, this.ActorEventCache.CachedEventsCountLimit, this.ActorEventCache.MaxCachedEventsInTotal);
            }

            base.OnClose();
        }

        private bool CheckBeforeJoinThisIsNewCreatedRoom(JoinGameRequest request)
        {
            if (this.ActorsManager.ActorNumberCounter == 0)
            {
                return true;
            }

            return false;
        }

        private void PublishEventDisconnected(Actor actor, int? newMasterClientId)
        {
            if (this.SuppressRoomEvents)
            {
                return;
            }

            if (this.ActorsManager.ActiveActorsCount > 0 && actor != null)
            {
                // instead of ev disconnect we discussed a prop. change
                // decided for ev because for clients its an important state change
                // and they would have to filter property changes ...

                //var propertiesChangedEvent = new PropertiesChangedEvent(0)
                //{
                //    TargetActorNumber = actor.ActorNr,
                //    Properties = new Hashtable()
                //    {
                //        {(byte)ActorParameter.IsInactive, true}
                //    }
                //};

                var actorsList = this.ActorsManager.ActiveActors.Select(a => a.ActorNr).ToArray();

                var disconnectEvent = new LeaveEvent(actor.ActorNr, actorsList)
                {
                    IsInactive = true,
                    MasterClientId = newMasterClientId
                };

                this.PublishEvent(disconnectEvent, this.ActiveActors, new SendParameters());
            }
        }

        private void UpdateMasterClientId()
        {
            var lowestid = int.MaxValue;
            foreach (var actor in this.ActiveActors)
            {
                if (actor.ActorNr < lowestid)
                {
                    lowestid = actor.ActorNr;
                }
            }

            this.MasterClientId = lowestid == int.MaxValue ? 0 : lowestid;
            this.roomState.Properties.Set((byte)GameParameter.MasterClientId, this.MasterClientId);
        }

        private int? UpdateMasterClientId(Actor actor)
        {
            if (this.MasterClientId == actor.ActorNr)
            {
                this.UpdateMasterClientId();
                if (Log.IsDebugEnabled)
                {
                    Log.DebugFormat("Actor {0} left. MasterClientId is {1}", actor.ActorNr, this.MasterClientId);
                }
                return this.MasterClientId;
            }
            return null;
        }

        protected virtual void CleanupActor(Actor actor)
        {
            if (this.DeleteCacheOnLeave)
            {
                if (Log.IsDebugEnabled)
                {
                    Log.DebugFormat("Clean up actor {0} - DeleteCacheOnLeave:", actor);
                }

                this.ActorEventCache.RemoveEventCache(actor.ActorNr);
                this.EventCache.RemoveEventsByActor(actor.ActorNr);
            }

            actor.KillInActiveActorCleanUpTimer();
            actor.RemoveGroups(new byte[0]);

            // raise leave event
            this.PublishLeaveEvent(actor, this.UpdateMasterClientId(actor));
        }

        protected virtual void DeactivateActor(Actor actor)
        {
            
        }

        protected bool CreateGame(HivePeer peer, JoinGameRequest request, SendParameters sendParameters)
        {
            peer.SetJoinStage(HivePeer.JoinStages.CreatingOrLoadingGame);

            return this.Join(peer, request, sendParameters);
        }

        private void CopyGamePropertiesForMasterUpdate(JoinGameRequest request)
        {
            request.properties = this.Properties.GetProperties();

            if (this.MaxPlayers != 0)
            {
                request.wellKnownPropertiesCache.MaxPlayer = this.MaxPlayers;
            }

            request.wellKnownPropertiesCache.IsOpen = this.IsOpen;
            request.wellKnownPropertiesCache.IsVisible = this.IsVisible;

            if (this.LobbyProperties != null)
            {
                request.wellKnownPropertiesCache.LobbyProperties = new object[this.LobbyProperties.Count];
                this.LobbyProperties.CopyTo(request.wellKnownPropertiesCache.LobbyProperties);
            }
        }

        private void ApplyGameProperties(JoinGameRequest createRequest)
        {
            // set default properties
            if (createRequest.wellKnownPropertiesCache.MaxPlayer.HasValue 
                && createRequest.wellKnownPropertiesCache.MaxPlayer.Value != this.MaxPlayers)
            {
                this.MaxPlayers = createRequest.wellKnownPropertiesCache.MaxPlayer.Value;
            }

            if (createRequest.wellKnownPropertiesCache.IsOpen.HasValue 
                && createRequest.wellKnownPropertiesCache.IsOpen.Value != this.IsOpen)
            {
                this.IsOpen = createRequest.wellKnownPropertiesCache.IsOpen.Value;
            }

            if (createRequest.wellKnownPropertiesCache.IsVisible.HasValue
                && createRequest.wellKnownPropertiesCache.IsVisible.Value != this.IsVisible)
            {
                this.IsVisible = createRequest.wellKnownPropertiesCache.IsVisible.Value;
            }

            if (createRequest.wellKnownPropertiesCache.LobbyProperties != null)
            {
                this.LobbyProperties = new HashSet<object>(createRequest.wellKnownPropertiesCache.LobbyProperties);
            }

            this.LobbyId = createRequest.LobbyName;
            this.LobbyType = (AppLobbyType)createRequest.LobbyType;
        }

        /// <summary>
        ///   Called for each operation in the execution queue.
        ///   Every <see cref = "Room" /> has a queue of incoming operations to execute. 
        ///   Per game <see cref = "ExecuteOperation" /> is never executed multi-threaded, thus all code executed here has thread safe access to all instance members.
        /// </summary>
        /// <param name = "peer">
        ///   The peer.
        /// </param>
        /// <param name = "operation">
        ///   The operation request to execute.
        /// </param>
        /// <param name = "sendParameters">
        ///   The send Parameters.
        /// </param>
        protected override void ExecuteOperation(HivePeer peer, Operation operation, SendParameters sendParameters)
        {
            try
            {
                base.ExecuteOperation(peer, operation, sendParameters);

                this.LogOperation(peer, operation.OperationRequest);

                switch ((OperationCode)operation.Code)
                {
                    case OperationCode.CreateGame:
                    {
                        var joinGameRequest = operation as JoinGameRequest;

                        joinGameRequest.OnStart();
                        this.HandleCreateGameOperation(peer, sendParameters, joinGameRequest);
                        joinGameRequest.OnComplete();
                    }

                    break;

                    case OperationCode.Join:
                    case OperationCode.JoinGame:
                        {
                            var joinGameRequest = operation as JoinGameRequest;

                            joinGameRequest.OnStart();
                            this.HandleJoinGameOperation(peer, sendParameters, joinGameRequest);
                            joinGameRequest.OnComplete();
                        }

                        break;

                    case OperationCode.DebugGame:
                        var debugGameRequest = operation as DebugGameRequest;

                        debugGameRequest.OnStart();
                        this.HandleDebugGameOperation(peer, debugGameRequest, sendParameters);
                        debugGameRequest.OnComplete();
                        break;

                    case OperationCode.Leave:
                        {
                            var leaveOperation = operation as LeaveRequest;

                            leaveOperation.OnStart();
                            this.HandleLeaveOperation(peer, sendParameters, leaveOperation);
                            leaveOperation.OnComplete();
                            break;
                        }

                    case OperationCode.RaiseEvent:
                        {
                            var raiseEventOperation = operation as RaiseEventRequest;
                            raiseEventOperation.ValidateActorsList(this.ActorsManager.AllActors.Count());
                            if (peer.ValidateOperation(raiseEventOperation, sendParameters) == false)
                            {
                                return;
                            }

                            raiseEventOperation.OnStart();
                            this.HandleRaiseEventOperation(peer, raiseEventOperation, sendParameters);
                            raiseEventOperation.OnComplete();
                            break;
                        }

                    case OperationCode.GetProperties:
                        {
                            var getPropertiesOperation = operation as GetPropertiesRequest;

                            getPropertiesOperation.OnStart();
                            this.HandleGetPropertiesOperation(peer, getPropertiesOperation, sendParameters);
                            getPropertiesOperation.OnComplete();
                            break;
                        }

                    case OperationCode.SetProperties:
                    {
                        var setPropertiesOperation = operation as SetPropertiesRequest;

                            setPropertiesOperation.OnStart();
                            this.HandleSetPropertiesOperation(peer, setPropertiesOperation, sendParameters);
                            setPropertiesOperation.OnComplete();
                            break;
                        }

                    case OperationCode.ChangeGroups:
                        {
                            var changeGroupsOperation = operation as ChangeGroups;

                            changeGroupsOperation.OnStart();
                            this.HandleChangeGroupsOperation(peer, changeGroupsOperation, sendParameters);
                            changeGroupsOperation.OnComplete();
                            break;
                        }

                    default:
                        {
                            // we check everything in peer and this should not happen
                            var message = $"Unknown operation code {(OperationCode) operation.Code}";
                            this.SendErrorResponseAndDisconnect(peer, operation.Code, ErrorCode.OperationInvalid, message, sendParameters);

                            // error here to pay attention on this issue
                            Log.Error(message);
                        }

                        break;
                }
            }
            catch (Exception ex)
            {
                if (exceptionLogGuard.IncrementAndCheck())
                {
                    var message = LogExtensions.AddSkippedMessagesInfo(exceptionLogGuard,
                        $"Exception during operation handling: p:{peer}, request:{ValueToString.OperationToString(operation.OperationRequest)}");
                    Log.Error(message, ex);
                }
            }
        }

        /// <summary>
        ///   Gets the actor for a <see cref = "HivePeer" />.
        /// </summary>
        /// <param name = "peer">
        ///   The peer.
        /// </param>
        /// <returns>
        ///   The actor for the peer or null if no actor for the peer exists (this should not happen).
        /// </returns>
        protected Actor GetActorByPeer(HivePeer peer)
        {
            if (peer.Actor != null)
            {
                return peer.Actor;
            }

            if (peer.JoinStage >= 4)
            {
                Log.Warn(peersActorIsNull, $"peer {peer} does not have attached actor");
            }

            var actor = this.ActorsManager.ActorsGetActorByPeer(peer);
            if (actor == null)
            {
                if (Log.IsDebugEnabled)
                {
                    Log.DebugFormat("Actor not found for peer: {0}", peer.ConnectionId);
                }
            }

            return actor;
        }

        protected Hashtable GetLobbyGameProperties(Hashtable source)
        {
            if (source == null || source.Count == 0)
            {
                return null;
            }

            Hashtable gameProperties;

            if (this.LobbyProperties != null)
            {
                // filter for game properties is set, only properties in the specified list 
                // will be reported to the lobby 
                gameProperties = new Hashtable(this.LobbyProperties.Count);

                foreach (object entry in this.LobbyProperties)
                {
                    if (entry != null && source.ContainsKey(entry))
                    {
                        gameProperties.Add(entry, source[entry]);
                    }
                }
            }
            else
            {
                // if no filter is set for properties which should be listed in the lobby
                // all properties are send
                gameProperties = source;
                gameProperties.Remove((byte)GameParameter.MaxPlayers);
                gameProperties.Remove((byte)GameParameter.IsOpen);
                gameProperties.Remove((byte)GameParameter.IsVisible);
                gameProperties.Remove((byte)GameParameter.LobbyProperties);
            }

            return gameProperties;
        }
        
        protected virtual void HandleChangeGroupsOperation(HivePeer peer, ChangeGroups changeGroupsRequest, SendParameters sendParameters)
        {
            // get the actor who send the operation request
            var actor = this.GetActorByPeer(peer);
            if (actor == null)
            {
                return;
            }

            actor.RemoveGroups(changeGroupsRequest.Remove);

            if (changeGroupsRequest.Add != null)
            {
                if (changeGroupsRequest.Add.Length > 0)
                {
                    foreach (byte groupId in changeGroupsRequest.Add)
                    {
                        this.GroupManager.AddActorToGroup(groupId, actor);
                    }
                }
                else
                {
                    this.GroupManager.AddToAllGroups(actor);
                }
            }
        }

        protected abstract void HandleCreateGameOperation(HivePeer peer, SendParameters sendParameters, JoinGameRequest createGameRequest);

        protected virtual void HandleDebugGameOperation(HivePeer peer, DebugGameRequest request, SendParameters sendParameters)
        {
            // Room: Properties; # of cached events
            // Actors:  Count, Last Activity, Actor #, Peer State, Connection ID
            // Room Reference

            // get info from request (was gathered in Peer class before operation was forwarded to the game): 
            var peerInfo = request.Info;
            var debugInfo = peerInfo + this;

            if (Log.IsInfoEnabled)
            {
                Log.Info("DebugGame: " + debugInfo);
            }

            this.LogQueue.WriteLog();

            var debugGameResponse = new DebugGameResponse { Info = debugInfo };

            peer.SendOperationResponse(new OperationResponse(request.OperationRequest.OperationCode, debugGameResponse), sendParameters);
        }

        /// <summary>
        ///   Handles the <see cref = "GetPropertiesRequest" /> operation: Sends the properties with the <see cref = "OperationResponse" />.
        /// </summary>
        /// <param name = "peer">
        ///   The peer.
        /// </param>
        /// <param name = "getPropertiesRequest">
        ///   The operation to handle.
        /// </param>
        /// <param name = "sendParameters">
        ///   The send Parameters.
        /// </param>
        protected virtual void HandleGetPropertiesOperation(HivePeer peer, GetPropertiesRequest getPropertiesRequest, SendParameters sendParameters)
        {
            var response = new GetPropertiesResponse();

            // check if game properties should be returned
            if ((getPropertiesRequest.PropertyType & (byte)PropertyType.Game) == (byte)PropertyType.Game)
            {
                response.GameProperties = this.Properties.GetProperties(getPropertiesRequest.GamePropertyKeys);
            }

            // check if actor properties should be returned
            if ((getPropertiesRequest.PropertyType & (byte)PropertyType.Actor) == (byte)PropertyType.Actor)
            {
                response.ActorProperties = new Hashtable();

                if (getPropertiesRequest.ActorNumbers == null)
                {
                    foreach (var actor in this.ActiveActors)
                    {
                        this.AddActorPropertiesToResponse(getPropertiesRequest, actor, response);
                    }
                }
                else
                {
                    foreach (var actorNumber in getPropertiesRequest.ActorNumbers)
                    {
                        var actor = this.ActorsManager.ActorsGetActorByNumber(actorNumber);
                        this.AddActorPropertiesToResponse(getPropertiesRequest, actor, response);
                    }
                }
            }

            peer.SendOperationResponse(new OperationResponse(getPropertiesRequest.OperationRequest.OperationCode, response), sendParameters);
        }

        private void AddActorPropertiesToResponse(GetPropertiesRequest getPropertiesRequest, Actor actor, GetPropertiesResponse response)
        {
            if (actor == null)
            {
                return;
            }


            // we add userId only if request's filter is null or contains userId
            var actorProperties = actor.Properties.GetProperties(getPropertiesRequest.ActorPropertyKeys);
            if (this.PublishUserId)
            {
                var addUserId = getPropertiesRequest.ActorPropertyKeys == null || getPropertiesRequest.ActorPropertyKeys.Contains((byte) ActorParameter.UserId);

                if (addUserId)
                {
                    actorProperties.Add((byte) ActorParameter.UserId, actor.UserId);
                }
            }

            response.ActorProperties.Add(actor.ActorNr, actorProperties);
        }

        /// <summary>
        /// Handles the <see cref="JoinGameRequest"/>: Joins a peer to a room and calls <see cref="PublishJoinEvent"/>.
        ///   Before a JoinOperation reaches this point (inside a room), the <see cref="HivePeer"/> made 
        ///   sure that it is removed from the previous Room (if there was any).
        /// </summary>
        /// <param name="peer">
        /// The peer.
        /// </param>
        /// <param name="sendParameters">
        /// The send Parameters.
        /// </param>
        /// <param name="joinGameRequest">
        /// The join Game Request.
        /// </param>
        protected abstract void HandleJoinGameOperation(HivePeer peer, SendParameters sendParameters, JoinGameRequest joinGameRequest);

        /// <summary>
        /// Handles the <see cref="LeaveRequest"/> and calls <see cref="RemovePeerFromGame"/>.
        /// </summary>
        /// <param name="peer">
        /// The peer.
        /// </param>
        /// <param name="sendParameters">
        /// The send Parameters.
        /// </param>
        /// <param name="leaveOperation">
        /// The operation.
        /// </param>
        protected abstract void HandleLeaveOperation(HivePeer peer, SendParameters sendParameters, LeaveRequest leaveOperation);

        /// <summary>
        ///   Handles the <see cref = "RaiseEventRequest" />: Sends a <see cref = "CustomEvent" /> to actors in the room.
        /// </summary>
        /// <param name = "peer">
        ///   The peer.
        /// </param>
        /// <param name = "raiseEventRequest">
        ///   The operation
        /// </param>
        /// <param name = "sendParameters">
        ///   The send Parameters.
        /// </param>
        protected abstract void HandleRaiseEventOperation(HivePeer peer, RaiseEventRequest raiseEventRequest, SendParameters sendParameters);

        protected abstract void HandleRemovePeerMessage(HivePeer peer, int reason, string details);

        /// <summary>
        ///   Handles the <see cref = "SetPropertiesRequest" /> and sends event <see cref = "PropertiesChangedEvent" /> to all <see cref = "Actor" />s in the room.
        /// </summary>
        /// <param name = "peer">
        ///   The peer.
        /// </param>
        /// <param name = "request">
        ///   The <see cref = "SetPropertiesRequest" /> operation to handle.
        /// </param>
        /// <param name = "sendParameters">
        ///   The send Parameters.
        /// </param>
        protected abstract void HandleSetPropertiesOperation(HivePeer peer, SetPropertiesRequest request, SendParameters sendParameters);

        private bool Join(HivePeer peer, JoinGameRequest joinRequest, SendParameters sendParameters)
        {
            if (Log.IsDebugEnabled)
            {
                Log.DebugFormat("Processing Join from IP: {0} to port: {1}", peer.RemoteIP, peer.LocalPort);
            }

            if (this.JoinApplyGameStateChanges(peer, joinRequest, sendParameters, out var a))
            {
                return this.JoinSendResponseAndEvents(peer, joinRequest, sendParameters, a, new ProcessJoinParams());
            }

            this.JoinFailureHandler(LeaveReason.ManagedDisconnect, peer, joinRequest);

            return false;
        }

        protected virtual void JoinFailureHandler(byte leaveReason, HivePeer peer, JoinGameRequest request)
        {
            if (Log.IsDebugEnabled)
            {
                Log.DebugFormat("JoinFailureHandler is called for peer with reason:{0}.room:{1},p:{2}", request.FailureReason, this.Name, peer);
            }
            peer.OnJoinFailed(request.FailureReason, request.FailureMessage);
        }

        protected bool JoinApplyGameStateChanges(HivePeer peer, JoinGameRequest joinRequest, SendParameters sendParameters, out Actor actor)
        {
            var isCreatingGame = false;
            var isNewGame = false;
            if (peer.JoinStage == HivePeer.JoinStages.CreatingOrLoadingGame)
            {
                isCreatingGame = true;
                if (this.CheckBeforeJoinThisIsNewCreatedRoom(joinRequest))
                {
                    isNewGame = true;
                    this.ApplyGameProperties(joinRequest);
                }
                else
                {
                    this.CopyGamePropertiesForMasterUpdate(joinRequest);
                }
            }

            actor = null;
            peer.SetJoinStage(HivePeer.JoinStages.ConvertingParams);

            if (this.IsDisposed)
            {
                // join arrived after being disposed - repeat join operation
                if (Log.IsWarnEnabled)
                {
                    Log.WarnFormat("Join operation on disposed game. GameName={0}", this.Name);
                }

                return false;
            }

            if (!this.ConvertParamsAndValidateGame(peer, joinRequest, sendParameters))
            {
                return false;
            }

            peer.SetJoinStage(HivePeer.JoinStages.CheckingCacheSlice);
            if (joinRequest.CacheSlice.HasValue && !this.EventCache.HasSlice(joinRequest.CacheSlice.Value))
            {
                var msg = string.Format(HiveErrorMessages.CacheSliceNoAviable, joinRequest.CacheSlice);
                this.SendErrorResponse(peer, joinRequest.OperationCode, ErrorCode.OperationInvalid, msg, sendParameters);
                joinRequest.OnJoinFailed(ErrorCode.OperationInvalid, msg);

                if (Log.IsDebugEnabled)
                {
                    Log.DebugFormat("JoinApplyGameStateChanges: Game '{0}' userId '{1}' failed to join. msg:{2} -- peer:{3}",
                        this.Name, peer.UserId, msg, peer);
                }
                return false;
            }

            peer.SetJoinStage(HivePeer.JoinStages.AddingActor);
            // create an new actor
            if (this.TryAddPeerToGame(peer, joinRequest.ActorNr, out actor, out var isNewActor, out var errorcode, out var reason, joinRequest) == false)
            {
                this.SendErrorResponse(peer, joinRequest.OperationCode, errorcode, reason, sendParameters);
                joinRequest.OnJoinFailed(errorcode, reason);

                if (Log.IsDebugEnabled)
                {
                    Log.DebugFormat("JoinApplyGameStateChanges: Game '{0}' userId '{1}' failed to join. msg:{2} -- peer:{3}",
                        this.Name, peer.UserId, reason, peer);
                }
                return false;
            }

            // check if a room removal is in progress and cancel it if so
            if (this.RemoveTimer != null)
            {
                this.RemoveTimer.Dispose();
                this.RemoveTimer = null;
            }

            if (this.MasterClientId == 0)
            {
                this.UpdateMasterClientId();
            }

            if (Log.IsDebugEnabled)
            {
                Log.DebugFormat("JoinApplyGameStateChanges: Actor {0} is added. MasterClientId is {1}", actor.ActorNr, this.MasterClientId);
            }

            peer.SetJoinStage(HivePeer.JoinStages.CheckAfterJoinParams);

            // set game properties for join from the first actor (game creator)
            if (isNewGame)
            {
                this.RoomState.RoomFlags = joinRequest.RoomFlags;
                this.Properties.DeleteNullProps = joinRequest.DeleteNullProps;

                if (this.MaxEmptyRoomTTL < joinRequest.EmptyRoomLiveTime)
                {
                    var msg = string.Format(HiveErrorMessages.MaxTTLExceeded, joinRequest.EmptyRoomLiveTime, this.MaxEmptyRoomTTL);
                    this.SendErrorResponse(peer, joinRequest.OperationCode, ErrorCode.OperationInvalid, msg, sendParameters); 
                    joinRequest.OnJoinFailed(ErrorCode.OperationInvalid, msg);

                    if (Log.IsWarnEnabled)
                    {
                        Log.WarnFormat("Game '{0}' userId '{1}' failed to create. msg:{2} -- peer:{3}", this.Name, peer.UserId, msg, peer);
                    }
                    return false;
                }

                if (joinRequest.GameProperties != null)
                {
                    this.Properties.SetProperties(joinRequest.GameProperties, out _, joinRequest.RequestMetaData?[(byte)ParameterKey.GameProperties]?.SubtypeMetaData);
                }

                if (joinRequest.AddUsers != null)
                {
                    this.Properties.Set((byte)GameParameter.ExpectedUsers, joinRequest.AddUsers, 
                        new KeyValuePair<int, int>(2, joinRequest.GetAddUsersSize()));
                }

                if (joinRequest.EmptyRoomLiveTime != 0 && joinRequest.EmptyRoomLiveTime != this.EmptyRoomLiveTime)
                {
                    this.EmptyRoomLiveTime = joinRequest.EmptyRoomLiveTime;
                }

                if (joinRequest.PlayerTTL != 0 && joinRequest.PlayerTTL != this.PlayerTTL)
                {
                    this.PlayerTTL = joinRequest.PlayerTTL;
                }
            }

            if (Log.IsDebugEnabled)
            {
                if (isCreatingGame)
                {
                    Log.DebugFormat(
                        "{0} Game - name={2}, lobbyName={3}, lobbyType={4}, maxPlayers={5}, IsOpen={6}, IsVisible={7}, EmptyRoomLiveTime={8}, PlayerTTL={9}, CheckUserOnJoin={10}, PublishUserId={11}, ExpectedUsers={12}",
                        isNewGame ? "Created" : "Loaded", // 0
                        "", // 1
                        this.Name, // 2
                        this.LobbyId,
                        this.LobbyType,
                        this.MaxPlayers,
                        this.IsOpen,
                        this.IsVisible,
                        this.EmptyRoomLiveTime,
                        this.PlayerTTL,
                        this.CheckUserOnJoin,
                        this.PublishUserId,
                        joinRequest.AddUsers != null ? joinRequest.AddUsers.Aggregate((current, next) => current + ", " + next) : ""
                        );
                }
            }

            if (!this.AddExpectedUsers(joinRequest))
            {
                this.SendErrorResponse(peer, joinRequest.OperationRequest.OperationCode, ErrorCode.SlotError,
                        HiveErrorMessages.CantAddSlots, sendParameters); 
                joinRequest.OnJoinFailed(ErrorCode.SlotError, HiveErrorMessages.CantAddSlots);
                if (Log.IsDebugEnabled)
                {
                    Log.DebugFormat("Game '{0}' userId '{1}' failed to join. msg:{2}", this.Name, peer.UserId, HiveErrorMessages.CantAddSlots);
                    Log.Debug(
                        $"Game '{this.Name}'. MaxPlayers:{this.MaxPlayers}, CheckUserIdOnJoin:{this.CheckUserOnJoin}, All Actors:{ValueToString.ToString(this.ActorsManager)}," +
                        $" ExpectedUsers:{ValueToString.ToString(this.ActorsManager.ExpectedUsers)}");
                }
                return false;
            }

            peer.SetJoinStage(HivePeer.JoinStages.ApplyActorProperties);
            // set custom actor properties if defined
            if (joinRequest.ActorProperties != null)
            {
                // this is set by the server only
                joinRequest.ActorProperties.Remove((byte)ActorParameter.IsInactive);
                joinRequest.ActorProperties.Remove((byte)ActorParameter.UserId);

                actor.Properties.SetProperties(joinRequest.ActorProperties, out _, joinRequest.RequestMetaData?[(byte)ParameterKey.ActorProperties]?.SubtypeMetaData);
            }

            if (!isNewGame && this.ModifyExpectedUsersProperty(joinRequest.AddUsers))
            {
                const byte propertyId = (byte)GameParameter.ExpectedUsers;
                var propertiesChangedEvent = new PropertiesChangedEvent(actor.ActorNr)
                {
                    Properties = new Hashtable
                    {
                        {propertyId, this.Properties.GetProperty(propertyId).Value}
                    }
                };

                joinRequest.PropertiesChangedEvent = propertiesChangedEvent;
            }
            return true;
        }

        private bool ModifyExpectedUsersProperty(string[] expectedUsersToAdd)
        {
            if (expectedUsersToAdd == null || expectedUsersToAdd.Length == 0)
            {
                return false;
            }

            var expectedUsersProperty = this.Properties.GetProperty((byte) GameParameter.ExpectedUsers);
            if (expectedUsersProperty?.Value != null)
            {
                var expectedUsers = (string[])expectedUsersProperty.Value;
                var newExpectedUsers = new string[expectedUsers.Length + expectedUsersToAdd.Length];

                Array.Copy(expectedUsers, newExpectedUsers, expectedUsers.Length);
                Array.Copy(expectedUsersToAdd, 0, newExpectedUsers, expectedUsers.Length, expectedUsersToAdd.Length);

                this.Properties.Set((byte)GameParameter.ExpectedUsers, newExpectedUsers);
            }
            else
            {
                this.Properties.Set((byte)GameParameter.ExpectedUsers, expectedUsersToAdd);
            }
            return true;
        }

        /// <summary>
        /// this method not only allows us to send error response but also we overwrite it to send old error codes to old clients
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="opCode"></param>
        /// <param name="errorCode"></param>
        /// <param name="msg"></param>
        /// <param name="sendParameters"></param>
        /// <param name="errorData"></param>
        protected virtual void SendErrorResponse(HivePeer peer, byte opCode, ErrorCode errorCode, string msg, SendParameters sendParameters,
            Dictionary<byte, object> errorData = null)
        {
            peer.SendOperationResponse(
                new OperationResponse { OperationCode = opCode, ReturnCode = (short)errorCode, DebugMessage = msg, Parameters = errorData, },
                sendParameters);
        }

        protected void SendErrorResponseAndDisconnect(HivePeer peer, byte opCode, ErrorCode errorCode, string msg, SendParameters sendParameters,
            Dictionary<byte, object> errorData = null)
        {
            this.SendErrorResponse(peer, opCode, errorCode, msg, sendParameters, errorData);
            peer.ScheduleDisconnect((short)errorCode, PeerBase.DefaultDisconnectInterval);
        }

        protected virtual bool AddExpectedUsers(JoinGameRequest joinRequest)
        {
            return this.ActorsManager.TryAddExpectedUsers(this, joinRequest);
        }

        protected bool JoinSendResponseAndEvents(HivePeer peer, JoinGameRequest joinRequest, SendParameters sendParameters, Actor actor, ProcessJoinParams prms)
        {
            peer.SetJoinStage(HivePeer.JoinStages.GettingUserResponse);

            var oresponse = this.GetUserJoinResponse(joinRequest, actor, prms);
            peer.SetJoinStage(HivePeer.JoinStages.SendingUserResponse);
            peer.SendOperationResponse(oresponse, sendParameters);

            peer.SetJoinStage(HivePeer.JoinStages.PublishingEvents);
            this.PublishJoinEvent(peer, joinRequest);

            if (joinRequest.PropertiesChangedEvent != null)
            {
                // publish event about changed excepted users property
                this.PublishEvent(joinRequest.PropertiesChangedEvent, this.ActiveActors, sendParameters); 
            }

            peer.SetJoinStage(HivePeer.JoinStages.EventsPublished);
            this.PublishEventCache(peer, joinRequest);

            peer.SetJoinStage(HivePeer.JoinStages.Complete);
            return true;
        }

        protected virtual OperationResponse GetUserJoinResponse(JoinGameRequest joinRequest, Actor actor, ProcessJoinParams prms)
        {
            var joinResponse = new JoinResponse
            {
                ActorNr = actor.ActorNr, 
                RoomFlags = this.RoomState.RoomFlags
            };

            if (this.Properties.Count > 0)
            {
                joinResponse.CurrentGameProperties = this.Properties.GetProperties();
            }

            if (!this.SuppressPlayerInfo)
            {
                foreach (var t in this.ActorsManager)
                {
                    // if actor is joining normally we skip its own properties 
                    // if actor is rejoining we send its own properties
                    if ((t.ActorNr != actor.ActorNr ||
                         (!t.IsInactive && joinRequest.IsRejoining)) &&
                        (t.Properties.Count > 0 || this.PublishUserId))
                    {
                        if (joinResponse.CurrentActorProperties == null)
                        {
                            joinResponse.CurrentActorProperties = new Hashtable();
                        }

                        var actorProperties = t.Properties.GetProperties();
                        if (t.IsInactive)
                        {
                            actorProperties.Add((byte) ActorParameter.IsInactive, true);
                        }

                        if (this.PublishUserId)
                        {
                            actorProperties.Add((byte) ActorParameter.UserId, t.UserId);
                        }

                        joinResponse.CurrentActorProperties.Add(t.ActorNr, actorProperties);
                    }
                }

                var actorList = new List<int>();
                actorList.AddRange(this.ActorsManager.ActorsGetActorNumbers().ToArray());
                actorList.AddRange(this.ActorsManager.InactiveActorsGetActorNumbers().ToArray());

                if (!this.SuppressRoomEvents)
                {
                    joinResponse.Actors = actorList.ToArray();
                }
            }

            var response = new OperationResponse(joinRequest.OperationRequest.OperationCode, joinResponse);
            if (prms.ResponseExtraParameters != null)
            {
                foreach (var extraParameter in prms.ResponseExtraParameters)
                {
                    response.Parameters.Add(extraParameter.Key, extraParameter.Value);
                }
            }
            return response;
        }

        protected void LeaveOperationHandler(HivePeer peer, SendParameters sendParameters, LeaveRequest request)
        {
            this.RemovePeerFromGame(peer, request != null && request.IsCommingBack);

            if (peer != null && request != null)
            {
                // is always reliable, so it gets a response
                peer.SendOperationResponse(new OperationResponse { OperationCode = request.OperationRequest.OperationCode }, sendParameters);
            }
        }

        protected void LogOperation(HivePeer peer, OperationRequest operationRequest)
        {
            if (this.LogQueue.Log.IsDebugEnabled)
            {
                this.LogQueue.Add(new LogEntry("ExecuteOperation: " + (OperationCode)operationRequest.OperationCode, "Peer=" + peer.ConnectionId));
            }
        }

        /// <summary>
        ///   Processes a game message. Messages are used for internal communication.
        ///   Per default only <see cref = "GameMessageCodes.RemovePeerFromGame">message RemovePeerFromGame</see> is handled, 
        ///   a message that is sent when a player leaves a game due to disconnect or due to a subsequent join to a different game.
        /// </summary>
        /// <param name = "message">
        ///   Message to process.
        /// </param>
        protected override void ProcessMessage(IMessage message)
        {
            try
            {
                if (Log.IsDebugEnabled)
                {
                    Log.DebugFormat("ProcessMessage {0}", message.Action);
                }

                switch ((GameMessageCodes)message.Action)
                {
                    case GameMessageCodes.RemovePeerFromGame:
                    {
                        var msg = (object[])message.Message;
                        var peer = (HivePeer)msg[0];
                        var reason = (int)msg[1];
                        var detail = (string)msg[2];

                        this.HandleRemovePeerMessage(peer, reason, detail);

                        if (this.LogQueue.Log.IsDebugEnabled)
                        {
                            this.LogQueue.Add(new LogEntry("ProcessMessage: " + (GameMessageCodes)message.Action, "Peer=" + peer.ConnectionId));
                        }
                    }

                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }

        /// <summary>
        ///   Sends all cached events to a peer.
        /// </summary>
        /// <param name = "litePeer">
        ///   The lite peer that receives the events.
        /// </param>
        /// <param name="joinRequest"></param>
        private void PublishEventCache(HivePeer litePeer, JoinGameRequest joinRequest)
        {
            var @event = new CustomEvent(0, 0, null);
            foreach (KeyValuePair<int, EventCache> entry in this.ActorEventCache)
            {
                var actor = entry.Key;
                var cache = entry.Value;
                @event.ActorNr = actor;
                foreach (KeyValuePair<byte, Hashtable> eventEntry in cache)
                {
                    @event.Code = @eventEntry.Key;
                    @event.Data = @eventEntry.Value;

                    var eventData = new EventData(@event.Code, @event);
                    litePeer.SendEvent(eventData, new SendParameters());
                }
            }

            int cacheSliceRequested = 0;
            if (joinRequest.CacheSlice.HasValue)
            {
                cacheSliceRequested = joinRequest.CacheSlice.Value;
            }
            foreach (var slice in this.EventCache.Slices)
            {
                if (slice >= cacheSliceRequested)
                {
                    if (slice != 0)
                    {
                        var sliceChangedEvent = new CacheSliceChanged(0) { Slice = slice };
                        this.PublishEvent(sliceChangedEvent, this.GetActorByPeer(litePeer), new SendParameters());
                    }

                    foreach (var customEvent in this.EventCache[slice])
                    {
                        var eventData = new EventData(customEvent.Code, customEvent);
                        litePeer.SendEvent(eventData, new SendParameters());
                    }
                }
            }
        }

        /// <summary>
        ///   Sends a <see cref = "JoinEvent" /> to all <see cref = "Actor" />s.
        /// </summary>
        /// <param name = "peer">
        ///   The peer.
        /// </param>
        /// <param name = "joinRequest">
        ///   The join request.
        /// </param>
        private void PublishJoinEvent(HivePeer peer, JoinGameRequest joinRequest)
        {
            if (this.SuppressRoomEvents)
            {
                return;
            }

            var actor = this.GetActorByPeer(peer);
            if (actor == null)
            {
                Log.ErrorFormat("There is no Actor for peer {0}", peer.ConnectionId);
                return;
            }

            // generate a join event and publish to all actors in the room
            var joinEvent = new JoinEvent(actor.ActorNr, this.ActorsManager.ActorsGetActorNumbers().ToArray());

            if (joinRequest.BroadcastActorProperties)
            {
                joinEvent.ActorProperties = joinRequest.ActorProperties;
                if (this.PublishUserId)
                {
                    if (joinEvent.ActorProperties == null)
                    {
                        joinEvent.ActorProperties = new Hashtable();
                    }
                    joinEvent.ActorProperties.Add((byte)ActorParameter.UserId, peer.UserId);
                }
            }

            this.PublishEvent(joinEvent, this.ActiveActors, new SendParameters());
        }

        /// <summary>
        ///   Sends a <see cref = "LeaveEvent" /> to all <see cref = "Actor" />s.
        /// </summary>
        /// <param name = "actor">
        ///   The actor which sends the event.
        /// </param>
        /// <param name="newMasterClientId"></param>
        private void PublishLeaveEvent(Actor actor, int? newMasterClientId)
        {
            if (this.SuppressRoomEvents)
            {
                return;
            }

            if (this.ActorsManager.ActiveActorsCount > 0 && actor != null)
            {
                var actorNumbers = this.ActorsManager.ActorsGetActorNumbers();
                var leaveEvent = new LeaveEvent(actor.ActorNr, actorNumbers.ToArray())
                {
                    MasterClientId = newMasterClientId
                };

                if (actor.Peer == null || actor.Peer.JoinStage >= HivePeer.JoinStages.EventsPublished)
                {
                    this.PublishEvent(leaveEvent, this.ActiveActors, new SendParameters());
                }
            }
        }

        protected bool RaiseEventOperationHandler(HivePeer peer, RaiseEventRequest raiseEventRequest, SendParameters sendParameters, Actor actor)
        {            
            sendParameters.Flush = raiseEventRequest.Flush;

            if (raiseEventRequest.IsCacheSliceIndexOperation)
            {
                var msg = string.Empty;

                if (!this.UpdateCacheSlice(actor.Peer, raiseEventRequest.Cache, raiseEventRequest.CacheSliceIndex, ref msg))
                {

                    this.SendErrorResponse(peer, raiseEventRequest.OperationCode, ErrorCode.OperationInvalid, msg, sendParameters); 

                    // TODO log to client bug logger
                    if (Log.IsDebugEnabled)
                    {
                        Log.DebugFormat("Game '{0}' userId '{1}' failed to update Cache Slice. msg:{2} -- peer:{3}", this.Name, peer.UserId, msg, peer);
                    }
                }
                return false;
            }

            if (raiseEventRequest.IsCacheOpRemoveFromCache)
            {
                this.EventCache.RemoveEventsFromCache(raiseEventRequest);
                var response = new OperationResponse(raiseEventRequest.OperationRequest.OperationCode);
                peer.SendOperationResponse(response, sendParameters);
                return false;
            }

            if (raiseEventRequest.IsCacheOpRemoveFromCacheForActorsLeft)
            {
                var currentActorNumbers = this.ActorsManager.ActorsGetActorNumbers();
                this.EventCache.RemoveEventsForActorsNotInList(currentActorNumbers);
                var response = new OperationResponse(raiseEventRequest.OperationRequest.OperationCode);
                peer.SendOperationResponse(response, sendParameters);
                return false;
            }

            // publish the custom event
            var customEvent = new CustomEvent(actor.ActorNr, raiseEventRequest.EvCode, raiseEventRequest.Data);

            var updateEventCache = false;
            IEnumerable<Actor> recipients;

            if (raiseEventRequest.Actors != null && raiseEventRequest.Actors.Length > 0)
            {
                recipients = this.ActorsManager.ActorsGetActorsByNumbers(raiseEventRequest.Actors);
                if (Log.IsDebugEnabled)
                {
                    Log.Debug("Broadcasting: Recipients selected basing on event actors list");
                }
            }
            else if (raiseEventRequest.Group != 0)
            {
                var group = this.GroupManager.GetActorGroup(raiseEventRequest.Group);
                if (group != null)
                {
                    recipients = group.GetExcludedList(actor);
                }
                else
                {
                    // group does not exist yet because no one joined it yet.
                    // it's not an error to sent events to empty groups so no error response will be sent 
                    return false;
                }
                if (Log.IsDebugEnabled)
                {
                    Log.Debug("Broadcasting: Recipients selected basing on event group");
                }
            }
            else
            {
                switch ((ReceiverGroup)raiseEventRequest.ReceiverGroup)
                {
                    case ReceiverGroup.All:
                        recipients = this.ActiveActors;
                        updateEventCache = true;
                        break;

                    case ReceiverGroup.Others:
                        recipients = this.ActorsManager.ActorsGetExcludedList(actor);
                        updateEventCache = true;
                        break;

                    case ReceiverGroup.MasterClient:
                        recipients = new[] { this.ActorsManager.ActorsGetActorByNumber(this.MasterClientId) };
                        break;

                    default:
                        this.SendErrorResponse(peer, raiseEventRequest.OperationCode, ErrorCode.OperationInvalid,
                            HiveErrorMessages.InvalidReceiverGroup + raiseEventRequest.ReceiverGroup, sendParameters); 

                        if (Log.IsDebugEnabled)
                        {
                            Log.DebugFormat("Game '{0}' user '{1}' sent wrong receiver group. msg:{2} -- peer:{3}", 
                                this.Name, peer.UserId, HiveErrorMessages.InvalidReceiverGroup + raiseEventRequest.ReceiverGroup, peer);
                        }
                        return false;
                }
                if (Log.IsDebugEnabled)
                {
                    Log.Debug($"Broadcasting: Recipients selected basing receiver group: {(ReceiverGroup)raiseEventRequest.ReceiverGroup}");
                }
            }

            if (updateEventCache 
                && raiseEventRequest.Cache != (byte)CacheOperation.DoNotCache
                && !this.CacheDiscarded)
            {
                if (!this.UpdateEventCache(actor, raiseEventRequest, out var msg))
                {
                    this.SendErrorResponse(peer, raiseEventRequest.OperationCode, ErrorCode.OperationInvalid, msg, sendParameters);
                    // TODO log to client bug logger
                    if (Log.IsDebugEnabled)
                    {
                        Log.DebugFormat(this.updateEventCacheLogCountGuard, "Game '{0}' user '{1}' failed to update EventCache. msg:{2} -- peer:{3}",
                            this.Name, peer.UserId, msg, peer);
                    }
                    return false;
                }

                // if we discarded one of caches inside UpdateEventCache
                if (this.CacheDiscarded)
                {
                    Log.WarnFormat(forceGameCloseLogCountGuard, "Game forced to close. cache limit exceeded. ActorsCacheDiscarded={0}, EventsCacheDiscarded:{1}, Room:{2}",
                        this.ActorEventCache.Discarded, this.EventCache.Discarded, this.Name);

                    var wasClosed = false;
                    if (this.IsOpen)
                    {
                        this.ForceGameToClose();
                        wasClosed = true;
                    }
                    this.BroadcastCachedEventsLimitExceededErrorInfoEvent(wasClosed);
                }
            }

            this.PublishEvent(customEvent, recipients, sendParameters);
            return true;
        }

        /// <summary>
        ///   Removes a peer from the game. 
        ///   This method is called if a client sends a <see cref = "LeaveRequest" /> or disconnects.
        /// </summary>
        /// <param name = "peer">
        ///   The <see cref = "HivePeer" /> to remove.
        /// </param>
        /// <param name="isComingBack">whether we expect peer will come back or not</param>
        /// <returns>
        ///   The actor number of the removed actor. 
        ///   If the specified peer does not exist -1 will be returned.
        /// </returns>
        protected virtual int RemovePeerFromGame(HivePeer peer, bool isComingBack)
        {
            return this.ActorsManager.RemovePeerFromGame(this, peer, this.PlayerTTL, isComingBack);
        }

        protected bool SetProperties(int actorNr, Hashtable properties, Hashtable expected, bool broadcast)
        {
            var request = new SetPropertiesRequest(actorNr, properties, expected, broadcast);

            if (!request.IsValid)
            {
                throw new Exception(request.GetErrorMessage());
            }

            if (!this.ValidateAndFillSetPropertiesRequest(null, request, out var errorMsg))
            {
                throw new Exception(errorMsg);
            }

            var propertiesUpdateResult = this.SetNewPropertyValues(request, out errorMsg);

            this.PublishResultsAndSetGameProperties(propertiesUpdateResult, errorMsg, request, null, new SendParameters());
            return true;
        }

        private bool ShouldUpdateMasterOnWelknownValues(SetPropertiesRequest request)
        {
            if (!request.UpdatingGameProperties)
            {
                return false;
            }

            if (request.newMaxPlayer.HasValue && request.newMaxPlayer.Value != this.MaxPlayers)
            {
                return true;
            }

            if (request.newIsOpen.HasValue && request.newIsOpen.Value != this.IsOpen)
            {
                return true;
            }

            if (request.newIsVisible.HasValue && request.newIsVisible.Value != this.IsVisible)
            {
                return true;
            }

            return false;
        }

        protected bool ValidateAndFillSetPropertiesRequest(HivePeer peer, SetPropertiesRequest request, out string errorMsg)
        {
            if (peer != null)
            {
                request.SenderActor = this.GetActorByPeer(peer);
                if (request.SenderActor == null)
                {
                    errorMsg = HiveErrorMessages.PeerNotJoinedToRoom;
                    return false;
                }
            }

            var isGamePropertiesUpdate = request.ActorNumber == 0;
            if (!isGamePropertiesUpdate)
            {
                request.TargetActor = this.ActorsManager.AllActorsGetActorByNumber(request.ActorNumber);
                if (request.TargetActor == null)
                {
                    errorMsg = string.Format(HiveErrorMessages.ActorNotFound, request.ActorNumber);
                    return false;
                }

                if (request.Broadcast && request.TargetActor.Peer != null && request.TargetActor.Peer.JoinStage != HivePeer.JoinStages.Complete)
                {
                    errorMsg = string.Format(HiveErrorMessages.ActorJoiningNotComplete, request.ActorNumber);
                    return false;
                }
            }

            // we need to broadcast either in case if request.Broadcast is true and propertiesUpdateResult is true or
            // if request.Broadcast is false and propertiesUpdateResult is true and CAS was used

            // if broadcast and UsingCAS are set and SuppressPlayerInfo is not set
            // a EvPropertiesChanged event will be send to room actors

            if ((request.Broadcast || request.UsingCAS) && (!this.SuppressPlayerInfo || isGamePropertiesUpdate))
            {
                var broadcastToAll = request.UsingCAS || this.BroadcastPropsChangesToAll;
                // UsingCAS we publish to 'All' else to 'Others'
                request.PublishTo = broadcastToAll ? this.ActorsManager.ActiveActors : this.ActorsManager.ActorsGetExcludedList(request.SenderActor);
            }

            request.UpdateMasterOnWelKnownsUpdate = this.ShouldUpdateMasterOnWelknownValues(request);

            errorMsg = null;
            return true;
        }

        protected virtual void OnGamePropertiesChanged(SetPropertiesRequest request)
        {
            Log.DebugFormat("MaxPlayer={0}, IsOpen={0}, IsVisible={0}, LobbyProperties={0}, GameProperties={0}", request.newMaxPlayer, request.newIsOpen, request.newIsVisible, request.newLobbyProperties, request.newGameProperties);
        }

        protected void PublishResultsAndSetGameProperties(bool propertiesUpdateResult, string errorMessage, 
            SetPropertiesRequest request, HivePeer peer, SendParameters sendParameters)
        {
            //for internal use we allow the peer to be null - meaning this is a property set by the server
            if (peer != null)
            {
                this.SendErrorResponse(peer, request.OperationCode, propertiesUpdateResult ? ErrorCode.Ok : ErrorCode.OperationInvalid, errorMessage, sendParameters);
                if (!propertiesUpdateResult)
                {
                    if (Log.IsWarnEnabled)
                    {
                        Log.WarnFormat("Game '{0}' userId '{1}' failed to SetProperties. msg:{2} -- peer:{3}", 
                            this.Name, peer.UserId, errorMessage, peer);
                    }
                }
            }

            if (request.PublishTo != null && propertiesUpdateResult)
            {
                var propertiesChangedEvent = new PropertiesChangedEvent(request.SenderActor?.ActorNr ?? 0)
                {
                    TargetActorNumber = request.ActorNumber,
                    Properties = request.Properties
                };
                this.PublishEvent(propertiesChangedEvent, request.PublishTo, sendParameters);
            }

            // report to master only if game properties are updated
            if (!request.UpdatingGameProperties || !request.ValuesUpdated.HasFlag(ValuesUpdateFlags.ThereAreChanges))
            {
                return;
            }

            this.UpdateGameProperties(request);
        }

        protected virtual bool SetNewPropertyValues(SetPropertiesRequest request, out string errorMessage)
        {
            var propertyValuesChanged = false;

            if (!request.UpdatingGameProperties)
            {
                if (request.Properties != null)
                {
                    // this is set by the server only
                    request.Properties.Remove((byte)ActorParameter.IsInactive);
                    request.Properties.Remove((byte)ActorParameter.UserId);
                }

                if (!request.TargetActor.Properties.CompareProperties(request.ExpectedValues, out errorMessage))
                {
                    return false;
                }
                request.TargetActor.Properties.SetProperties(request.Properties, out propertyValuesChanged, request.RequestMetaData?.ParamMetaData?.SubtypeMetaData);
            }
            else
            {
                if (!this.GenericCheckBeforeApplyProperties(request, out errorMessage))
                {
                    return false;
                }

                var updateExpectedUsers = false;

                if (request.ExpectedUsers != null)
                {
                    var maxPlayers = request.newMaxPlayer ?? this.MaxPlayers;
                    if (!this.CheckWhetherMayUpdateExpectedUsersList(request.ExpectedUsers, maxPlayers, out errorMessage))
                    {
                        return false;
                    }
                    updateExpectedUsers = true;
                }
                else if (request.ExpectedValues?[(byte) GameParameter.ExpectedUsers] != null) // clearing ExpectedUsers
                {
                    updateExpectedUsers = true;
                }

                this.Properties.SetProperties(request.Properties, out propertyValuesChanged,
                    request.RequestMetaData?.ParamMetaData?.SubtypeMetaData);

                if (request.MasterClientId.HasValue)
                {
                    this.MasterClientId = request.MasterClientId.Value;
                }

                if (propertyValuesChanged && updateExpectedUsers)
                {
                    this.UpdateExpectedUsersList(request.ExpectedUsers);

                    request.ValuesUpdated |= ValuesUpdateFlags.ExpectedUsers;
                }
            }

            if (propertyValuesChanged)
            {
                request.ValuesUpdated |= ValuesUpdateFlags.ThereAreChanges;
            }
            return true;
        }

        private bool GenericCheckBeforeApplyProperties(SetPropertiesRequest request, out string errorMessage)
        {
            if (request.wellKnownPropertiesCache.EmptyRoomTTL.HasValue &&
                request.wellKnownPropertiesCache.EmptyRoomTTL > this.MaxEmptyRoomTTL)
            {
                errorMessage = string.Format(HiveErrorMessages.SetPropertiesMaxTTLExceeded,
                    request.wellKnownPropertiesCache.EmptyRoomTTL, this.MaxEmptyRoomTTL);
                return false;
            }

            if (request.ExpectedUsers != null)
            {
                if (request.ExpectedValues == null || !request.ExpectedValues.ContainsKey((byte)GameParameter.ExpectedUsers))
                {
                    if (request.ExpectedValues != null)// we need this check only for logging
                    {
                        Log.Warn(noExpectedUsersLogGuard, "CAS is used but expectedUsers are not there");
                    }

                    errorMessage = "ExpectedUsers may be updated only in CAS mode";
                    return false;
                }
            }

#if MAXPLAYERS_STRICK_ABOUT_EXPECTED_USERS
            if (request.newMaxPlayer.HasValue && request.newMaxPlayer.Value != 0)
            {
                int newMaxPlayers = request.newMaxPlayer.Value;
                if (request.ExpectedUsers == null)
                {
                    if (this.ActorsManager.Count + this.ActorsManager.YetExpectedUsersCount < newMaxPlayers)
                    {
                        errorMessage = "new MaxPlayer can not be less than current amount of players plus expected users";
                        Log.Warn(maxPlayersTooSmallLogGuard, $"new MaxPlayers value ({newMaxPlayers}) can not be less than current amount of players plus expected users." +
                                                             $" currentAmount_{this.ActorsManager.Count}, yetExpected_{this.ActorsManager.YetExpectedUsersCount}, g:{this}");
                    }
                }
                else if (!this.ActorsManager.CheckMayOverrideSlots(request.ExpectedUsers, newMaxPlayers))
                {
                    errorMessage = "new MaxPlayer can not be less than current amount of players plus new ExpectedUsers";
                    int newYetExpectedCount =
                        request.ExpectedUsers.Count(userId => this.ActorsManager.GetActorByUserId(userId) == null);
                    Log.Warn(maxPlayersTooSmallLogGuard, $"new MaxPlayers value ({newMaxPlayers}) can not be less than amount of players plus expected users." +
                                                         $" currentAmount_{this.ActorsManager.Count}, newYetExpected_{newYetExpectedCount}, g:{this}");
                }
            }
#else
            if (request.newMaxPlayer.HasValue && request.newMaxPlayer.Value != 0 &&
                request.newMaxPlayer.Value < this.ActorsManager.Count)
            {
                errorMessage = "new MaxPlayer can not be less than current amount of players";
                return false;
            }
#endif

            if (request.MasterClientId.HasValue &&
                !this.CheckMasterClientIdValue(request.MasterClientId.Value, out errorMessage))
            {
                return false;
            }

            return this.Properties.CompareProperties(request.ExpectedValues, out errorMessage);
        }

        private void UpdateExpectedUsersList(string[] newExpectedUsersArray)
        {
            if (newExpectedUsersArray != null)
            {
                this.ActorsManager.ExpectedUsers = new List<string>(newExpectedUsersArray);
            }
            else
            {
                this.ActorsManager.ExpectedUsers.Clear();
            }
        }

        private bool CheckWhetherMayUpdateExpectedUsersList(string[] expectedUsers, int maxPlayers, out string errorMsg)
        {
            expectedUsers = expectedUsers ?? EmptyStringArray;

            if (expectedUsers.Any(string.IsNullOrEmpty))
            {
                errorMsg = HiveErrorMessages.SlotCanNotHaveEmptyName;
                return false;
            }

            if (!this.ActorsManager.CheckMayOverrideSlots(expectedUsers, maxPlayers))
            {
                errorMsg = "Not enough places to reserve all requested slots";
                return false;
            }

            errorMsg = string.Empty;
            return true;
        }

        protected void CheckTotalPropertiesSize(SetPropertiesRequest setPropertiesRequest)
        {
            if (!this.IsOpen)
            {
                return;
            }

            var totalSize = this.Properties.TotalSize;

            foreach (var a in this.ActorsManager.AllActors)
            {
                totalSize += a.Properties.TotalSize;
            }

            if (totalSize > this.LimitMaxPropertiesSizePerGame)
            {
                Log.Warn(forceGameCloseLogCountGuard, 
                    $"Game closed because properties limit is exceeded. game={this.Name}, size_{totalSize}, " +
                    $"limit_{this.LimitMaxPropertiesSizePerGame}, PropsCount_{this.Properties.Count}");
                this.ForceGameToClose();

                this.PublishErrorInfoEvent(HiveErrorMessages.RoomClosedPropertiesSizeLimitExceeded, this.ActiveActors, new SendParameters());
            }
        }

        private bool CheckMasterClientIdValue(int masterClientId, out string debugMessage)
        {
            debugMessage = string.Empty;
            if (this.ActorsManager.ActorsGetActorByNumber(masterClientId) == null)
            {
                debugMessage = $"MasterClientId value '{masterClientId}' is invalid. No actor with such number";
                return false;
            }
            return true;
        }

        private void UpdateGameProperties(SetPropertiesRequest request)
        {
            // set default properties
            var doUpdateOnMaster = request.UpdateMasterOnWelKnownsUpdate;

            if (request.newLobbyProperties != null)
            {
                this.LobbyProperties = new HashSet<object>(request.newLobbyProperties);
                doUpdateOnMaster = true;
            }

            if (request.ValuesUpdated.HasFlag(ValuesUpdateFlags.ExpectedUsers))
            {
                doUpdateOnMaster = true;
            }

            if (request.newLobbyProperties != null)
            {
                // if the property filter for the app lobby properties has been changed
                // all game properties are resend to the master server because the application 
                // lobby might not contain all properties specified.
                request.newGameProperties = this.GetLobbyGameProperties(this.Properties.GetProperties());
                doUpdateOnMaster = true;
            }
            else
            {
                // property filter hasn't changed; only the changed properties will
                // be updated in the application lobby
                request.newGameProperties = this.GetLobbyGameProperties(request.Properties);
                doUpdateOnMaster |= request.newGameProperties != null && request.newGameProperties.Count > 0;
            }

            if (doUpdateOnMaster)
            {
                this.OnGamePropertiesChanged(request);
            }
        }

        /// <summary>
        /// Tries to add a <see cref="HivePeer"/> to this game instance.
        /// </summary>
        /// <param name="peer">
        /// The peer to add.
        /// </param>
        /// <param name="actorNr">
        /// The actor Nr.
        /// </param>
        /// <param name="actor">
        /// When this method returns this out param contains the <see cref="Actor"/> associated with the <paramref name="peer"/>.
        /// </param>
        /// <param name="errorcode">returns error code if we fail to add actor</param>
        /// <param name="reason">
        /// reason why player can not be added
        /// </param>
        /// <param name="isNewActor">returns true if actor is new</param>
        /// <param name="joinRequest">join request which was sent by client</param>
        /// <returns>
        /// Returns true if no actor exists for the specified peer and a new actor for the peer has been successfully added. 
        ///   The actor parameter is set to the newly created <see cref="Actor"/> instance.
        ///   Returns false if an actor for the specified peer already exists. 
        ///   The actor parameter is set to the existing <see cref="Actor"/> for the specified peer.
        /// </returns>
        protected virtual bool TryAddPeerToGame(HivePeer peer, int actorNr, out Actor actor, 
            out bool isNewActor, out ErrorCode errorcode, out string reason, JoinGameRequest joinRequest)
        {
            return this.ActorsManager.TryAddPeerToGame(this, peer, actorNr, out actor, out isNewActor, out errorcode, out reason, joinRequest);
        }

        /// <summary>
        ///   Helper method of <see cref = "HandleRaiseEventOperation" />.
        ///   Stores an event for new actors.
        /// </summary>
        /// <param name = "actor">
        ///   The actor.
        /// </param>
        /// <param name = "raiseEventRequest">
        ///   The raise event request.
        /// </param>
        /// <param name="msg">
        ///   Contains an error message if the method returns false.
        /// </param>
        /// <returns>
        ///   True if <see cref = "RaiseEventRequest.Cache" /> is valid.
        /// </returns>
        private bool UpdateEventCache(Actor actor, RaiseEventRequest raiseEventRequest, out string msg)
        {
            return this.UpdateEventCache(actor.ActorNr, raiseEventRequest.EvCode, raiseEventRequest.Data, raiseEventRequest.Cache, out msg);
        }

        protected bool UpdateEventCache(int actorNr, byte eventCode, object data, byte cacheOp, out string msg)
        {
            switch (cacheOp)
            {
                case (byte)CacheOperation.DoNotCache:
                    msg = string.Empty;
                    return true;

                case (byte)CacheOperation.AddToRoomCache:
                    return UpdateRoomEventCache(actorNr, eventCode, data, out msg);

                case (byte)CacheOperation.AddToRoomCacheGlobal:
                    return UpdateRoomEventCache(0, eventCode, data, out msg);
            }

            return this.UpdateEventCache(actorNr, eventCode, data, (CacheOperation)cacheOp, out msg);
        }

        private bool UpdateRoomEventCache(int actorNr, byte eventCode, object data, out string msg)
        {
            var customEvent = new CustomEvent(actorNr, eventCode, data);
            if (this.EventCache.AddEventToCurrentSlice(customEvent, out msg))
            {
                if (this.EventCache.IsTotalLimitExceeded)
                {
                    this.EventCache.DiscardCache();
                }
                return true;
            }
            return false;
        }

        //Probably Obsolete, we don't know if developers use it.
        private bool UpdateEventCache(int actorNr, byte evCode, object data, CacheOperation cacheOp, out string msg)
        {
            msg = string.Empty;
            // cache operations for the actor event cache currently only working with hashtable data
            Hashtable eventData;
            if (data == null || data is Hashtable)
            {
                eventData = (Hashtable)data;
            }
            else
            {
                msg = $"Cache operation '{cacheOp}' requires a Hashtable as event data.";
                return false;
            }

            switch (cacheOp)
            {
                case CacheOperation.MergeCache:
                {
                    if (this.ActorEventCache.MergeEvent(actorNr, evCode, eventData, out msg))
                    {
                        if (this.ActorEventCache.IsTotalLimitExceeded)
                        {
                            this.ActorEventCache.Discard();
                        }
                        return true;
                    }
                    return false;
                }
                case CacheOperation.RemoveCache:
                    this.ActorEventCache.RemoveEvent(actorNr, evCode);
                    return true;

                case CacheOperation.ReplaceCache:
                    this.ActorEventCache.ReplaceEvent(actorNr, evCode, eventData);
                    return true;

                default:
                    msg = $"Unknown cache operation '{cacheOp}'.";
                    return false;
            }
        }

        private bool UpdateCacheSlice(HivePeer peer, byte op, int? sliceIndex, ref string message)
        {
            // get the actor who send the operation request
            var actor = this.GetActorByPeer(peer);
            if (actor == null)
            {
                return false;
            }

            return this.UpdateCacheSlice((CacheOperation)op, actor.ActorNr, sliceIndex, out message);
        }

        protected bool UpdateCacheSlice(CacheOperation op, int actorNr, int? sliceIndex, out string message)
        {
            message = string.Empty;
            try
            {
                switch (op)
                {
                    case CacheOperation.SliceIncreaseIndex:
                        {
                            this.EventCache.Slice++;
                            // notify "other" actors of change
                            var sliceChangedEvent = new CacheSliceChanged(actorNr) { Slice = this.EventCache.Slice };
                            this.PublishEvent(sliceChangedEvent, this.ActiveActors, new SendParameters());
                            return true;
                        }
                    case CacheOperation.SliceSetIndex:
                        {
                            if (sliceIndex == null)
                            {
                                message = "SliceSetIndex: Missing parameter CacheSliceIndex.";
                                return false;
                            }


                            if (this.EventCache.Slice != sliceIndex.Value)
                            {
                                this.EventCache.Slice = sliceIndex.Value;

                                var sliceChangedEvent = new CacheSliceChanged(actorNr) { Slice = this.EventCache.Slice };
                                this.PublishEvent(sliceChangedEvent, this.ActiveActors, new SendParameters());
                            }
                            return true;
                        }
                    case CacheOperation.SlicePurgeIndex:
                        {
                            if (sliceIndex == null)
                            {
                                message = "SlicePurgeIndex: Missing parameter CacheSliceIndex.";
                                return false;
                            }

                            if (this.EventCache.Slice != sliceIndex.Value)
                            {
                                this.EventCache.RemoveSlice(sliceIndex.Value);
                                return true;
                            }

                            message = $"Purging of current slice={(int) sliceIndex} not allowed.";
                            return false;
                        }
                    case CacheOperation.SlicePurgeUpToIndex:
                        {
                            if (sliceIndex == null)
                            {
                                message = "SlicePurgeUpToIndex: Missing parameter CacheSliceIndex.";
                                return false;
                            }

                            if (this.EventCache.Slice > sliceIndex.Value)
                            {
                                this.EventCache.RemoveUpToSlice(sliceIndex.Value);
                                return true;
                            }

                            message = $"Purging uo to current slice={(int) sliceIndex} not allowed.";
                            return false;
                        }
                }

                message = $"Unknown cache operation={op}.";

            }
            catch (EventCacheException e)
            {
                message = e.Message;
            }
            return false;
        }

        private bool ValidateGame(HivePeer peer, JoinGameRequest joinGameRequest, SendParameters sendParameters)
        {
            // check if the game is open
            if (this.IsOpen == false)
            {
                var errorMsg = this.CacheDiscarded ? HiveErrorMessages.GameClosedCacheDiscarded : HiveErrorMessages.GameClosed;
                this.SendErrorResponse(peer, joinGameRequest.OperationCode, ErrorCode.GameClosed, 
                    errorMsg, sendParameters); 

                joinGameRequest.OnJoinFailed(ErrorCode.GameClosed, errorMsg);

                if (Log.IsDebugEnabled)
                {
                    Log.DebugFormat("ValidateGame: Game '{0}' userId '{1}' failed to join. msg:{2} -- peer: {3}",
                        this.Name, peer.UserId, errorMsg, peer);
                }
                return false;
            }

            var am = this.ActorsManager;
            var isGameFull = am.ActiveActorsCount + am.InactiveActorsCount + am.YetExpectedUsersCount >= this.MaxPlayers;
            
            // check if the maximum number of players has already been reached
            if (this.MaxPlayers > 0 && isGameFull && !this.ActorsManager.IsExpectedUser(peer.UserId))
            {
                this.OnGameFull(peer, joinGameRequest, sendParameters);
                return false;
            }

            return true;
        }

        protected virtual void OnGameFull(HivePeer peer, JoinGameRequest joinGameRequest, SendParameters sendParameters)
        {
            this.SendErrorResponse(peer, joinGameRequest.OperationCode, ErrorCode.GameFull,
                HiveErrorMessages.GameFull, sendParameters);

            joinGameRequest.OnJoinFailed(ErrorCode.GameFull, HiveErrorMessages.GameFull);

            if (Log.IsDebugEnabled)
            {
                Log.DebugFormat("ValidateGame: Game '{0}' userId '{1}' failed to join. msg:{2} -- peer:{3}",
                    this.Name, peer.UserId, HiveErrorMessages.GameFull, peer);
            }

        }

        private bool ConvertParamsAndValidateGame(HivePeer peer, JoinGameRequest joinRequest, SendParameters sendParameters)
        {
            // ValidateGame checks isOpen and max players 
            // and does not apply to rejoins
            if (this.CheckBeforeJoinThisIsNewCreatedRoom(joinRequest))
            {
                return true;
            }

            if (joinRequest.IsRejoining)
            {
                string errorMsg = string.Empty;
                ErrorCode errorcode;

                if (this.CacheDiscarded)
                {
                    errorMsg = HiveErrorMessages.RejoiningBlockedCacheExceeded;
                    this.SendErrorResponse(peer, joinRequest.OperationCode, ErrorCode.EventCacheExceeded, errorMsg, sendParameters);

                    joinRequest.OnJoinFailed(ErrorCode.GameClosed, errorMsg);

                    if (Log.IsDebugEnabled)
                    {
                        Log.DebugFormat("ConvertParamsAndValidateGame: Game '{0}' userId '{1}' failed to join. msg:{2} -- peer: {3}",
                            this.Name, peer.UserId, errorMsg, peer);
                    }
                    return false;
                }

                if (this.PlayerTTL == 0 && joinRequest.JoinMode == JoinModes.RejoinOnly)
                {
                    errorcode = ErrorCode.OperationDenied;
                    errorMsg = HiveErrorMessages.CanNotRejoinGameDoesNotSupportRejoin;

                    this.SendErrorResponse(peer, joinRequest.OperationCode, errorcode, errorMsg, sendParameters);

                    joinRequest.OnJoinFailed(errorcode, errorMsg);

                    if (Log.IsDebugEnabled)
                    {
                        Log.DebugFormat("ConvertParamsAndValidateGame: Game '{0}' userId '{1}' failed to join. msg:'{2}' (JoinMode={3}) -- peer:{4}",
                            this.Name, peer.UserId, errorMsg, joinRequest.JoinMode, peer);
                    }
                    return false;
                }

                Actor actor;
                if (this.CheckUserOnJoin)
                {
                    actor = this.ActorsManager.GetActorByUserId(peer.UserId);
                }
                else
                {
                    actor = this.ActorsManager.GetActorByNumber(joinRequest.ActorNr);
                }

                if (joinRequest.JoinMode == JoinModes.RejoinOnly)
                {
                    if (actor != null)
                    {
                        return true;
                    }

                    if (this.CheckUserOnJoin)
                    {
                        errorcode = ErrorCode.JoinFailedWithRejoinerNotFound;
                        errorMsg = HiveErrorMessages.UserNotFound;
                    }
                    else
                    {
                        errorcode = ErrorCode.JoinFailedWithRejoinerNotFound;
                        errorMsg = string.Format(HiveErrorMessages.ActorNotFound, joinRequest.ActorNr);
                    }

                    this.SendErrorResponse(peer, joinRequest.OperationCode, errorcode, errorMsg, sendParameters);

                    joinRequest.OnJoinFailed(errorcode, errorMsg);

                    if (Log.IsDebugEnabled)
                    {
                        Log.DebugFormat("ConvertParamsAndValidateGame: Game '{0}' userId '{1}' failed to join. msg:'{2}' (JoinMode={3}) -- peer:{4}",
                            this.Name, peer.UserId, errorMsg, joinRequest.JoinMode, peer);
                    }

                    return false;
                }

                if (joinRequest.JoinMode == JoinModes.RejoinOrJoin)
                {
                    if (actor != null)
                    {
                        if (this.PlayerTTL != 0)
                        {
                            return true;
                        }

                        errorMsg = HiveErrorMessages.CanNotRejoinGameDoesNotSupportRejoin;
                    }

                    //TBD - I suggest being even stricter if the room is configured with expected users - we don't support JoinMode 2!
                    if (this.ActorsManager.IsExpectedUser(peer.UserId))
                    {
                        errorMsg = HiveErrorMessages.CanNotUseRejoinOrJoinIfPlayerExpected;
                    }

                    if (!string.IsNullOrEmpty(errorMsg))
                    {
                        this.SendErrorResponse(peer, joinRequest.OperationCode, ErrorCode.OperationDenied, errorMsg, sendParameters);

                        joinRequest.OnJoinFailed(ErrorCode.OperationDenied, errorMsg);

                        if (Log.IsDebugEnabled)
                        {
                            Log.DebugFormat("Game '{0}' userId '{1}' failed to join. msg:{2} (JoinMode={3}) -- peer:{4}",
                                this.Name, peer.UserId, errorMsg, joinRequest.JoinMode, peer);
                        }

                        return false;
                    }
                }
            }

            return this.ValidateGame(peer, joinRequest, sendParameters);
        }

        private void ForceGameToClose()
        {
            if (Log.IsDebugEnabled)
            {
                Log.DebugFormat("Forcing game to close.GameId={0}", this.Name);
            }

            SetProperties(0, new Hashtable {{GameParameters.IsOpen, false}, {GameParameters.IsVisible, false}}, null, true);

            if (Log.IsDebugEnabled)
            {
                Log.DebugFormat("Game closed. GameId={0}", this.Name);
            }
        }

        private void BroadcastCachedEventsLimitExceededErrorInfoEvent(bool roomWasClosed)
        {
            var errorMsg = roomWasClosed ? HiveErrorMessages.RoomClosedCachedEventsLimitExceeded : HiveErrorMessages.CachedEventsLimitExceeded;
            this.PublishErrorInfoEvent(errorMsg, this.ActiveActors, new SendParameters());
        }

#endregion
    }
}