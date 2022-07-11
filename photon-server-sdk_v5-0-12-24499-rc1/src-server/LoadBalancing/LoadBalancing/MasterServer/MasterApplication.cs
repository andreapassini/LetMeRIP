// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MasterApplication.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the MasterApplication type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime;
using System.Runtime.InteropServices;

using ExitGames.Logging;
using ExitGames.Logging.Log4Net;

using log4net;
using log4net.Config;

using Microsoft.Extensions.Configuration;

using Photon.Common.Authentication;
using Photon.Common.Authentication.Diagnostic;
using Photon.Common.LoadBalancer;
using Photon.Common.Misc;
using Photon.Hive.WebRpc;
using Photon.Hive.WebRpc.Configuration;
using Photon.LoadBalancing.Common;
using Photon.LoadBalancing.MasterServer.GameServer;
using Photon.LoadBalancing.ServerToServer.Operations;
using Photon.SocketServer;
using Photon.SocketServer.Rpc.Protocols;

using LogManager = ExitGames.Logging.LogManager;

namespace Photon.LoadBalancing.MasterServer
{
    public class MasterApplication : ApplicationBase
    {
        #region Constants and Fields

        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private WebRpcManager webRpcManager;
        #endregion

        #region Constructor Desctructor

        static MasterApplication()
        {
            LogManager.SetLoggerFactory(Log4NetLoggerFactory.Instance);
        }

        public MasterApplication()
        : this(LoadConfiguration())
        {
        }

        protected MasterApplication(IConfiguration configuration)
            : base(configuration)
        { }

        #endregion

        #region Properties

        public static ApplicationStats AppStats { get; protected set; }

        public GameServerContextManager GameServers { get; protected set; }

        public bool IsMaster => true;

        public LoadBalancer<GameServerContext> LoadBalancer { get; protected set; }

        public GameApplication DefaultApplication { get; protected set; }

        public AuthTokenFactory TokenCreator { get; protected set; }

        public CustomAuthHandler CustomAuthHandler { get; protected set; }

        private S2SCustomTypeCacheMan S2SCustomTypeCacheMan { get; set; }

        #endregion

        #region Public Methods

        public CustomTypeCache GetS2SCustomTypeCache()
        {
            return this.S2SCustomTypeCacheMan.GetCustomTypeCache();
        }

        public virtual void OnServerWentOffline(GameServerContext gameServerContext)
        {
            this.RemoveGameServerFromLobby(gameServerContext);

            if (AppStats != null)
            {
                AppStats.HandleGameServerRemoved(gameServerContext);
            }
        }

        #endregion

        #region Methods

        private static IConfiguration LoadConfiguration()
        {
            var cb = new ConfigurationBuilder();
            var cbpath = Path.GetDirectoryName(typeof(MasterApplication).Assembly.CodeBase).Remove(0, 6);
            return cb.AddXmlFile(Path.Combine(cbpath, "Master.xml.config")).Build();
        }

        protected override PeerBase CreatePeer(InitRequest initRequest)
        {
            if (this.IsGameServerPeer(initRequest))
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("Received init request from game server");
                }
                return this.CreateGameServerPeer(initRequest);
            }

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Received init request from game client on leader node");
            }

            var peer = new MasterClientPeer(initRequest);

            if (this.webRpcManager.IsRpcEnabled)
            {
                peer.WebRpcHandler = this.webRpcManager.GetWebRpcHandler();
            }

            return peer;
        }

        protected PeerBase CreateGameServerPeer(InitRequest initRequest)
        { 
            var peer = this.CreateMasterServerPeer(initRequest);

            if (initRequest.InitObject == null)
            {
                return peer;
            }

            var request = new RegisterGameServerDataContract(initRequest.Protocol, (Dictionary<byte, object>)initRequest.InitObject);
            if (!request.IsValid)
            {
                log.WarnFormat("Can not register server. Init request from {0}:{1} is invalid:{2}", initRequest.RemoteIP, initRequest.RemotePort, request.GetErrorMessage());
                return null;
            }

            this.GameServers.RegisterGameServerOnInit(request, peer);

            if (!peer.IsRegistered)
            {
                return null;
            }

            initRequest.ResponseObject = peer.GetRegisterResponse();
            return peer;
        }

        protected virtual IncomingGameServerPeer CreateMasterServerPeer(InitRequest initRequest)
        {
            return new IncomingGameServerPeer(initRequest, this);
        }

        /// <summary>
        /// method to initialize self-hosted specific stuff
        /// </summary>
        protected virtual void Initialize()
        {
            this.CustomAuthHandler = new CustomAuthHandler(new HttpRequestQueueCountersFactory(), Photon.Common.Authentication.Configuration.Auth.AuthSettings.Default.HttpQueueSettings);
            this.CustomAuthHandler.InitializeFromConfig();

            if (MasterServerSettings.Default.AppStatsPublishInterval > 0)
            {
                AppStats = new ApplicationStats(MasterServerSettings.Default.AppStatsPublishInterval);
            }
        }

        protected virtual bool IsGameServerPeer(InitRequest initRequest)
        {
            return initRequest.LocalPort == MasterServerSettings.Default.S2S.IncomingGameServerPeerPort;
        }

        protected override void OnStopRequested()
        {
            // in case of application restarts, we need to disconnect all GS peers to force them to reconnect. 
            if (log.IsInfoEnabled)
            {
                log.InfoFormat("OnStopRequested... going to disconnect {0} GS peers", this.GameServers?.Count ?? 0);
            }

            // copy to prevent changes of the underlying enumeration
            if (this.GameServers != null)
            {
                var gameServers = this.GameServers.GameServerPeersToArray();

                foreach (IncomingGameServerPeer peer in gameServers)
                {
                    if (peer != null)
                    {
                        if (log.IsDebugEnabled)
                        {
                            log.DebugFormat("Disconnecting GS peer {0}:{1}", peer.RemoteIP, peer.RemotePort);
                        }

                        peer.Disconnect(ErrorCodes.Ok);
                    }
                }
            }
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

            int expirationTimeSeconds = Photon.Common.Authentication.Settings.Default.AuthTokenExpirationSeconds;
            //if (expirationTimeSeconds <= 0)
            //{
            //    log.ErrorFormat("Authentication token expiration to low: expiration={0} seconds", expirationTimeSeconds);
            //}

            var expiration = TimeSpan.FromSeconds(expirationTimeSeconds);
            this.TokenCreator = GetAuthTokenFactory();
            this.TokenCreator.Initialize(sharedKey, hmacKey, expiration, "MS:" + Environment.MachineName);

            log.InfoFormat("TokenCreator initialized with an expiration of {0}", expiration);
        }

        protected virtual AuthTokenFactory GetAuthTokenFactory()
        {
            return new AuthTokenFactory();
        }

        protected override void Setup()
        {
            InitLogging();

            this.S2SCustomTypeCacheMan = new S2SCustomTypeCacheMan();

            var env = new Dictionary<string, object>
            {
                {"AppId", this.HwId},
                {"AppVersion", ""},
                {"Region", ""},
                {"Cloud", ""},
            };

            var settings = WebRpcSettings.Default;
            var webRpcEnabled = (settings != null && settings.Enabled);
            var baseUrlString = webRpcEnabled ? settings.BaseUrl : string.Empty;

            this.webRpcManager = new WebRpcManager(webRpcEnabled, baseUrlString, env, settings.HttpQueueSettings);

            GCSettings.LatencyMode = CommonSettings.Default.GCLatencyMode;

            log.InfoFormat("Master server initialization started");

            Protocol.AllowRawCustomValues = true;
            Protocol.RegisterTypeMapper(new UnknownTypeMapper());
            this.SetUnmanagedDllDirectory();

            this.SetupTokenCreator();

            if (CommonSettings.Default.EnablePerformanceCounters)
            {
                this.InitCorePerformanceCounters();
                CustomAuthResultCounters.Initialize();
            }
            else
            {
                log.Info("Performance counters are disabled");
            }

            this.GameServers = new GameServerContextManager(this, MasterServerSettings.Default.S2S.GSContextTTL);
            this.LoadBalancer = new LoadBalancer<GameServerContext>(Path.Combine(this.ApplicationRootPath, "LoadBalancer.config"));

            this.DefaultApplication = new GameApplication("{Default}", "{Default}", this.LoadBalancer);

            this.Initialize();

            log.InfoFormat("Master server initialization finished");
        }

        protected virtual void InitLogging()
        {
            GlobalContext.Properties["Photon:ApplicationLogPath"] = Path.Combine(this.ApplicationRootPath, "log");
            GlobalContext.Properties["LogFileName"] = "MS" + this.ApplicationName;

#if NETSTANDARD2_0 || NETCOREAPP
            var logRepository = log4net.LogManager.GetRepository(Assembly.GetEntryAssembly());
            XmlConfigurator.ConfigureAndWatch(logRepository, new FileInfo(Path.Combine(this.BinaryPath, "log4net.config")));
#else
            XmlConfigurator.ConfigureAndWatch(new FileInfo(Path.Combine(this.BinaryPath, "log4net.config")));
#endif
        }

        protected override void TearDown()
        {
            log.InfoFormat("Master server TearDown is called. Master server stopped");
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetDllDirectory(string lpPathName);

        /// <summary>
        /// Adds a directory to the search path used to locate the 32-bit or 64-bit version 
        /// for unmanaged DLLs used in the application.
        /// </summary>
        /// <remarks>
        /// Assemblies having references to unmanaged libraries (like SqlLite) require either a
        /// 32-Bit or a 64-Bit version of the library depending on the current process.
        /// </remarks>
        private void SetUnmanagedDllDirectory()
        {
            string unmanagedDllDirectory = Path.Combine(this.BinaryPath, IntPtr.Size == 8 ? "x64" : "x86");

            //TODO do we need a non wind solution?
            if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                log.WarnFormat("Not implemented set unmanaged dll directory to path {0}", unmanagedDllDirectory);
                return;
            }
            
            bool result = SetDllDirectory(unmanagedDllDirectory);

            if (result == false)
            {
                log.WarnFormat("Failed to set unmanaged dll directory to path {0}", unmanagedDllDirectory);
            }
        }

        protected virtual void RemoveGameServerFromLobby(GameServerContext gameServerContext)
        {
            this.DefaultApplication.OnGameServerRemoved(gameServerContext);
        }

        #endregion
    }
}