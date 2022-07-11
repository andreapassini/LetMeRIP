// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IncomingGameServerPeer.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the IncomingGameServerPeer type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Net;

using ExitGames.Logging;

using Photon.Common;
using Photon.Common.LoadBalancer.Common;
using Photon.Hive.Operations;
using Photon.LoadBalancing.GameServer;
using Photon.LoadBalancing.ServerToServer.Events;
using Photon.LoadBalancing.ServerToServer.Operations;
using Photon.SocketServer;
using Photon.SocketServer.Diagnostics;
using Photon.SocketServer.ServerToServer;

using OperationCode = Photon.LoadBalancing.ServerToServer.Operations.OperationCode;

namespace Photon.LoadBalancing.MasterServer.GameServer
{
    public class IncomingGameServerPeer : InboundS2SPeer, IGameServerPeer
    {
        #region Constants and Fields

        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private readonly MasterApplication application;

        private readonly LogCountGuard logCountGuard = new LogCountGuard(new TimeSpan(0, 0, 1));

        #endregion

        #region Constructors and Destructors

        public IncomingGameServerPeer(InitRequest initRequest, MasterApplication masterApplication) 
            : base(initRequest)
        {
            this.application = masterApplication;

            if (log.IsInfoEnabled)
            {
                log.InfoFormat("game server connection from {0}:{1} established (id={2})", this.RemoteIP, this.RemotePort, this.ConnectionId);
            }

            this.SetPrivateCustomTypeCache(this.application.GetS2SCustomTypeCache());
        }

        #endregion

        #region Properties

        protected string ServerId { get; set; }

        public GameServerContext Context { get; private set; }

        public bool IsRegistered => !string.IsNullOrEmpty(this.ServerId);

        public string Name => "s2s GS connection";

        #endregion

        #region Public Methods

        public override string ToString()
        {
            var ctx = this.Context;
            if (ctx == null)
            {
                return base.ToString();
            }

            var addrInfo = ctx.AddressInfo;
            return $"GameServer(Id={this.ServerId}, ConnId={this.ConnectionId}) at {addrInfo.TcpAddress}/{addrInfo.UdpAddress}";

        }

        public object GetRegisterResponse()
        {
            IPAddress masterAddress = IPAddress.Loopback;
            var contract = new RegisterGameServerInitResponse
            {
                InternalAddress = masterAddress.GetAddressBytes(),
                ReturnCode = (short)ErrorCode.Ok,
                DebugMessage = string.Empty,
            };
            return contract.ToDictionary();
        }

        void IGameServerPeer.AttachToContext(GameServerContext context)
        {
            this.Context = context;

            if (this.Context != null)
            {
                this.ServerId = this.Context.ServerId;
            }

            if (log.IsDebugEnabled)
            {
                log.Debug($"Context attached to peer. context:{context}, p:{this}");
            }

            this.OnContextAttached();
        }

        void IGameServerPeer.DettachFromContext()
        {
            this.Context = null;

            if (log.IsDebugEnabled)
            {
                log.Debug($"Context dettached from peer. p:{this}");
            }

            this.OnContextDettached();
        }

        #endregion

        #region Methods

        protected virtual void OnContextAttached()
        {
        }

        protected virtual void OnContextDettached()
        {
        }

        private OperationResponse HandleRegisterGameServerRequest(OperationRequest request)
        {
            try
            {
                var registerRequest = new RegisterGameServer(this.Protocol, request);

                if (registerRequest.IsValid == false)
                {
                    string msg = registerRequest.GetErrorMessage();
                    log.ErrorFormat("RegisterGameServer contract error: {0}", msg);

                    return new OperationResponse(request.OperationCode) { DebugMessage = msg, ReturnCode = (short)ErrorCode.OperationInvalid };
                }

                IPAddress masterAddress = IPAddress.Loopback;
                var contract = new RegisterGameServerResponse { InternalAddress = masterAddress.GetAddressBytes() };

                // is master
                if (!this.application.IsMaster)
                {
                    return new OperationResponse(request.OperationCode, contract)
                               {
                                   ReturnCode = (short)ErrorCode.RedirectRepeat,
                                   DebugMessage = "RedirectRepeat"
                               };
                }

                if (log.IsDebugEnabled)
                {
                    log.DebugFormat(
                        "Received register request: Address={0}, UdpPort={1}, TcpPort={2}, WebSocketPort={3}, SecureWebSocketPort={4}, State={5}, Hostname={6}, IPv6Address={7}, WebRTCPort={8}",
                        registerRequest.GameServerAddress,
                        registerRequest.UdpPort,
                        registerRequest.TcpPort,
                        registerRequest.WebSocketPort,
                        registerRequest.SecureWebSocketPort,
                        (ServerState)registerRequest.ServerState,
                        registerRequest.GameServerHostName,
                        registerRequest.GameServerAddressIPv6,
                        registerRequest.WebRTCPort);
                }

                this.application.GameServers.RegisterGameServer(registerRequest, this);

                var addrInfo = this.Context.AddressInfo;
                if (log.IsDebugEnabled)
                {
                    log.Debug(
                        $"Registered GameServerAddress={addrInfo.Address} GameServerAddressIPv6={addrInfo.AddressIPv6}" +
                        $" TcpAddress={addrInfo.TcpAddress} TcpAddressIPv6={addrInfo.TcpAddressIPv6} UdpAddress={addrInfo.UdpAddress} UdpAddressIPv6={addrInfo.UdpAddressIPv6}" +
                        $" WebSocketAddress={addrInfo.WebSocketAddress} WebSocketAddressIPv6={addrInfo.WebSocketAddressIPv6}" +
                        $" SecureWebSocketAddress={addrInfo.SecureWebSocketHostname}");
                }

                return new OperationResponse(request.OperationCode, contract);
            }
            catch (Exception e)
            {
                log.Error(e);
                return new OperationResponse(request.OperationCode) { DebugMessage = e.Message, ReturnCode = (short)ErrorCode.InternalServerError };
            }
        }

        private void HandleUpdateGameServerEvent(IEventData eventData)
        {
            var updateGameServer = new UpdateServerEvent(this.Protocol, eventData);
            if (updateGameServer.IsValid == false)
            {
                string msg = updateGameServer.GetErrorMessage();
                log.ErrorFormat("UpdateServer contract error: {0}", msg);
                return;
            }

            var ctx = this.Context;
            ctx?.HandleUpdateGameServerEvent(updateGameServer);
        }

        private void HandleGameUpdatesBatchEvent(IEventData eventData)
        {
            var updateEvent = new GameUpdatesBatchEvent(this.Protocol, eventData);
            if (updateEvent.IsValid == false)
            {
                string msg = updateEvent.GetErrorMessage();
                log.ErrorFormat("UpdateServer contract error: {0}", msg);
                return;
            }

            var ctx = this.Context;
            ctx?.HandleGameUpdatesBatchEvent(updateEvent);
        }

        private void HandleReplicationHelperEvent(IEventData eventData)
        {
            var finishEvent = new ReplicationHelperEvent(this.Protocol, eventData);
            if (finishEvent.IsValid == false)
            {
                string msg = finishEvent.GetErrorMessage();
                log.ErrorFormat("UpdateExpectedGamesCount contract error: {0}", msg);
                return;
            }

            var ctx = this.Context;
            ctx?.HandleReplicationHelperEvent(finishEvent);
        }

        private void HandleUpdateGameState(IEventData eventData)
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("HandleUpdateGameState");
            }

            var updateEvent = new UpdateGameEvent(this.Protocol, eventData);
            if (updateEvent.IsValid == false)
            {
                string msg = updateEvent.GetErrorMessage();
                log.ErrorFormat("UpdateGame contract error: {0}", msg);
                return;
            }

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("HandleUpdateGameState: {0}, reinitialize: {1}", updateEvent.GameId, updateEvent.Reinitialize);
            }

            var ctx = this.Context;
            ctx?.HandleUpdateGameEvent(updateEvent);
        }

        private void HandleRemoveGameState(IEventData eventData)
        {
            var removeEvent = new RemoveGameEvent(this.Protocol, eventData);
            if (removeEvent.IsValid == false)
            {
                string msg = removeEvent.GetErrorMessage();
                log.ErrorFormat("RemoveGame contract error: {0}", msg);
                return;
            }

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("HandleRemoveGameState: {0}", removeEvent.GameId);
            }

            var ctx = this.Context;
            ctx?.HandleRemoveGameState(removeEvent);
        }

        private void HandleUpdateAppStatsEvent(IEventData eventData)
        {
            var ctx = this.Context;
            ctx?.HandleUpdateApplicationStats(eventData);
        }

        private void HandleGameServerLeaveEvent(IEventData eventData)
        {
            var ctx = this.Context;
            if (log.IsInfoEnabled)
            {
                log.InfoFormat("Got leave message from server. context={0}, p:{1}", ctx != null ? ctx.ToString() : "<null>", this);
            }

            ctx?.HandleGameServerLeave(eventData);
        }

        protected override void OnDisconnect(int reasonCode, string reasonDetail)
        {
            if (log.IsWarnEnabled)
            {
                var serverId = this.ServerId;
                var srvAddr = this.Context != null ? this.Context.AddressInfo.Address : "{null}";

                if (ApplicationBase.Instance.Running)
                {
                    log.Warn(this.Context?.OnServerDisconnectLogGuard, 
                        this.Name + ": OnDisconnect - game server connection " +
                        $"closed (connectionId={this.ConnectionId}, addr:{srvAddr}, serverId={serverId}, reason={reasonCode})");
                }
                else
                {
                    log.Info("OnDisconnect: game server connection closed " +
                             $"(connectionId={this.ConnectionId}, addr:{srvAddr}, serverId={serverId}, reason={reasonCode})");
                }
            }

            var ctx = this.Context;
            ctx?.OnGameServerDisconnect(this, reasonCode);
        }

        protected override void OnEvent(IEventData eventData, SendParameters sendParameters)
        {
            try
            {
                if (!this.Connected)
                {
                    if (log.IsDebugEnabled)
                    {
                        log.Debug($"Peer is already disconnected. Even {eventData.Code} is ignored. p:{this}");
                    }
                    return;
                }

                if (!this.IsRegistered)
                {
                    log.WarnFormat(this.Name + ": received game server event {0} but server is not registered. p:{1}", eventData.Code, this);
                    return;
                }

                if (this.Context == null)
                {
                    // if server is registered and this.Context == null then we are disconnecting because of attaching new peer to context
                    if (log.IsWarnEnabled)
                    {
                        log.WarnFormat(this.logCountGuard,
                            "peer does not have context. event is ignored. Event:{0}, p:{1}", eventData.Code, this);
                    }
                    return;
                }

                switch ((ServerEventCode)eventData.Code)
                {
                    default:
                        if (log.IsDebugEnabled)
                        {
                            log.DebugFormat("Received unknown event code {0}", eventData.Code);
                        }

                        break;

                    case ServerEventCode.UpdateServer:
                        this.HandleUpdateGameServerEvent(eventData);
                        break;

                    case ServerEventCode.UpdateGameState:
                        this.HandleUpdateGameState(eventData);
                        break;

                    case ServerEventCode.GameUpdatesBatch:
                        this.HandleGameUpdatesBatchEvent(eventData);
                        break;

                    case ServerEventCode.RemoveGameState:
                        this.HandleRemoveGameState(eventData);
                        break;

                    case ServerEventCode.UpdateAppStats:
                        this.HandleUpdateAppStatsEvent(eventData);
                        break;

                    case ServerEventCode.ExpectedGamesCount:
                        this.HandleReplicationHelperEvent(eventData);
                        break;

                    case ServerEventCode.GameServerLeave:
                        this.HandleGameServerLeaveEvent(eventData);
                        break;
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }

        protected override void OnOperationRequest(OperationRequest request, SendParameters sendParameters)
        {
            try
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("OnOperationRequest: pid={0}, op={1}", this.ConnectionId, request.OperationCode);
                }

                OperationResponse response;

                switch ((OperationCode)request.OperationCode)
                {
                    default:
                        response = new OperationResponse(request.OperationCode)
                        {
                            ReturnCode = (short)ErrorCode.OperationInvalid, 
                            DebugMessage = HiveErrorMessages.UnknownOperationCode
                        };
                        break;

                    case OperationCode.RegisterGameServer:
                        {
                            response = this.Context != null
                                           ? new OperationResponse(request.OperationCode) { ReturnCode = (short)ErrorCode.InternalServerError, DebugMessage = "already registered" }
                                           : this.HandleRegisterGameServerRequest(request);
                            break;
                        }
                }

                this.SendOperationResponse(response, sendParameters);
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }

        protected override void OnOperationResponse(OperationResponse operationResponse, SendParameters sendParameters)
        {
            throw new NotSupportedException();
        }

        protected override void OnSendBufferFull()
        {
            log.WarnFormat(this.Name + ": Abort game server connection {0}, IP {1}: SendBufferFull", this.ConnectionId, this.RemoteIP);
            this.AbortConnection();
        }

        #endregion
    }
}