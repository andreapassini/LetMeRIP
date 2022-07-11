// --------------------------------------------------------------------------------------------------------------------
// <copyright file="LitePeer.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Inheritance class of <see cref="PeerBase" />.
//   The LitePeer dispatches incoming <see cref="OperationRequest" />s at <see cref="OnOperationRequest">OnOperationRequest</see>.
//   When joining a <see cref="Room" /> a <see cref="Caching.RoomReference" /> is stored in the <see cref="RoomReference" /> property.
//   An <see cref="IFiber" /> guarantees that all outgoing messages (events/operations) are sent one after the other.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using ExitGames.Concurrency.Fibers;
using ExitGames.Logging;
using Photon.Common;
using Photon.Common.Authentication;
using Photon.Hive.Caching;
using Photon.Hive.Common;
using Photon.Hive.Messages;
using Photon.Hive.Operations;
using Photon.Hive.Plugin;
using Photon.Hive.WebRpc;
using Photon.SocketServer;
using Photon.SocketServer.Diagnostics;
using Photon.SocketServer.Rpc;
using Photon.SocketServer.Rpc.Protocols;
using ErrorCodes = Photon.SocketServer.ErrorCodes;
using SendParameters = Photon.SocketServer.SendParameters;

namespace Photon.Hive
{
    /// <summary>
    ///   Inheritance class of <see cref = "PeerBase" />.  
    ///   The LitePeer dispatches incoming <see cref = "OperationRequest" />s at <see cref = "OnOperationRequest">OnOperationRequest</see>.
    ///   When joining a <see cref = "Room" /> a <see cref = "Caching.RoomReference" /> is stored in the <see cref = "RoomReference" /> property.
    ///   An <see cref = "IFiber" /> guarantees that all outgoing messages (events/operations) are sent one after the other.
    /// </summary>
    public class HivePeer : ClientPeer
    {
        #region Constants and Fields

        /// <summary>
        ///   An <see cref = "ILogger" /> instance used to log messages to the logging framework.
        /// </summary>
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();
        private static readonly LogCountGuard webRpcLimitCountGuard = new LogCountGuard(new TimeSpan(0, 0, 10));

        private readonly LogCountGuard uniqKeysValidationLogGuard = new LogCountGuard(new TimeSpan(0, 0, 10));
        private static readonly LogCountGuard validateOpCountGuard = new LogCountGuard(new TimeSpan(0, 0, 10));
        private static readonly LogCountGuard hpDisconnectLogGuard = new LogCountGuard(new TimeSpan(0, 0, 10));

        private readonly TimeIntervalCounterLite httpForwardedRequests = new TimeIntervalCounterLite(new TimeSpan(0, 0, 1));

        /// <summary>
        /// we use it to create fake LeaveRequest if user's one  is broken
        /// </summary>
        private static readonly OperationRequest EmptyLeaveRequest = new OperationRequest((byte)OperationCode.Leave)
        {
            Parameters = new Dictionary<byte, object>(),
        };

        private int joinStage;
        private readonly HashSet<object> peerUniqPropertyKeys = new HashSet<object>();

        #endregion

        #region Constructors and Destructors

        /// <summary>
        ///   Initializes a new instance of the <see cref = "HivePeer" /> class.
        /// </summary>
        public HivePeer(InitRequest request)
            : base(request)
        {
            this.UserId = string.Empty;
            // we set here roomCreationTime to handle Auth request in a same way as others,
            // but we know that its ts is from peer creation
        }

        #endregion

        public static class JoinStages
        {
            public const byte Connected = 0;
            public const byte CreatingOrLoadingGame = 1;
            public const byte ConvertingParams = 2;
            public const byte CheckingCacheSlice = 3;
            public const byte AddingActor = 4;
            public const byte CheckAfterJoinParams = 5;
            public const byte ApplyActorProperties = 6;
            public const byte BeforeJoinComplete = 7;
            public const byte GettingUserResponse = 8;
            public const byte SendingUserResponse = 9;
            public const byte PublishingEvents = 10;
            public const byte EventsPublished = 11;
            public const byte Complete = 12;
        }

        #region Properties

        /// <summary>
        ///   Gets or sets a <see cref = "Caching.RoomReference" /> when joining a <see cref = "Room" />.
        /// </summary>
        protected RoomReference RoomReference { get; private set; }

        public Actor Actor { get; set; }
        public string UserId { get; protected set; }

        public WebRpcHandler WebRpcHandler { get; set; }

        public Dictionary<string, object> AuthCookie { get; protected set; }

        public AuthenticationToken AuthToken { get; protected set; }

        protected int HttpRpcCallsLimit { get; set; }

        /// <summary>
        /// The count of checks which were performed on this peer while it is in invalid state.
        /// </summary>
        public int CheckCount { get; set; }

        public int JoinStage => this.joinStage;

        protected int LimitMaxUniqPropertyKeysPerPeer { get; set; } = int.MaxValue;

        public int LimitMaxPropertiesSizePerGame { get; set; } = int.MaxValue;

        #endregion

        #region Public Methods

        /// <summary>
        ///   Checks if a operation is valid. If the operation is not valid
        ///   an operation response containing a descriptive error message
        ///   will be sent to the peer.
        /// </summary>
        /// <param name = "operation">
        ///   The operation.
        /// </param>
        /// <param name = "sendParameters">
        ///   The send Parameters.
        /// </param>
        /// <returns>
        ///   true if the operation is valid; otherwise false.
        /// </returns>
        public bool ValidateOperation(Operation operation, SendParameters sendParameters)
        {
            if (operation.IsValid)
            {
                return true;
            }

            var errorMessage = operation.GetErrorMessage();
            this.SendOperationResponse(new OperationResponse
                                            {
                                                OperationCode = operation.OperationRequest.OperationCode,
                                                ReturnCode = (short)ErrorCode.OperationInvalid,
                                                DebugMessage = errorMessage
                                            }, 
                                            sendParameters);
            if (Loggers.InvalidOpLogger.IsInfoEnabled)
            {
                Loggers.InvalidOpLogger.Info(validateOpCountGuard, $"Invalid operation: code_{operation.Code}. reason: '{errorMessage}', p:{this}");
            }
            return false;
        }

        /// <summary>
        ///   Checks if the the state of peer is set to a reference of a room.
        ///   If a room reference is present the peer will be removed from the related room and the reference will be disposed. 
        ///   Disposing the reference allows the associated room factory to remove the room instance if no more references to the room exists.
        /// </summary>
        public void RemovePeerFromCurrentRoom(int reason, string detail)
        {
            this.RequestFiber.Enqueue(() => this.RemovePeerFromCurrentRoomInternal(reason, detail));
        }

        public void ReleaseRoomReference()
        {
            this.RequestFiber.Enqueue(this.ReleaseRoomReferenceInternal);
        }

        public void OnJoinFailed(ErrorCode result, string details)
        {
            this.RequestFiber.Enqueue(() => this.OnJoinFailedInternal(result, details));
        }

        public virtual bool IsThisSameSession(HivePeer peer)
        {
            return false;
        }

        internal void SetJoinStage(byte stage)
        {
            Interlocked.Exchange(ref this.joinStage, stage);
        }

        public virtual void UpdateSecure(string key, object value)
        {
            //always updated - keep this until behaviour is clarified
            if (this.AuthCookie == null)
            {
                this.AuthCookie = new Dictionary<string, object>();
            }
            this.AuthCookie[key] = value;

            //we only update existing values
//            if (this.AuthCookie != null && this.AuthCookie.ContainsKey(key))
//            {
//                this.AuthCookie[key] = value;
//            }
        }

        #endregion

        #region Methods

        protected virtual void HandleCreateGameOperation(OperationRequest operationRequest, SendParameters sendParameters)
        {
            if (this.JoinStage != JoinStages.Connected || this.RoomReference != null)
            {
                this.OnWrongOperationStage(operationRequest, sendParameters);
                return;
            }

            // On "LoadBalancing" game servers games must by created first by the game creator to ensure that no other joining peer 
            // reaches the game server before the game is created.
            // we use JoinGameRequest to make sure that GameId is set
            var createRequest = new JoinGameRequest(this.Protocol, operationRequest, this.UserId, this.LimitMaxPropertiesSizePerGame, this.PrivateInboundController.OnlyLogLimitsViolations);
            if (this.ValidateOperation(createRequest, sendParameters) == false)
            {
                return;
            }

            if (!this.MayJoinOrCreateGame(createRequest, out var returnCode, out var debugMsg))
            {
                this.SendOperationResponseAndDisconnect(new OperationResponse
                {
                    OperationCode = operationRequest.OperationCode,
                    ReturnCode = returnCode,
                    DebugMessage = debugMsg,
                }, sendParameters);

                if (Loggers.DisconnectLogger.IsInfoEnabled)
                {
                    Loggers.DisconnectLogger.Info(hpDisconnectLogGuard, $"Disconnect peer may not create game. Reason_{returnCode}, Msg:{debugMsg}, p:{this}");
                }
                return;
            }


            // try to create the game
            if (this.TryCreateRoom(createRequest.GameId, out var gameReference) == false)
            {
                var response = new OperationResponse
                {
                    OperationCode = (byte)OperationCode.CreateGame,
                    ReturnCode = (short)ErrorCode.GameIdAlreadyExists,
                    DebugMessage = HiveErrorMessages.GameAlreadyExist,
                };

                this.SendOperationResponse(response, sendParameters);
                return;
            }

            // save the game reference in the peers state
            this.RoomReference = gameReference;

            // finally enqueue the operation into game queue
            gameReference.Room.EnqueueOperation(this, createRequest, sendParameters);
        }

        /// <summary>
        ///   Enqueues RaiseEvent operation requests in the peers current game.
        /// </summary>
        /// <param name = "operationRequest">
        ///   The operation request.
        /// </param>
        /// <param name = "sendParameters">
        ///   The send Parameters.
        /// </param>
        /// <remarks>
        ///   The current for a peer is stored in the peers state property. 
        ///   Using the <see cref = "Room.EnqueueOperation" /> method ensures that all operation request dispatch logic has thread safe access to all room instance members since they are processed in a serial order. 
        ///   <para>
        ///     Inheritors can use this method to enqueue there custom game operation to the peers current game.
        ///   </para>
        /// </remarks>
        protected virtual void HandleRaiseEventOperation(OperationRequest operationRequest, SendParameters sendParameters)
        {
            if (this.joinStage < JoinStages.PublishingEvents)
            {
                this.OnWrongOperationStage(operationRequest, sendParameters);
                return;
            }

            var raiseEventOperation = new RaiseEventRequest(this.Protocol, operationRequest);

            if (this.ValidateOperation(raiseEventOperation, sendParameters) == false)
            {
                return;
            }

            // enqueue operation into game queue. 
            // the operation request will be processed in the games ExecuteOperation method.
            if (this.RoomReference != null)
            {
                this.RoomReference.Room.EnqueueOperation(this, raiseEventOperation, sendParameters);
                return;
            }

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Received RaiseEvent operation on peer without a game: p:{0}", this);
            }
        }

        /// <summary>
        ///   Enqueues SetProperties operation requests in the peers current game.
        /// </summary>
        /// <param name = "operationRequest">
        ///   The operation request.
        /// </param>
        /// <param name = "sendParameters">
        ///   The send Parameters.
        /// </param>
        /// <remarks>
        ///   The current for a peer is stored in the peers state property. 
        ///   Using the <see cref = "Room.EnqueueOperation" /> method ensures that all operation request dispatch logic has thread safe access to all room instance members since they are processed in a serial order. 
        ///   <para>
        ///     Inheritors can use this method to enqueue there custom game operation to the peers current game.
        ///   </para>
        /// </remarks>
        protected virtual void HandleSetPropertiesOperation(OperationRequest operationRequest, SendParameters sendParameters)
        {
            if (this.joinStage < JoinStages.PublishingEvents)
            {
                this.OnWrongOperationStage(operationRequest, sendParameters);
                return;
            }

            var setPropertiesOperation = new SetPropertiesRequest(this.Protocol, operationRequest);
            if (this.ValidateOperation(setPropertiesOperation, sendParameters) == false)
            {
                return;
            }

            if (!this.ValidateUniqKeys(setPropertiesOperation.Properties))
            {
                log.Warn(this.uniqKeysValidationLogGuard, "Uniq Keys Validation: peer set " +
                                                     $"too many uniq properties. limit={this.LimitMaxUniqPropertyKeysPerPeer}. already set={this.peerUniqPropertyKeys.Count}. " +
                                                     $"in request: {setPropertiesOperation.Properties.Count}. p={this}");

                if (!this.PrivateInboundController.OnlyLogLimitsViolations)
                {
                    this.SendOperationResponseAndDisconnect(new OperationResponse
                    {
                        OperationCode = operationRequest.OperationCode,
                        ReturnCode = (short)ErrorCode.OperationLimitReached,
                        DebugMessage = ErrorMessages.TooManyPropertiesSetByPeer
                    }, sendParameters);

                    if (Loggers.DisconnectLogger.IsInfoEnabled)
                    {
                        Loggers.DisconnectLogger.Info(hpDisconnectLogGuard, $"Disconnect uniq properties limit. Reason_{ErrorCode.OperationLimitReached}, Msg:{ErrorMessages.TooManyPropertiesSetByPeer}, p:{this}");
                    }
                    return;
                }
            }
            // enqueue operation into game queue. 
            // the operation request will be processed in the games ExecuteOperation method.
            if (this.RoomReference != null)
            {
                this.RoomReference.Room.EnqueueOperation(this, setPropertiesOperation, sendParameters);
                return;
            }

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Received SetProperties operation on peer without a game: p:{0}", this);
            }
        }

        /// <summary>
        ///   Enqueues GetProperties operation requests in the peers current game.
        /// </summary>
        /// <param name = "operationRequest">
        ///   The operation request.
        /// </param>
        /// <param name = "sendParameters">
        ///   The send Parameters.
        /// </param>
        /// <remarks>
        ///   The current for a peer is stored in the peers state property. 
        ///   Using the <see cref = "Room.EnqueueOperation" /> method ensures that all operation request dispatch logic has thread safe access to all room instance members since they are processed in a serial order. 
        ///   <para>
        ///     Inheritors can use this method to enqueue there custom game operation to the peers current game.
        ///   </para>
        /// </remarks>
        private void HandleGetPropertiesOperation(OperationRequest operationRequest, SendParameters sendParameters)
        {
            if (this.joinStage < JoinStages.PublishingEvents)
            {
                this.OnWrongOperationStage(operationRequest, sendParameters);
                return;
            }

            var getPropertiesOperation = new GetPropertiesRequest(this.Protocol, operationRequest);
            if (this.ValidateOperation(getPropertiesOperation, sendParameters) == false)
            {
                return;
            }

            // enqueue operation into game queue. 
            // the operation request will be processed in the games ExecuteOperation method.
            if (this.RoomReference != null)
            {
                this.RoomReference.Room.EnqueueOperation(this, getPropertiesOperation, sendParameters);
                return;
            }

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Received GetProperties operation on peer without a game: peerId={0}", this.ConnectionId);
            }
        }

        /// <summary>
        ///   Enqueues game related operation requests in the peers current game.
        /// </summary>
        /// <param name = "operationRequest">
        ///   The operation request.
        /// </param>
        /// <param name = "sendParameters">
        ///   The send Parameters.
        /// </param>
        /// <remarks>
        ///   The current for a peer is stored in the peers state property. 
        ///   Using the <see cref = "Room.EnqueueOperation" /> method ensures that all operation request dispatch logic has thread safe access to all room instance members since they are processed in a serial order. 
        ///   <para>
        ///     Inheritors can use this method to enqueue there custom game operation to the peers current game.
        ///   </para>
        /// </remarks>
        private void HandleChangeGroupsOperation(OperationRequest operationRequest, SendParameters sendParameters)
        {
            if (this.joinStage < JoinStages.PublishingEvents)
            {
                this.OnWrongOperationStage(operationRequest, sendParameters);
                return;
            }

            var changeGroupsOperation = new ChangeGroups(this.Protocol, operationRequest);
            if (this.ValidateOperation(changeGroupsOperation, sendParameters) == false)
            {
                return;
            }

            // enqueue operation into game queue. 
            // the operation request will be processed in the games ExecuteOperation method.
            if (this.RoomReference != null)
            {
                this.RoomReference.Room.EnqueueOperation(this, changeGroupsOperation, sendParameters);
                return;
            }

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Received ChangeGroups operation on peer without a game: p:{0}", this);
            }
        }

        /// <summary>
        ///   Handles the <see cref = "JoinGameRequest" /> to enter a <see cref = "HiveGame" />.
        ///   This method removes the peer from any previously joined room, finds the room intended for join
        ///   and enqueues the operation for it to handle.
        /// </summary>
        /// <param name = "operationRequest">
        ///   The operation request to handle.
        /// </param>
        /// <param name = "sendParameters">
        ///   The send Parameters.
        /// </param>
        protected virtual void HandleJoinGameOperation(OperationRequest operationRequest, SendParameters sendParameters)
        {
            if (this.JoinStage != JoinStages.Connected || this.RoomReference != null)
            {
                this.OnWrongOperationStage(operationRequest, sendParameters);
                return;
            }

            // create join operation
            var joinRequest = new JoinGameRequest(this.Protocol, operationRequest, this.UserId, this.LimitMaxPropertiesSizePerGame);
            if (this.ValidateOperation(joinRequest, sendParameters) == false)
            {
                return;
            }

            if (!this.MayJoinOrCreateGame(joinRequest, out var returnCode, out var debugMsg))
            {
                this.SendOperationResponseAndDisconnect(new OperationResponse
                {
                    OperationCode = operationRequest.OperationCode,
                    ReturnCode = returnCode,
                    DebugMessage = debugMsg,
                }, sendParameters);

                if (Loggers.DisconnectLogger.IsInfoEnabled)
                {
                    Loggers.DisconnectLogger.Info(hpDisconnectLogGuard, $"Disconnect peer may not join game. Reason_{returnCode}, Msg:{debugMsg}, p:{this}");
                }

                return;
            }

            // try to get the game reference from the game cache 
            RoomReference gameReference;
            var pluginTraits = this.GetPluginTraits();

            if (joinRequest.JoinMode > 0 || pluginTraits.AllowAsyncJoin)
            {
                gameReference = this.GetOrCreateRoom(joinRequest.GameId);
            }
            else
            {
                if (this.TryGetRoomReference(joinRequest.GameId, out gameReference) == false)
                {
                    this.HandleRoomNotFound(sendParameters, joinRequest);
                    return;
                }
            }

            // save the game reference in the peers state
            this.RoomReference = gameReference;

            // finally enqueue the operation into game queue
            gameReference.Room.EnqueueOperation(this, joinRequest, sendParameters);
        }

        protected virtual bool MayJoinOrCreateGame(JoinGameRequest joinRequest, out short errorCode, out string errorMsg)
        {
            errorCode = (short)ErrorCode.Ok;
            errorMsg = string.Empty;

            return true;
        }

        protected virtual PluginTraits GetPluginTraits()
        {
            return HiveGameCache.Instance.PluginManager.PluginTraits;
        }

        /// <summary>
        ///   Handles the <see cref = "LeaveRequest" /> to leave a <see cref = "HiveGame" />.
        /// </summary>
        /// <param name = "operationRequest">
        ///   The operation request to handle.
        /// </param>
        /// <param name = "sendParameters">
        ///   The send Parameters.
        /// </param>
        protected virtual void HandleLeaveOperation(OperationRequest operationRequest, SendParameters sendParameters)
        {
            // check if the peer have a reference to game 
            if (this.RoomReference == null)
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("Received leave operation on peer without a game: peerId={0}", this.ConnectionId);
                }

                return;
            }

            var leaveOperation = new LeaveRequest(this.Protocol, operationRequest);
            if (this.ValidateOperation(leaveOperation, sendParameters) == false)
            {
                if (log.IsDebugEnabled)
                {
                    log.Debug($"Wrong leave request. Use default one. errorMsg:{leaveOperation.GetErrorMessage()}");
                }
                // we create default request to remove actor for sure
                leaveOperation = new LeaveRequest(this.Protocol, EmptyLeaveRequest);
            }

            var rr = this.RoomReference;
            this.RoomReference = null;
            // enqueue the leave operation into game queue. 
            rr.Room.EnqueueOperation(this, leaveOperation, sendParameters);

            DisposeRoomReference(rr);
            // we schedule disconnect right here to prevent from spam that happens after Leave
            this.ScheduleDisconnect(LeaveReason.LeaveRequest, DefaultDisconnectInterval);
        }

        /// <summary>
        ///   Handles a ping operation.
        /// </summary>
        /// <param name = "operationRequest">
        ///   The operation request to handle.
        /// </param>
        /// <param name = "sendParameters">
        ///   The send Parameters.
        /// </param>
        protected virtual void HandlePingOperation(OperationRequest operationRequest, SendParameters sendParameters)
        {
            this.SendOperationResponse(new OperationResponse { OperationCode = operationRequest.OperationCode }, sendParameters);
        }

        /// <summary>
        /// Handles WebRpc operation
        /// </summary>
        /// <param name = "request">
        ///   The operation request to handle.
        /// </param>
        /// <param name = "sendParameters">
        ///   The send Parameters.
        /// </param>
        protected virtual void HandleRpcOperation(OperationRequest request, SendParameters sendParameters)
        {
            if (this.WebRpcHandler != null)
            {
                var callsCount = this.httpForwardedRequests.Increment(1);
                if (callsCount > this.HttpRpcCallsLimit)
                {
                    var resp = new OperationResponse
                    {
                        OperationCode = request.OperationCode,
                        ReturnCode = (short)ErrorCode.HttpLimitReached,
                        DebugMessage = string.Format(HiveErrorMessages.HttpForwardedOperationsLimitReached, this.HttpRpcCallsLimit)
                    };

                    if (this.PrivateInboundController.OnlyLogLimitsViolations)
                    {
                        this.SendOperationResponse(resp, sendParameters);
                    }
                    else
                    {
                        this.SendOperationResponseAndDisconnect(resp, sendParameters);
                    }

                    log.Warn(webRpcLimitCountGuard, $"Limit exceeded Too many web rpc requests. limit: {this.HttpRpcCallsLimit} callsCount_{callsCount}");
                    return;
                }

                this.WebRpcHandler.HandleCall(this, this.UserId, request, this.AuthCookie, sendParameters);
                return;
            }

            this.SendOperationResponse(new OperationResponse
            {
                OperationCode = request.OperationCode,
                ReturnCode = (short)ErrorCode.OperationInvalid,
                DebugMessage = HiveErrorMessages.WebRpcIsNotEnabled,
            }, sendParameters);
        }


        /// <summary>
        ///   Called when client disconnects.
        ///   Ensures that disconnected players leave the game <see cref = "Room" />.
        ///   The player is not removed immediately but a message is sent to the room. This avoids
        ///   threading issues by making sure the player remove is not done concurrently with operations.
        /// </summary>
        protected override void OnDisconnect(int reasonCode, string reasonDetail)
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("OnDisconnect: conId={0}, reason={1}, reasonDetail={2}", this.ConnectionId, reasonCode, reasonDetail);
            }

            this.RemovePeerFromCurrentRoomInternal(reasonCode, reasonDetail);
        }

        /// <summary>
        ///   Called when the client sends an <see cref = "OperationRequest" />.
        /// </summary>
        /// <param name = "operationRequest">
        ///   The operation request.
        /// </param>
        /// <param name = "sendParameters">
        ///   The send Parameters.
        /// </param>
        protected override void OnOperationRequest(OperationRequest operationRequest, SendParameters sendParameters)
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("OnOperationRequest. Code={0}", operationRequest.OperationCode);
            }

            var opCode = (OperationCode) operationRequest.OperationCode;
            switch (opCode)
            {
                case OperationCode.Authenticate:
                    return;

                case OperationCode.CreateGame:
                    this.HandleCreateGameOperation(operationRequest, sendParameters);
                    return;

                case OperationCode.JoinGame:
                    this.HandleJoinGameOperation(operationRequest, sendParameters);
                    return;

                case OperationCode.Ping:
                    this.HandlePingOperation(operationRequest, sendParameters);
                    return;

                case OperationCode.DebugGame:
                    this.HandleDebugGameOperation(operationRequest, sendParameters);
                    return;

                case OperationCode.Leave:
                    this.HandleLeaveOperation(operationRequest, sendParameters);
                    return;

                case OperationCode.RaiseEvent:
                    this.HandleRaiseEventOperation(operationRequest, sendParameters);
                    return;

                case OperationCode.GetProperties:
                    this.HandleGetPropertiesOperation(operationRequest, sendParameters);
                    return;

                case OperationCode.SetProperties:
                    this.HandleSetPropertiesOperation(operationRequest, sendParameters);
                    return;

                case OperationCode.ChangeGroups:
                    this.HandleChangeGroupsOperation(operationRequest, sendParameters);
                    return;

                case OperationCode.Rpc:
                    this.HandleRpcOperation(operationRequest, sendParameters);
                    return;

                default:
                    this.HandleUnknownOperationCode(operationRequest, sendParameters);
                    return;
            }
        }

        protected void HandleUnknownOperationCode(OperationRequest operationRequest, SendParameters sendParameters)
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Unknown operation code: OpCode={0}", operationRequest.OperationCode);
            }

            this.SendOperationResponse(
                new OperationResponse(operationRequest.OperationCode)
                {
                    ReturnCode = (short)ErrorCode.OperationInvalid,
                    DebugMessage = HiveErrorMessages.UnknownOperationCode
                }, sendParameters);
        }

        protected virtual void HandleDebugGameOperation(OperationRequest operationRequest, SendParameters sendParameters)
        {
        }

        protected virtual RoomReference GetOrCreateRoom(string gameId, params object[] args)
        {
            return HiveGameCache.Instance.GetRoomReference(gameId, this, args);
        }

        protected virtual bool TryCreateRoom(string gameId, out RoomReference roomReference, params object[] args)
        {
            return HiveGameCache.Instance.TryCreateRoom(gameId, this, out roomReference, args);
        }

        protected virtual bool TryGetRoomReference(string gameId, out RoomReference roomReference)
        {
            return HiveGameCache.Instance.TryGetRoomReference(gameId, this, out roomReference);
        }

        protected virtual bool TryGetRoomWithoutReference(string gameId, out Room room)
        {
            return HiveGameCache.Instance.TryGetRoomWithoutReference(gameId, out room);
        }

        protected virtual void OnRoomNotFound(string gameId)
        {
        }

        private void OnJoinFailedInternal(ErrorCode result, string details)
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("OnJoinFailedInternal: {0} - {1}", result, details);
            }

            // if join operation failed -> release the reference to the room
            if (result != ErrorCode.Ok && this.RoomReference != null)
            {
                this.ReleaseRoomReferenceInternal();
            }
        }

        private void ReleaseRoomReferenceInternal()
        {
            var r = this.RoomReference;
            if (DisposeRoomReference(r)) return;

            // finally the peers state is set to null to indicate
            // that the peer is not attached to a room anymore.
            this.RoomReference = null;
        }

        private static bool DisposeRoomReference(RoomReference r)
        {
            if (r == null)
            {
                return true;
            }

            // release the reference to the game
            // the game cache will recycle the game instance if no 
            // more references to the game are left.
            r.Dispose();
            return false;
        }

        private void RemovePeerFromCurrentRoomInternal(int reason, string detail)
        {
            // check if the peer already joined another game
            var r = this.RoomReference;
            if (r == null)
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("RemovePeerFromCurrentRoom: Room Reference is null for p:{0}", this);
                }
                return;
            }

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("RemovePeerFromCurrentRoom: Removing peer from room. p:{0}", this);
            }
            // remove peer from his current game.
            var message = new RoomMessage((byte)GameMessageCodes.RemovePeerFromGame, new object[] { this, reason, detail });
            r.Room.EnqueueMessage(message);

            this.ReleaseRoomReferenceInternal();
        }

        private void HandleRoomNotFound(SendParameters sendParameters, JoinGameRequest joinRequest)
        {
            this.OnRoomNotFound(joinRequest.GameId);

            var response = new OperationResponse
            {
                OperationCode = (byte)OperationCode.JoinGame,
                ReturnCode = (short)ErrorCode.GameIdNotExists,
                DebugMessage = HiveErrorMessages.GameIdDoesNotExist,
            };

            this.SendOperationResponse(response, sendParameters);

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Game '{0}' userId '{1}' failed to join. msg:{2} -- peer:{3}", joinRequest.GameId, this.UserId,
                    HiveErrorMessages.GameIdDoesNotExist, this);
            }
        }

        private void OnWrongOperationStage(OperationRequest operationRequest, SendParameters sendParameters)
        {
            this.SendOperationResponseAndDisconnect(new OperationResponse
            {
                OperationCode = operationRequest.OperationCode,
                ReturnCode = (short)ErrorCode.OperationDenied,
                DebugMessage = HiveErrorMessages.OperationIsNotAllowedOnThisJoinStage,
            }, sendParameters);

            if (Loggers.DisconnectLogger.IsInfoEnabled)
            {
                Loggers.DisconnectLogger.Info(hpDisconnectLogGuard, $"Disconnect wrong operation stage. Reason_{ErrorCode.OperationDenied}, Msg:{HiveErrorMessages.OperationIsNotAllowedOnThisJoinStage}, p:{this}");
            }
        }

        private bool ValidateUniqKeys(Hashtable properties)
        {
            if (properties == null)
            {
                return true;
            }

            var limit = this.LimitMaxUniqPropertyKeysPerPeer;
            if (limit == int.MaxValue)
            {
                return true;
            }

            foreach (var key in properties.Keys)
            {
                this.peerUniqPropertyKeys.Add(key);
            }

            return this.peerUniqPropertyKeys.Count <= limit;
        }


        protected override void OnDeserializationError(byte[] data, RtsMessageType msgType, string debugMessage, 
            short errorCode = ErrorCodes.UnexpectedData, byte code = 0, Exception exception = null)
        {
            // inside base implementation we send disconnect message and schedule disconnect
            base.OnDeserializationError(data, msgType, debugMessage, errorCode, code, exception);

            this.RemovePeerFromCurrentRoomInternal(errorCode, debugMessage);
        }

        protected override void OnSendBufferFull()
        {
            // we schedule disconnect here and will send disconnect message from OnSendBufferEmpty
            // when client will be able to get anything
            this.ScheduleDisconnect(ErrorCodes.SendBufferFull, PeerBase.DefaultDisconnectInterval << 1);
            this.RemovePeerFromCurrentRoomInternal(ErrorCodes.SendBufferFull, "SendBufferFull");

            if (Loggers.DisconnectLogger.IsWarnEnabled)
            {
                Loggers.DisconnectLogger.Info(hpDisconnectLogGuard, $"Disconnect send buffer full, p:{this}");
            }

        }

        protected override void OnSendBufferEmpty()
        {
            this.SendDisconnectMessage(new DisconnectMessage(ErrorCodes.SendBufferFull, "SendBufferFull"), new SendParameters());
            base.OnSendBufferEmpty();

            if (Loggers.DisconnectLogger.IsWarnEnabled)
            {
                Loggers.DisconnectLogger.Info(hpDisconnectLogGuard, $"Disconnect send buffer full from OnSendBufferEmpty, p:{this}");
            }
        }

        #endregion
    }
}