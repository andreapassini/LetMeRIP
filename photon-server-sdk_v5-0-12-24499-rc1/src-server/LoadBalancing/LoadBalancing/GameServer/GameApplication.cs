// --------------------------------------------------------------------------------------------------------------------
// <copyright file="GameApplication.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the GameApplication type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime;

using ExitGames.Concurrency.Fibers;
using ExitGames.Logging;
using ExitGames.Logging.Log4Net;

using log4net;
using log4net.Config;

using Microsoft.Extensions.Configuration;

using Photon.Common.Authentication;
using Photon.Common.LoadBalancer;
using Photon.Common.LoadBalancer.Common;
using Photon.Common.LoadBalancer.LoadShedding;
using Photon.Common.LoadBalancer.LoadShedding.Diagnostics;
using Photon.Common.Misc;
using Photon.Hive;
using Photon.Hive.Common;
using Photon.Hive.Messages;
using Photon.Hive.WebRpc;
using Photon.Hive.WebRpc.Configuration;
using Photon.LoadBalancing.Common;
using Photon.LoadBalancing.ServerToServer.Operations;
using Photon.SocketServer;
using Photon.SocketServer.Rpc.Protocols;

using ConfigurationException = ExitGames.Configuration.ConfigurationException;
using LogManager = ExitGames.Logging.LogManager;

namespace Photon.LoadBalancing.GameServer
{
    public class GameApplication : ApplicationBase
    {
        #region Constants and Fields

        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private ServerStateManager serverStateManager;

        protected PoolFiber executionFiber;

        private WebRpcManager webRpcManager;
        #endregion

        #region Constructors and Destructors

        static GameApplication()
        {
            LogManager.SetLoggerFactory(Log4NetLoggerFactory.Instance);
        }

        public GameApplication()
        : this(LoadConfiguration())
        {
        }

        public GameApplication(IConfiguration configuration)
        : base(configuration)
        {
            this.UpdateMasterEndPoint();

            this.ServerId = Guid.NewGuid();
            this.GamingTcpPort = GameServerSettings.Default.Master.GamingTcpPort;
            this.GamingUdpPort = GameServerSettings.Default.Master.GamingUdpPort;
            this.GamingWebSocketPort = GameServerSettings.Default.Master.GamingWebSocketPort;
            this.GamingSecureWebSocketPort = GameServerSettings.Default.Master.GamingSecureWebSocketPort;
            this.GamingWsPath = string.IsNullOrEmpty(GameServerSettings.Default.Master.GamingWsPath) ? string.Empty : "/" + GameServerSettings.Default.Master.GamingWsPath;
            this.GamingWebRTCPort = GameServerSettings.Default.Master.GamingWebRTCPort;

            this.ConnectRetryIntervalSeconds = GameServerSettings.Default.S2S.ConnectRetryInterval;

            this.executionFiber = new PoolFiber();
            this.executionFiber.Start();
        }

        #endregion

        #region Public Properties

        public Guid ServerId { get; private set; }

        public int? GamingTcpPort { get; protected set; }

        public int? GamingUdpPort { get; protected set; }

        public int? GamingWebSocketPort { get; protected set; }

        public int? GamingSecureWebSocketPort { get; set; }

        public string GamingWsPath { get; protected set; }

        public int? GamingWebRTCPort { get; protected set; }

        public IPEndPoint MasterEndPoint { get; protected set; }

        public ApplicationStatsPublisher AppStatsPublisher { get; protected set; }

        public MasterServerConnection MasterServerConnection { get; protected set; }

        public IPAddress PublicIpAddress { get; protected set; }

        public IPAddress PublicIpAddressIPv6 { get; protected set; }

        public WorkloadController WorkloadController { get; protected set; }

        public virtual GameCache GameCache { get; protected set; }

        public AuthTokenFactory TokenCreator { get; protected set; }

        public S2SCustomTypeCacheMan S2SCacheMan { get; protected set; }

        public int ConnectRetryIntervalSeconds { get; set; }
        #endregion

        #region Properties

        protected bool IsMaster { get; set; }

        public GameUpdatesBatcher GameUpdatesBatcher { get; private set; }
        public ServerState ServerState { get; private set; }

        #endregion

        #region Public Methods

        public virtual void OnMasterConnectionEstablished(MasterServerConnectionBase masterServerConnectionBase)
        {
            this.serverStateManager.CheckAppOffline();
        }

        public virtual void OnMasterConnectionFailed(MasterServerConnectionBase masterServerConnection)
        {
        }

        public virtual void OnDisconnectFromMaster(MasterServerConnectionBase masterServerConnection)
        {
        }

        public CustomTypeCache GetS2SCustomTypeCache()
        {
            return this.S2SCacheMan.GetCustomTypeCache();
        }

        public virtual void OnRegisteredAtMaster(MasterServerConnectionBase masterServerConnectionBase, RegisterGameServerResponse registerResponse)
        {
            masterServerConnectionBase.UpdateAllGameStates();
        }

        #endregion

        #region Methods

        private static IConfiguration LoadConfiguration()
        {
            var cb = new ConfigurationBuilder();
            var cbpath = Path.GetDirectoryName(typeof(GameApplication).Assembly.CodeBase).Remove(0, 6);
            return cb.AddXmlFile(Path.Combine(cbpath, "GameServer.xml.config")).Build();
        }

        private void SetupTokenCreator()
        {
            var sharedKey = Photon.Common.Authentication.Settings.Default.AuthTokenKey;
            if (string.IsNullOrEmpty(sharedKey))
            {
                log.WarnFormat("AuthTokenKey not specified in config. Authentication tokens are not supported");
                return;
            }

            var hmacKey = Photon.Common.Authentication.Settings.Default.HMACTokenKey;
            if (string.IsNullOrEmpty(hmacKey))
            {
                log.Warn("HMACTokenKey not specified in config");
            }


            var expirationTimeSeconds = Photon.Common.Authentication.Settings.Default.AuthTokenExpirationSeconds;
            //if (expirationTimeSeconds <= 0)
            //{
            //    log.ErrorFormat("Authentication token expiration to low: expiration={0} seconds", expirationTimeSeconds);
            //}

            var expiration = TimeSpan.FromSeconds(expirationTimeSeconds);
            this.TokenCreator = GetAuthTokenFactory();
            this.TokenCreator.Initialize(sharedKey, hmacKey, expiration, $"GS:{Environment.MachineName}");

            log.InfoFormat("TokenCreator initialized with an expiration of {0}", expiration);
        }

        protected virtual AuthTokenFactory GetAuthTokenFactory()
        {
            return new AuthTokenFactory();
        }

        private void UpdateMasterEndPoint()
        {
            IPAddress masterAddress;
            if (!IPAddress.TryParse(GameServerSettings.Default.S2S.MasterIPAddress, out masterAddress))
            {
                IPHostEntry hostEntry = null;
                try
                {
                    hostEntry = Dns.GetHostEntry(GameServerSettings.Default.S2S.MasterIPAddress);
                }
                catch (Exception e)
                {
                    log.Error(e);
                }
                if (hostEntry == null || hostEntry.AddressList == null || hostEntry.AddressList.Length == 0)
                {
                    throw new ConfigurationException(
                        "MasterIPAddress setting is neither an IP nor an DNS entry: "
                        + GameServerSettings.Default.S2S.MasterIPAddress);
                }

                masterAddress =
                    hostEntry.AddressList.First(address => address.AddressFamily == AddressFamily.InterNetwork);

                if (masterAddress == null)
                {
                    throw new ConfigurationException(
                        "MasterIPAddress does not resolve to an IPv4 address! Found: "
                        + string.Join(", ", hostEntry.AddressList.Select(a => a.ToString()).ToArray()));
                }
            }

            int masterPort = GameServerSettings.Default.S2S.OutgoingMasterServerPeerPort;
            this.MasterEndPoint = new IPEndPoint(masterAddress, masterPort);
        }

        /// <summary>
        ///   Sanity check to verify that game states are cleaned up correctly
        /// </summary>
        protected virtual void CheckGames()
        {
            var roomNames = this.GameCache.GetRoomNames();

            foreach (var roomName in roomNames)
            {
                Room room;
                if (this.GameCache.TryGetRoomWithoutReference(roomName, out room))
                {
                    room.EnqueueMessage(new RoomMessage((byte)GameMessageCodes.CheckGame));
                }
            }
        }

        protected virtual PeerBase CreateGamePeer(InitRequest initRequest)
        {
            var peer = new GameClientPeer(initRequest, this);
            {
                if (this.webRpcManager.IsRpcEnabled)
                {
                    peer.WebRpcHandler = this.webRpcManager.GetWebRpcHandler();
                }
                initRequest.ResponseObject = "ResponseObject";
            }
            return peer;
        }

        protected override PeerBase CreatePeer(InitRequest initRequest)
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("CreatePeer for {0}", initRequest.ApplicationId);
            }

            if (log.IsDebugEnabled)
            {
                log.DebugFormat(
                    "incoming game peer at {0}:{1} from {2}:{3}",
                    initRequest.LocalIP,
                    initRequest.LocalPort,
                    initRequest.RemoteIP,
                    initRequest.RemotePort);
            }

            return this.CreateGamePeer(initRequest);
        }

        protected virtual void InitLogging()
        {
            LogManager.SetLoggerFactory(Log4NetLoggerFactory.Instance);
            GlobalContext.Properties["Photon:ApplicationLogPath"] = Path.Combine(this.ApplicationRootPath, "log");
            GlobalContext.Properties["LogFileName"] = "GS" + this.ApplicationName;
#if NETSTANDARD2_0 || NETCOREAPP
            var logRepository = log4net.LogManager.GetRepository(Assembly.GetEntryAssembly());
            XmlConfigurator.ConfigureAndWatch(logRepository, new FileInfo(Path.Combine(this.BinaryPath, "log4net.config")));
#else
            XmlConfigurator.ConfigureAndWatch(new FileInfo(Path.Combine(this.BinaryPath, "log4net.config")));
#endif
        }

        protected override void OnStopRequested()
        {
            log.InfoFormat("OnStopRequested: serverid={0}", ServerId);

            if (this.WorkloadController != null)
            {
                this.WorkloadController.Stop();
            }

            if (this.MasterServerConnection != null)
            {
                this.MasterServerConnection.SendLeaveEventAndWaitForResponse(GameServerSettings.Default.S2S.StopNotificationTimeout);
                this.MasterServerConnection.Dispose();
                this.MasterServerConnection = null;
            }

            base.OnStopRequested();
        }

        protected override void Setup()
        {
            this.InitLogging();
            this.InitInboundController();

            this.S2SCacheMan = new S2SCustomTypeCacheMan();

            var env = new Dictionary<string, object>
            {
                {"AppId", this.HwId},
                {"AppVersion", ""},
                {"Region", ""},
                {"Cloud", ""},
            };

            var settings = WebRpcSettings.Default;

            this.webRpcManager = new WebRpcManager(settings.Enabled, settings.BaseUrl, env, settings.HttpQueueSettings);

            GCSettings.LatencyMode = CommonSettings.Default.GCLatencyMode;

            if (log.IsInfoEnabled)
            {
                log.InfoFormat("Using Server GC:{0}", GCSettings.IsServerGC);
                log.InfoFormat("Using GC LatencyMode:{0}", GCSettings.LatencyMode);
            }

            log.InfoFormat("Setup: serverId={0}", ServerId);

            Protocol.AllowRawCustomValues = true;
            Protocol.RegisterTypeMapper(new UnknownTypeMapper());

            this.PublicIpAddress = PublicIPAddressReader.ParsePublicIpAddress(GameServerSettings.Default.Master.PublicIPAddress);
            this.PublicIpAddressIPv6 = string.IsNullOrEmpty(GameServerSettings.Default.Master.PublicIPAddressIPv6) ?
                null : IPAddress.Parse(GameServerSettings.Default.Master.PublicIPAddressIPv6);

            this.IsMaster = PublicIPAddressReader.IsLocalIpAddress(this.MasterEndPoint.Address) || this.MasterEndPoint.Address.Equals(this.PublicIpAddress);

            Counter.IsMasterServer.RawValue = this.IsMaster ? 1 : 0;

            this.InitGameCache();

            if (CommonSettings.Default.EnablePerformanceCounters)
            {
                this.InitCorePerformanceCounters();
            }
            else
            {
                log.Info("Performance counters are disabled");
            }

            this.SetupTokenCreator();
            this.SetupFeedbackControlSystem();
            this.SetupServerStateMonitor();
            this.SetupMasterConnection();
            this.Initialize();

            this.executionFiber.ScheduleOnInterval(this.CheckGames, 60000, 60000);
        }

        /// <summary>
        /// we put here stuff that should be initialized only for self hosted
        /// </summary>
        protected virtual void Initialize()
        {
            if (GameServerSettings.Default.Master.AppStatsPublishInterval > 0)
            {
                this.AppStatsPublisher = new ApplicationStatsPublisher(this, GameServerSettings.Default.Master.AppStatsPublishInterval);
            }
        }

        private void InitInboundController()
        {
            var controller = new InboundController(200, 255, 180, 255);
            controller.SetupOperationParameter((byte)Hive.Operations.OperationCode.RaiseEvent, (byte)Hive.Operations.ParameterKey.Data, 
                new ParameterData(InboundController.PROVIDE_SIZE));
            controller.SetupOperationParameter((byte)Hive.Operations.OperationCode.SetProperties,
                (byte)Hive.Operations.ParameterKey.Properties, new ParameterData(
                            InboundController.PROVIDE_SIZE_OF_SUB_KEYS,
                            GameServerSettings.Default.Limits.Inbound.Properties.MaxPropertiesSizePerRequest, 
                            GameServerSettings.Default.Limits.Inbound.Properties.MaxPropertiesPerRequest));
            controller.SetupOperationParameter((byte)Hive.Operations.OperationCode.SetProperties,
                (byte)Hive.Operations.ParameterKey.ExpectedValues, new ParameterData(
                            InboundController.PROVIDE_SIZE_OF_SUB_KEYS,
                            GameServerSettings.Default.Limits.Inbound.Properties.MaxPropertiesSizePerRequest,
                            GameServerSettings.Default.Limits.Inbound.Properties.MaxPropertiesPerRequest));

            controller.SetupOperationParameter((byte)Hive.Operations.OperationCode.JoinGame,
                (byte)Hive.Operations.ParameterKey.ActorProperties, new ParameterData(InboundController.PROVIDE_SIZE_OF_SUB_KEYS));
            controller.SetupOperationParameter((byte)Hive.Operations.OperationCode.JoinGame,
                (byte)Hive.Operations.ParameterKey.GameProperties, new ParameterData(InboundController.PROVIDE_SIZE_OF_SUB_KEYS));
            controller.SetupOperationParameter((byte)Hive.Operations.OperationCode.JoinGame,
                (byte)Hive.Operations.ParameterKey.AddUsers, new ParameterData(InboundController.PROVIDE_SIZE));

            controller.SetupOperationParameter((byte)Hive.Operations.OperationCode.CreateGame,
                (byte)Hive.Operations.ParameterKey.ActorProperties, new ParameterData(InboundController.PROVIDE_SIZE_OF_SUB_KEYS));
            controller.SetupOperationParameter((byte)Hive.Operations.OperationCode.CreateGame,
                (byte)Hive.Operations.ParameterKey.GameProperties, new ParameterData(InboundController.PROVIDE_SIZE_OF_SUB_KEYS));
            controller.SetupOperationParameter((byte)Hive.Operations.OperationCode.CreateGame,
                (byte)Hive.Operations.ParameterKey.AddUsers, new ParameterData(InboundController.PROVIDE_SIZE));

            //setting limits
            controller.SetupOperationLimits((byte)Hive.Operations.OperationCode.SetProperties, 
                new OperationLimits(InboundController.Unlimited, GameServerSettings.Default.Limits.Inbound.Operations.SetPropertiesRate));
            controller.SetupOperationLimits((byte)Hive.Operations.OperationCode.GetProperties, 
                new OperationLimits(InboundController.Unlimited, GameServerSettings.Default.Limits.Inbound.Operations.GetPropertiesRate));

            controller.SetupOperationLimits((byte)Hive.Operations.OperationCode.JoinGame, 
                new OperationLimits(InboundController.Unlimited, GameServerSettings.Default.Limits.Inbound.Operations.JoinGameRate));
            controller.SetupOperationLimits((byte)Hive.Operations.OperationCode.Join, 
                new OperationLimits(InboundController.Unlimited, GameServerSettings.Default.Limits.Inbound.Operations.JoinGameRate));
            controller.SetupOperationLimits((byte)Hive.Operations.OperationCode.CreateGame, 
                new OperationLimits(InboundController.Unlimited, GameServerSettings.Default.Limits.Inbound.Operations.CreateGameRate));

            controller.SetupOperationLimits((byte)Hive.Operations.OperationCode.Ping, 
                new OperationLimits(InboundController.Unlimited, GameServerSettings.Default.Limits.Inbound.Operations.PingRate));

            controller.SetupOperationLimits((byte)Hive.Operations.OperationCode.ChangeGroups, 
                new OperationLimits(InboundController.Unlimited, GameServerSettings.Default.Limits.Inbound.Operations.ChangeGroupsRate));

            controller.SetupOperationLimits((byte)Hive.Operations.OperationCode.ChangeGroups, 
                new OperationLimits(InboundController.Unlimited, GameServerSettings.Default.Limits.Inbound.Operations.ChangeGroupsRate));

            controller.SetupOperationLimits((byte)Hive.Operations.OperationCode.DebugGame, 
                new OperationLimits(InboundController.Unlimited, GameServerSettings.Default.Limits.Inbound.Operations.DebugGameRate));

            controller.SetupOperationLimits((byte)Hive.Operations.OperationCode.Rpc, 
                new OperationLimits(InboundController.Unlimited, GameServerSettings.Default.Limits.Inbound.Operations.RpcRate));

            controller.SetupOperationLimits((byte)Hive.Operations.OperationCode.Settings,
                new OperationLimits(InboundController.Unlimited, GameServerSettings.Default.Limits.Inbound.Operations.SettingsRate));

            controller.SetupOperationLimits((byte)Hive.Operations.OperationCode.JoinLobby, OperationLimits.BlockOperation());
            controller.SetupOperationLimits((byte)Hive.Operations.OperationCode.LeaveLobby, OperationLimits.BlockOperation());
            controller.SetupOperationLimits((byte)Hive.Operations.OperationCode.JoinLobby, OperationLimits.BlockOperation());
            controller.SetupOperationLimits((byte)Hive.Operations.OperationCode.JoinRandomGame, OperationLimits.BlockOperation());
            controller.SetupOperationLimits((byte)Hive.Operations.OperationCode.FindFriends, OperationLimits.BlockOperation());
            controller.SetupOperationLimits((byte)Hive.Operations.OperationCode.LobbyStats, OperationLimits.BlockOperation());
            controller.SetupOperationLimits((byte)Hive.Operations.OperationCode.GetGameList, OperationLimits.BlockOperation());

            Protocol.InboundController = controller;
        }

        private void SetupServerStateMonitor()
        {
            var serverStateFilePath = GameServerSettings.Default.ServerStateFile;

            this.serverStateManager = new ServerStateManager(this.WorkloadController, this.ApplicationName);
            this.serverStateManager.OnNewServerState += OnNewServerState;

            if (string.IsNullOrEmpty(serverStateFilePath) == false)
            {
                this.serverStateManager.Start(Path.Combine(this.ApplicationRootPath, serverStateFilePath));
            }

            if (GameServerSettings.Default.EnableNamedPipe)
            {
                serverStateManager.StartListenPipe();
            }
        }

        protected virtual void SetupMasterConnection()
        {
            if (log.IsInfoEnabled)
            {
                log.Info("Initializing master server connection ...");
            }

            var masterAddress = GameServerSettings.Default.S2S.MasterIPAddress;
            var masterPost = GameServerSettings.Default.S2S.OutgoingMasterServerPeerPort;
            this.MasterServerConnection = new MasterServerConnection(this, masterAddress, masterPost, this.ConnectRetryIntervalSeconds);
            this.MasterServerConnection.Initialize();
            this.GameUpdatesBatcher = new GameUpdatesBatcher(new GameUpdatesBatcherParams
            {
                MasterServerConnection = this.MasterServerConnection,
                ApplicationVersion = "",
                ApplicationId = "",
                Fiber = this.executionFiber,
                UseBatcher = GameServerSettings.Default.Master.UseGameUpdatesBatcher,
                MaxUpdatesToBatch = GameServerSettings.Default.Master.MaxGameUpdatesToBatch,
                BatchPeriod = GameServerSettings.Default.Master.BatchPeriod,
            });
        }

        private void SetupFeedbackControlSystem()
        {
            var workLoadConfigFile = GameServerSettings.Default.WorkloadConfigFile;

            this.WorkloadController = new WorkloadController(
                this, "_Total", 1000, this.ServerId.ToString(), workLoadConfigFile);

            if (!this.WorkloadController.IsInitialized)
            {
                const string message = "WorkloadController failed to be constructed";

                if (CommonSettings.Default.EnablePerformanceCounters)
                {
                    throw new Exception(message);
                }

                log.Warn(message);
            }

            this.WorkloadController.Start();
        }

        /// <summary>
        /// We need this method here to gracefully skip game cache initialization in descendants
        /// </summary>
        protected virtual void InitGameCache()
        {
            this.GameCache = new GameCache(this);

        }

        protected override void TearDown()
        {
            log.InfoFormat("TearDown: serverId={0}", ServerId);

            if (this.WorkloadController != null)
            {
                this.WorkloadController.Stop();
            }

            if (this.MasterServerConnection != null)
            {
                this.MasterServerConnection.SendLeaveEventAndWaitForResponse(GameServerSettings.Default.S2S.StopNotificationTimeout);
                this.MasterServerConnection.Dispose();
                this.MasterServerConnection = null;
            }

            if (this.serverStateManager != null)
            {
                this.serverStateManager.StopListenPipe();
            }
        }

        protected virtual void OnNewServerState(ServerState oldState, ServerState requestedState, TimeSpan offlineTime)
        {
            switch (requestedState)
            {
                case ServerState.Normal:
                case ServerState.OutOfRotation:
                    if (oldState == ServerState.Offline)
                    {
                        //if (this.MasterServerConnection != null)
                        //{
                        //    var peer = this.MasterServerConnection.GetPeer();
                        //    if (peer != null && peer.IsRegistered)
                        //    {
                        //        this.MasterServerConnection.UpdateAllGameStates();
                        //    }
                        //}
                        //else
                        //{
                        //    log.WarnFormat("Server state is updated but there is not connection to master server");
                        //}
                    }
                    break;

                case ServerState.Offline:
                    this.RaiseOfflineEvent(offlineTime);
                    break;
            }

            this.ServerState = requestedState;
        }

        protected virtual void RaiseOfflineEvent(TimeSpan time)
        {

        }

        #endregion
    }
}