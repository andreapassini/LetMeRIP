// --------------------------------------------------------------------------------------------------------------------
// <copyright file="GameClientPeer.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the GamePeer type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using ExitGames.Concurrency.Fibers;
using ExitGames.Logging;

using Photon.Common.Authentication;
using Photon.Common.LoadBalancer.Common;
using Photon.Hive;
using Photon.Hive.Caching;
using Photon.Hive.Common;
using Photon.Hive.Operations;
using Photon.Hive.Plugin;
using Photon.Hive.WebRpc.Configuration;
using Photon.LoadBalancing.Common;
using Photon.LoadBalancing.Operations;
using Photon.SocketServer;
using Photon.SocketServer.Diagnostics;

using AuthSettings = Photon.Common.Authentication.Configuration.Auth.AuthSettings;
using ErrorCode = Photon.Common.ErrorCode;
using OperationCode = Photon.LoadBalancing.Operations.OperationCode;
using SendParameters = Photon.SocketServer.SendParameters;

namespace Photon.LoadBalancing.GameServer
{

    public class GameClientPeer : HivePeer, IAuthTimeoutCheckerClient
    {
        #region Constants and Fields

        private static readonly ILogger log = LogManager.GetCurrentClassLogger();
        protected static readonly LogCountGuard canNotDecryptToken = new LogCountGuard(new TimeSpan(0, 1, 0));
        private static readonly LogCountGuard secureConnectionLogGuard = new LogCountGuard(new TimeSpan(0, 0, 30));
        private static readonly LogCountGuard tokenValidationLogGuard = new LogCountGuard(new TimeSpan(0, 0, 10));
        private static readonly LogCountGuard gcpDisconnectLogGuard = new LogCountGuard(new TimeSpan(0, 0, 10));

        private readonly GameApplication application;

        private readonly long tokenExpirationTime = TimeSpan.FromSeconds(Settings.Default.AuthTokenExpirationSeconds/3.0).Ticks;

        private readonly bool authOnceUsed;
        protected readonly bool binaryTokenUsed;

        protected int NumberAuthRequests;
        protected int MaxNumberAuthRequests = 3;

        #endregion

        #region Constructors and Destructors

        /// <summary>
        /// this constructor is called only from self-hosted layer
        /// </summary>
        /// <param name="initRequest"></param>
        /// <param name="application"></param>
        public GameClientPeer(InitRequest initRequest, GameApplication application)
            : this(initRequest, application, false)
        {
            application.AppStatsPublisher?.IncrementPeerCount();
        }

        protected GameClientPeer(InitRequest initRequest, GameApplication application, bool derived)
            : base(initRequest)
        {
            this.application = application;
            this.LimitMaxUniqPropertyKeysPerPeer = GameServerSettings.Default.Limits.Inbound.Properties.MaxUniqPropertyKeysPerPeer;
            this.LimitMaxPropertiesSizePerGame = GameServerSettings.Default.Limits.Inbound.Properties.MaxPropertiesSizePerGame;

            this.HttpRpcCallsLimit = WebRpcSettings.Default.HttpCallsLimit;

            var token = initRequest.InitObject as string;
            AuthenticationToken authToken;

            if (!string.IsNullOrEmpty(token))
            {
                authToken = AuthOnInitHandler.DoAuthUsingInitObject(token, this, initRequest,
                    application.TokenCreator, out var errorCode, out var errorMsg);
                if (authToken == null)
                {
                    this.RequestFiber.Enqueue(() => this.SendOperationResponseAndDisconnect(new OperationResponse((byte) OperationCode.AuthOnce)
                    {
                        DebugMessage = errorMsg,
                        ReturnCode = errorCode
                    }, new SendParameters()));
                }
            }
            else if (initRequest.DecryptedAuthToken != null)
            {
                authToken = (AuthenticationToken) initRequest.DecryptedAuthToken;
                this.binaryTokenUsed = true;
            }
            else
            {
                // classic auth
                AuthTimeoutChecker.StartWaitForAuthRequest(this, log, (byte)OperationCode.Authenticate);
                return;
            }

            if (authToken != null)
            {
                this.authOnceUsed = true;
                this.AuthToken = authToken;

                if (!derived)
                {
                    if (!ConnectionRequirementsChecker.Check(this, authToken.ApplicationId, this.authOnceUsed))
                    {
                        log.Warn(secureConnectionLogGuard,
                            $"Client used non secure connection type when it is required. appId:{authToken.ApplicationId}, Connection: {this.NetworkProtocol}. AuthOnce");

                        return;
                    }

                    this.SetupPeer(this.AuthToken);

                    this.RequestFiber.Enqueue(() =>
                    {
                        var responseObject = new AuthenticateResponse { QueuePosition = 0 };
                        this.SendOperationResponse(new OperationResponse((byte)OperationCode.AuthOnce, responseObject),
                            new SendParameters());
                    });
                }
            }
        }

        #endregion

        #region Properties


        public DateTime LastActivity { get; protected set; }

        public byte LastOperation { get; protected set; }

        protected  bool IsAuthenticated { get; set; }

        protected bool AllowDebugGameOperation { get; set; } = CommonSettings.Default.AllowDebugGameOperation;
        #endregion

        #region Public Methods

        public override string ToString()
        {
            var roomName = string.Empty;
            var roomRef = this.RoomReference;
            if (roomRef != null)
            {
                var room = roomRef.Room;
                if (room != null)
                {
                    roomName = room.Name;
                }
            }

            return $"{this.GetType().Name}: " +
                   $"PID: {this.ConnectionId}, " +
                   $"IsConnected: {this.Connected}, " +
                   $"IsDisposed: {this.Disposed}, " +
                   $"Last Activity: Operation {this.LastOperation} at UTC {this.LastActivity:s}, " +
                   $"in Room '{roomName}', " +
                   $"IP {this.RemoteIP}:{this.RemotePort}, " +
                   $"NetworkProtocol {this.NetworkProtocol}, " +
                   $"Protocol {this.Protocol.ProtocolType}, " +
                   $"JoinStage: {this.JoinStage} ";
        }

        public override bool IsThisSameSession(HivePeer peer)
        {
            return this.AuthToken != null && peer.AuthToken != null && this.AuthToken.AreEqual(peer.AuthToken);
        }

        public override void UpdateSecure(string key, object value)
        {
            //always updated - keep this until behaviour is clarified
            if (this.AuthCookie == null)
            {
                this.AuthCookie = new Dictionary<string, object>();
            }
            this.AuthCookie[key] = value;
            this.SendAuthEvent();

            //we only update existing values
//            if (this.AuthCookie != null && this.AuthCookie.ContainsKey(key))
//            {
//                this.AuthCookie[key] = value;
//                this.SendAuthEvent();
//            }
        }

        #endregion

        #region Methods

        protected override void OnRoomNotFound(string gameId)
        {
            this.application.MasterServerConnection.RemoveGameState(gameId, GameRemoveReason.GameRemoveGameNotFound);
        }

        protected override void OnDisconnect(int reasonCode, string reasonDetail)
        {
            AuthTimeoutChecker.StopWaitForAuthRequest(this, log);

            base.OnDisconnect(reasonCode, reasonDetail);

            // this call decrement peers only for self-hosted layer
            this.application.AppStatsPublisher?.DecrementPeerCount();
        }

        protected override void OnOperationRequest(OperationRequest request, SendParameters sendParameters)
        {
            if (log.IsDebugEnabled)
            {
                if (request.OperationCode != (byte)Photon.Hive.Operations.OperationCode.RaiseEvent)
                {
                    log.DebugFormat("OnOperationRequest: conId={0}, opCode={1}", this.ConnectionId, request.OperationCode);
                }
            }

            this.LastActivity = DateTime.UtcNow;
            this.LastOperation = request.OperationCode;

            if (request.OperationCode == (byte) OperationCode.Authenticate)
            {
                if (this.IsAuthenticated)
                {
                    this.SendOperationResponseAndDisconnect(new OperationResponse(request.OperationCode)
                    {
                        ReturnCode = (short) ErrorCode.OperationDenied,
                        DebugMessage = LBErrorMessages.AlreadyAuthenticated
                    }, sendParameters);

                    if (Loggers.DisconnectLogger.IsInfoEnabled)
                    {
                        Loggers.DisconnectLogger.Info(gcpDisconnectLogGuard, $"Disconnect from OnOperationRequest Reason_{ErrorCode.OperationDenied}, Msg:{LBErrorMessages.AlreadyAuthenticated}, p:{this}");
                    }

                    return;
                }

                this.HandleAuthenticateOperation(request, sendParameters);
                return;
            }

            if (!this.IsAuthenticated)
            {
                this.SendOperationResponseAndDisconnect(new OperationResponse(request.OperationCode)
                {
                    ReturnCode = (short)ErrorCode.OperationDenied,
                    DebugMessage = LBErrorMessages.NotAuthorized
                }, sendParameters);

                if (Loggers.DisconnectLogger.IsInfoEnabled)
                {
                    Loggers.DisconnectLogger.Info(gcpDisconnectLogGuard, $"Disconnect from OnOperationRequest Reason_{ErrorCode.OperationDenied}, Msg:{LBErrorMessages.NotAuthorized}, p:{this}");
                }
                return;
            }

            base.OnOperationRequest(request, sendParameters);
        }

        protected override RoomReference GetOrCreateRoom(string gameId, params object[] args)
        {
            return this.application.GameCache.GetRoomReference(gameId, this, args);
        }

        protected override bool TryCreateRoom(string gameId, out RoomReference roomReference, params object[] args)
        {
            return this.application.GameCache.TryCreateRoom(gameId, this, out roomReference, args);
        }

        protected override bool TryGetRoomReference(string gameId, out RoomReference roomReference)
        {
            return this.application.GameCache.TryGetRoomReference(gameId, this, out roomReference);
        }

        protected override bool TryGetRoomWithoutReference(string gameId, out Room room)
        {
            return this.application.GameCache.TryGetRoomWithoutReference(gameId, out room); 
        }

        public virtual string GetRoomCacheDebugString(string gameId)
        {
            return this.application.GameCache.GetDebugString(gameId); 
        }

        protected virtual void HandleAuthenticateOperation(OperationRequest operationRequest, SendParameters sendParameters)
        {
            var request = new AuthenticateRequest(this.Protocol, operationRequest);
            if (this.ValidateOperation(request, sendParameters) == false)
            {
                return;
            }

            AuthTimeoutChecker.StopWaitForAuthRequest(this, log);

            //only allow a maximum of X auth requests
            if (++this.NumberAuthRequests > this.MaxNumberAuthRequests)
            {
                this.SendOperationResponse(new OperationResponse(operationRequest.OperationCode)
                {
                    ReturnCode = (short)ErrorCode.OperationDenied,
                    DebugMessage = LBErrorMessages.Authenticating
                }, sendParameters);
                return;
            }

            if (!request.IsTokenAuthUsed && !AuthSettings.Default.Enabled)
            {
                this.SendOperationResponseAndDisconnect(new OperationResponse(operationRequest.OperationCode)
                {
                    ReturnCode = (short)ErrorCode.InvalidAuthentication,
                    DebugMessage = ErrorMessages.InvalidAuthenticationType
                }, sendParameters);
                return;
            }

            this.HandleAuthenticateTokenRequest(request, sendParameters);
        }

        protected void SetupPeer(string userId)
        {
            if (!string.IsNullOrEmpty(userId))
            {
                this.UserId = userId;
            }
            this.IsAuthenticated = true;
        }

        private void SetupPeer(AuthenticationToken authToken)
        {
            this.SetupPeer(authToken.UserId);
            this.AuthCookie = authToken.AuthCookie != null && authToken.AuthCookie.Count > 0 ? authToken.AuthCookie : null;
            this.AuthToken = authToken;
        }

        private void HandleAuthenticateTokenRequest(AuthenticateRequest request, SendParameters sendParameters)
        {
            var authToken = this.GetValidAuthToken(request, sendParameters);
            if (authToken == null)
            {
                return;
            }

            this.SetupPeer(authToken);
            // publish operation response
            var responseObject = new AuthenticateResponse { QueuePosition = 0 };
            this.SendOperationResponse(new OperationResponse(request.OperationRequest.OperationCode, responseObject), sendParameters);
        }

        protected AuthenticationToken GetValidAuthToken(AuthenticateRequest authenticateRequest, SendParameters sendParameters)
        {
            if (this.application.TokenCreator == null)
            {
                log.ErrorFormat("No custom authentication supported: AuthTokenKey not specified in config.");

                this.SendOperationResponseAndDisconnect(new OperationResponse(authenticateRequest.OperationRequest.OperationCode)
                {
                    ReturnCode = (short)ErrorCode.InvalidAuthentication,
                    DebugMessage = ErrorMessages.AuthTokenTypeNotSupported
                }, sendParameters);

                if (Loggers.DisconnectLogger.IsInfoEnabled)
                {
                    Loggers.DisconnectLogger.Info(gcpDisconnectLogGuard, $"Disconnect from GetValidAuthToken Reason_{ErrorCode.InvalidAuthentication}, Msg:{ErrorMessages.AuthTokenTypeNotSupported}, p:{this}");
                }

                return null;
            }

            // validate the authentication token
            if (string.IsNullOrEmpty(authenticateRequest.Token))
            {
                this.SendOperationResponseAndDisconnect(
                    new OperationResponse(authenticateRequest.OperationRequest.OperationCode)
                    {
                        ReturnCode = (short) ErrorCode.InvalidAuthentication,
                        DebugMessage = ErrorMessages.AuthTokenMissing
                    }, sendParameters);

                if (Loggers.DisconnectLogger.IsInfoEnabled)
                {
                    Loggers.DisconnectLogger.Info(gcpDisconnectLogGuard, $"Disconnect from GetValidAuthToken Reason_{ErrorCode.InvalidAuthentication}, Msg:{ErrorMessages.AuthTokenMissing}, p:{this}");
                }

                return null;
            }

            var tc = this.application.TokenCreator;
            if (!tc.DecryptAuthenticationToken(authenticateRequest.Token, out var authToken, out var errorMsg))
            {
                log.WarnFormat(canNotDecryptToken, "Could not decrypt authentication token. errorMsg:{0}, token: {1}", errorMsg, authenticateRequest.Token);

                this.SendOperationResponseAndDisconnect(new OperationResponse(authenticateRequest.OperationRequest.OperationCode)
                {
                    ReturnCode = (short) ErrorCode.InvalidAuthentication,
                    DebugMessage = ErrorMessages.AuthTokenInvalid
                }, sendParameters);

                if (Loggers.DisconnectLogger.IsInfoEnabled)
                {
                    Loggers.DisconnectLogger.Info(gcpDisconnectLogGuard, $"Disconnect from GetValidAuthToken Reason_{ErrorCode.InvalidAuthentication}, Msg:{ErrorMessages.AuthTokenInvalid}, p:{this}");
                }
                return null;
            }

            if (authToken.ExpireAtTicks < DateTime.UtcNow.Ticks)
            {
                this.SendOperationResponseAndDisconnect(
                    new OperationResponse(authenticateRequest.OperationRequest.OperationCode)
                    {
                        ReturnCode = (short) ErrorCode.AuthenticationTokenExpired,
                        DebugMessage = ErrorMessages.AuthTokenExpired
                    }, sendParameters);
                return null;
            }

            if (!this.ValidateAuthToken(authenticateRequest, sendParameters, authToken, out var errorCode, out errorMsg))
            {
                this.SendOperationResponseAndDisconnect(
                    new OperationResponse(authenticateRequest.OperationRequest.OperationCode)
                    {
                        ReturnCode = (short)errorCode,
                        DebugMessage = errorMsg
                    }, sendParameters);

                if (Loggers.DisconnectLogger.IsInfoEnabled)
                {
                    Loggers.DisconnectLogger.Info(gcpDisconnectLogGuard, $"Disconnect from GetValidAuthToken Reason_{ErrorCode.ExpectedGSCheckFailure}, Msg:{ErrorMessages.ExpectedGSCheckFailure}, p:{this}");
                }
                return null;
            }

            return authToken;
        }

        protected virtual bool ValidateAuthToken(AuthenticateRequest authenticateRequest, SendParameters sendParameters,
            AuthenticationToken authToken, out ErrorCode errorCode, out string errorMsg)
        {
            if (log.IsDebugEnabled)
            {
                log.Debug(
                    $"Token Validation: checking token with GS: {authToken.ExpectedGS} and GameId:{authToken.ExpectedGameId}");
            }

            if (GameServerSettings.Default.TokenCheckExpectedHostAndGame && authToken.ExpectedGS != GameServerSettings.Default.Master.PublicHostName)
            {
                log.Warn(tokenValidationLogGuard, "Token Validation: Expected GS is different this one. " +
                                                  $"egs:'{authToken.ExpectedGS}', gs:{GameServerSettings.Default.Master.PublicHostName}, uid:{authToken.UserId}, " +
                                                  $"AppId:{authToken.ApplicationId}/{authToken.ApplicationVersion}, peer:{this}");

                errorCode = ErrorCode.ExpectedGSCheckFailure;
                errorMsg = ErrorMessages.ExpectedGSCheckFailure;
                return false;
            }

            errorMsg = string.Empty;
            errorCode = ErrorCode.Ok;
            return true;
        }

        protected override PluginTraits GetPluginTraits()
        {
            return application.GameCache.PluginManager.PluginTraits;
        }

        protected override void HandleDebugGameOperation(OperationRequest operationRequest, SendParameters sendParameters)
        {
            if (!this.AllowDebugGameOperation)
            {
                this.SendOperationResponse(new OperationResponse(operationRequest.OperationCode)
                {
                    ReturnCode = (short)ErrorCode.OperationDenied,
                    DebugMessage = LBErrorMessages.NotAuthorized
                }, sendParameters);
                return;
            }

            var debugRequest = new DebugGameRequest(this.Protocol, operationRequest);
            if (this.ValidateOperation(debugRequest, sendParameters) == false)
            {
                return;
            }

            string debug = string.Format("DebugGame called from PID {0}. {1}", this.ConnectionId, this.GetRoomCacheDebugString(debugRequest.GameId));
            operationRequest.Parameters.Add((byte)ParameterCode.Info, debug);


            if (this.RoomReference == null)
            {
                // get a room without obtaining a reference:
                if (!this.TryGetRoomWithoutReference(debugRequest.GameId, out var room))
                {
                    var response = new OperationResponse
                    {
                        OperationCode = (byte)OperationCode.DebugGame,
                        ReturnCode = (short)ErrorCode.GameIdNotExists,
                        DebugMessage = HiveErrorMessages.GameIdDoesNotExist
                    };


                    this.SendOperationResponse(response, sendParameters);
                    return;
                }

                room.EnqueueOperation(this, debugRequest, sendParameters);
            }
            else
            {
                this.RoomReference.Room.EnqueueOperation(this, debugRequest, sendParameters);
            }
        }

        protected override void HandleRaiseEventOperation(OperationRequest operationRequest, SendParameters sendParameters)
        {
            base.HandleRaiseEventOperation(operationRequest, sendParameters);
            this.CheckAndUpdateTokenTtl();
        }

        protected override void HandleCreateGameOperation(OperationRequest operationRequest, SendParameters sendParameters)
        {
            if (this.application.ServerState != ServerState.Normal)
            {
                this.SendOperationResponseAndDisconnect(new OperationResponse
                {
                    OperationCode = operationRequest.OperationCode,
                    ReturnCode = (short)ErrorCode.OperationDenied,
                    DebugMessage = LBErrorMessages.ServerClosedForGameCreation
                }, sendParameters);

                if (Loggers.DisconnectLogger.IsInfoEnabled)
                {
                    Loggers.DisconnectLogger.Info(gcpDisconnectLogGuard, $"Disconnect from HandleCreateGameOperation Reason_{ErrorCode.OperationDenied}, Msg:{LBErrorMessages.ServerClosedForGameCreation}, p:{this}");
                }

                return;
            }

            base.HandleCreateGameOperation(operationRequest, sendParameters);
        }

        private void CheckAndUpdateTokenTtl()
        {
            var utcNow = DateTime.UtcNow;
            if (this.AuthToken == null 
                || this.AuthToken.ExpireAtTicks - utcNow.Ticks > this.tokenExpirationTime
                || (this.AuthToken.IsFinalExpireAtUsed && this.AuthToken.FinalExpireAtTicks >= utcNow.Ticks))
            {
                return;
            }

            this.SendAuthEvent();
        }

        private void SendAuthEvent()
        {
            if (!this.Connected)
            {
                return;
            }

            var response = new EventData((byte) Events.EventCode.AuthEvent)
            {
                Parameters = new Dictionary<byte, object>
                {
                    {(byte) ParameterCode.Token, this.GetEncryptedAuthToken()}
                }
            };
            this.SendEvent(response, new SendParameters());
        }

        protected virtual object GetEncryptedAuthToken()
        {
            var tc = this.application.TokenCreator;
            if (this.binaryTokenUsed)
            {
                return tc.EncryptAuthenticationTokenBinary(this.AuthToken, true);
            }
            return this.authOnceUsed ? tc.EncryptAuthenticationTokenV2(this.AuthToken, true)
                                    : tc.EncryptAuthenticationToken(this.AuthToken, true);
        }

        protected override bool MayJoinOrCreateGame(JoinGameRequest joinGameRequest, out short errorCode, out string errorMsg)
        {
            if (!base.MayJoinOrCreateGame(joinGameRequest, out errorCode, out errorMsg))
            {
                return false;
            }

            if (!this.ValidateExpectedGame(joinGameRequest))
            {
                log.Warn(tokenValidationLogGuard, "Token Validation: Expected game is different from requested game. " +
                                                  $"eg:'{this.AuthToken.ExpectedGameId}', rg:{joinGameRequest.GameId}, uid:{this.UserId}, peer:{this}");

                errorCode = (short)ErrorCode.ExpectedGameCheckFailure;
                errorMsg = HiveErrorMessages.ExpectedGameCheckFailure;
                return false;
            }

            var appState = this.application.ServerState;
            switch (appState)
            {
                case ServerState.OutOfRotation:
                    {
                        if (joinGameRequest.CreateIfNotExists)
                        {
                            // in case we need create room we reject request
                            if (!this.TryGetRoomWithoutReference(joinGameRequest.GameId, out _))
                            {//room not found
                                errorCode = (short) ErrorCode.OperationDenied;
                                errorMsg = LBErrorMessages.ServerClosedForGameCreation;
                                return false;
                            }
                        }
                        break;
                    }
                case ServerState.Offline:
                    {
                        errorCode = (short)ErrorCode.OperationDenied;
                        errorMsg = LBErrorMessages.ServerClosedAtAll;
                        return false;
                    }
            }

            return true;
        }

        protected virtual bool ValidateExpectedGame(JoinGameRequest joinRequest)
        {
            if (!GameServerSettings.Default.TokenCheckExpectedHostAndGame || this.AuthToken == null)
            {
                return true;
            }

            return joinRequest.GameId == this.AuthToken.ExpectedGameId;
        }

        #endregion

        #region IAuthTimeoutCheckerClient

        public IDisposable AuthTimeoutTimer { get; set; }
        public IFiber Fiber => this.RequestFiber;
        public void OnAuthTimeout(byte authOpCode)
        {
            this.SendOperationResponseAndDisconnect(new OperationResponse
            {
                OperationCode = authOpCode,
                ReturnCode = (short)ErrorCode.AuthRequestWaitTimeout,
                DebugMessage = ErrorMessages.NoAuthRequestInExpectedTime,
            }, new SendParameters());
        }

        #endregion
    }
}