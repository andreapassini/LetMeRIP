
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

using ExitGames.Configuration;
using ExitGames.Logging;

using Photon.Common.LoadBalancer.LoadShedding;
using Photon.LoadBalancing.ServerToServer.Operations;
using Photon.SocketServer;
using Photon.SocketServer.Diagnostics;

namespace Photon.LoadBalancing.GameServer
{
    public abstract class MasterServerConnectionBase : IDisposable
    {
        #region Fields and Constants

        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private readonly int connectRetryIntervalSeconds;

        private int isReconnecting;

        private Timer reconnectTimer;

        private OutgoingMasterServerPeer peer;

        private const string name = "s2s MS connection";

        protected readonly string ConnectionId = Guid.NewGuid().ToString();

        private readonly LogCountGuard connectionFailedGuard = new LogCountGuard(new TimeSpan(0, 1, 0));//once per minute
        #endregion

        #region .ctr

        protected MasterServerConnectionBase(GameApplication controller, string address, int port, int connectRetryIntervalSeconds)
        {
            this.Application = controller;
            this.Address = address;
            this.Port = port;
            this.connectRetryIntervalSeconds = connectRetryIntervalSeconds;
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("ConnectionId={0}", this.ConnectionId);
            }
        }

        #endregion

        #region Properties

        public GameApplication Application { get; }

        public string Address { get; }

        public IPEndPoint EndPoint { get; private set; }

        public int Port { get; }

        public bool IsReconnecting
        {
            get { return Interlocked.CompareExchange(ref this.isReconnecting, 0, 0) != 0; }
        }

        public string Name { get { return name; } }
        #endregion

        #region Publics

        public OutgoingMasterServerPeer GetPeer()
        {
            return this.peer;
        }

        public void Initialize()
        {
            this.ConnectToMaster();
        }

        public SendResult SendEventIfRegistered(IEventData eventData, SendParameters sendParameters)
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Sending event to master. EventCode={0}, ConnectionId={1}", eventData.Code, this.ConnectionId);
            }

            var masterPeer = this.peer;
            if (masterPeer == null || masterPeer.IsRegistered == false)
            {
                if (log.IsDebugEnabled)
                {
                    if (masterPeer != null)
                    {
                        log.DebugFormat("Event data was not sent. peer is unregistered. Event Code={0}, IsRegistered:{1}, ConnectionId:{2}",
                            eventData.Code, masterPeer.IsRegistered, this.ConnectionId);
                    }
                    else
                    {
                        log.DebugFormat("Event data was not sent. peer is null. Event Code={0}, ConnectionId:{1}", 
                            eventData.Code, this.ConnectionId);
                    }

                }
                return SendResult.Disconnected;
            }

            return masterPeer.SendEvent(eventData, sendParameters);
        }

        public SendResult SendEvent(IEventData eventData, SendParameters sendParameters)
        {
            var masterPeer = this.peer;
            if (masterPeer == null || masterPeer.Connected == false)
            {
                return SendResult.Disconnected;
            }

            return masterPeer.SendEvent(eventData, sendParameters);
        }

        public virtual void UpdateAllGameStates()
        {
        }

        public void ConnectToMaster(IPEndPoint endPoint)
        {
            if (this.Application.Running == false)
            {
                return;
            }

            if (this.peer == null)
            {
                this.peer = this.CreateServerPeer();
            }

            if (this.peer.ConnectTcp(endPoint, "Master", this.GetInitObject()))
            {
                if (log.IsInfoEnabled)
                {
                    log.Info($"{name}: Connecting to master at {endPoint}, serverId={this.Application.ServerId}");
                }
            }
            else
            {
                log.Warn($"{name}: master connection refused - is the process shutting down ? {this.Application.ServerId}");
            }
        }

        public virtual void OnConnectionEstablished()
        {
            if (log.IsInfoEnabled)
            {
                log.Info($"{name}: connection established: address:{this.Address}, ConnectionId:{this.ConnectionId}");
            }
            Interlocked.Exchange(ref this.isReconnecting, 0);
            this.Application.OnMasterConnectionEstablished(this);
        }

        public virtual void OnConnectionFailed(int errorCode, string errorMessage)
        {
            if (this.isReconnecting == 0)
            {
                log.Error($"{name}: connection failed: address={this.EndPoint}, errorCode={errorCode}, msg={errorMessage}");
            }
            else if (log.IsWarnEnabled)
            {
                log.Warn(this.connectionFailedGuard, $"{name}: connection failed: address={this.EndPoint}, errorCode={errorCode}, msg={errorMessage}");
            }

            this.ReconnectToMaster();

            this.Application.OnMasterConnectionFailed(this);
        }

        public virtual void OnDisconnect(int reasonCode, string reasonDetail)
        {
            this.ReconnectToMaster();
            this.Application.OnDisconnectFromMaster(this);
        }

        public void Dispose()
        {
            var timer = this.reconnectTimer;
            if (timer != null)
            {
                timer.Dispose();
                this.reconnectTimer = null;
            }

            var masterPeer = this.peer;
            if (masterPeer != null)
            {
                masterPeer.Disconnect(ErrorCodes.None);
                masterPeer.Dispose();
                this.peer = null;
            }
        }

        public void OnRegisteredAtMaster(RegisterGameServerResponse registerResponse)
        {
            if (log.IsInfoEnabled)
            {
                log.Info( $"{name}: connection registered on master: address:{this.Address}");
            }
            this.Application.OnRegisteredAtMaster(this, registerResponse);
        }

        #endregion

        #region Privates

        protected abstract OutgoingMasterServerPeer CreateServerPeer();

        private Dictionary<byte, object> GetInitObject()
        {
            var contract = new RegisterGameServerDataContract
            {
                GameServerAddress = this.Application.PublicIpAddress.ToString(),
                GameServerHostName = GameServerSettings.Default.Master.PublicHostName,

                UdpPort = this.Application.GamingUdpPort,
                TcpPort = this.Application.GamingTcpPort,
                WebSocketPort = this.Application.GamingWebSocketPort,
                SecureWebSocketPort = this.Application.GamingSecureWebSocketPort,
                WebRTCPort = GameServerSettings.Default.Master.GamingWebRTCPort,
                GamingWsPath = this.Application.GamingWsPath,
                ServerId = this.Application.ServerId.ToString(),
                ServerState = (int)this.Application.WorkloadController.ServerState,
                LoadLevelCount = (byte)FeedbackLevel.LEVELS_COUNT,
                PredictionData = this.GetPeer().GetPredictionData(),
                LoadBalancerPriority = GameServerSettings.Default.Master.LoadBalancerPriority,
                LoadIndex = (byte)this.Application.WorkloadController.FeedbackLevel,
                SupportedProtocols = OutgoingMasterServerPeer.GetSupportedProtocolsFromString(GameServerSettings.Default.Master.SupportedProtocols),

            };

            if (this.Application.PublicIpAddressIPv6 != null)
            {
                contract.GameServerAddressIPv6 = this.Application.PublicIpAddressIPv6.ToString();
            }

            if (log.IsInfoEnabled)
            {
                log.Info(
                    $"{name}: Parameters to register on master: address {contract.GameServerAddress}, " +
                    $"TCP {contract.TcpPort}, UDP {contract.UdpPort}, WebSocket {contract.WebSocketPort}, " +
                    $"Secure WebSocket {contract.SecureWebSocketPort}, " +
                    $"ServerID {contract.ServerId}, Hostname {contract.GameServerHostName}, IPv6Address {contract.GameServerAddressIPv6}, WebRTC {contract.WebRTCPort}");
            }

            return contract.ToDictionary();
        }

        private void ConnectToMaster()
        {
            if (this.reconnectTimer != null)
            {
                this.reconnectTimer.Dispose();
                this.reconnectTimer = null;
            }

            // check if the photon application is shutting down
            if (this.Application.Running == false)
            {
                return;
            }

            try
            {
                this.UpdateEndpoint();
                if (log.IsDebugEnabled)
                {
                    log.Debug($"{name}: MasterServer endpoint for address {this.Address} updated to {this.EndPoint}");
                }

                this.ConnectToMaster(this.EndPoint);
            }
            catch (Exception e)
            {
                log.Error(e);
                if (this.isReconnecting == 1)
                {
                    this.ReconnectToMaster();
                }
                else
                {
                    throw;
                }
            }
        }

        private void UpdateEndpoint()
        {
            if (!IPAddress.TryParse(this.Address, out var masterAddress))
            {
                var hostEntry = Dns.GetHostEntry(this.Address);
                if (hostEntry.AddressList == null || hostEntry.AddressList.Length == 0)
                {
                    throw new ConfigurationException(
                        "MasterIPAddress setting is neither an IP nor an DNS entry: " + this.Address);
                }

                masterAddress = hostEntry.AddressList.First(address => address.AddressFamily == AddressFamily.InterNetwork);

                if (masterAddress == null)
                {
                    throw new ConfigurationException(
                        "MasterIPAddress does not resolve to an IPv4 address! Found: "
                        + string.Join(", ", hostEntry.AddressList.Select(a => a.ToString()).ToArray()));
                }
            }

            this.EndPoint = new IPEndPoint(masterAddress, this.Port);
        }

        protected virtual void ReconnectToMaster()
        {
            if (this.Application.Running == false)
            {
                return;
            }

            if (this.reconnectTimer != null)
            {
                return;
            }

            Interlocked.Exchange(ref this.isReconnecting, 1);
            this.reconnectTimer = new Timer(o => this.ConnectToMaster(), null, this.connectRetryIntervalSeconds * 1000, Timeout.Infinite);
        }

        #endregion
    }
}
