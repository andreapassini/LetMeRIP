// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PhotonApp.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the PhotonApp type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime;

using ExitGames.Configuration;
using ExitGames.Logging;
using ExitGames.Logging.Log4Net;

using log4net;
using log4net.Config;

using Microsoft.Extensions.Configuration;

using Photon.Common.Authentication;
using Photon.Common.Authentication.Diagnostic;
using Photon.Common.Misc;
using Photon.NameServer.Configuration;
using Photon.SocketServer;

using LogManager = ExitGames.Logging.LogManager;

namespace Photon.NameServer
{
    public class PhotonApp : ApplicationBase
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        public AuthTokenFactory TokenCreator { get; private set; }
        public CustomAuthHandler CustomAuthHandler { get; private set; }

        private FileSystemWatcher fileWatcher;

        public MasterServerCache ServerCache { get; private set; }

        // only dump config information into this file for debugging
        private const string serverConfigFile = "ServerConfig.xml";

        static PhotonApp()
        {
            LogManager.SetLoggerFactory(Log4NetLoggerFactory.Instance);
        }
        public PhotonApp()
            : this(LoadConfiguration())
        { }

        protected PhotonApp(IConfiguration configuration)
        : base(configuration)
        { }

        private static IConfiguration LoadConfiguration()
        {
            var cb = new ConfigurationBuilder();

            var cbpath = Path.GetDirectoryName(typeof(PhotonApp).Assembly.CodeBase).Remove(0, 6);
            return cb.AddXmlFile(Path.Combine(cbpath, "NameServer.xml.config")).Build();
        }

        protected override PeerBase CreatePeer(InitRequest initRequest)
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Received init request: conId={0}, endPoint={1}:{2}", initRequest.ConnectionId, initRequest.LocalIP, initRequest.LocalPort);
            }
            
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Create ClientPeer");
            }

            return new ClientPeer(this, initRequest);
        }

        protected override void Setup()
        {
            this.SetupLog4net();

            WebRequest.DefaultWebProxy = null;

            log.Info("Initializing ...");

            log.Info("ServicePointManager.DefaultConnectionLimit=" + ServicePointManager.DefaultConnectionLimit);

            this.SetupTokenCreator();

            AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;

            Protocol.RegisterTypeMapper(new UnknownTypeMapper());

            if (Settings.Default.EnablePerformanceCounters)
            {
                this.InitCorePerformanceCounters();
                CustomAuthResultCounters.Initialize();
            }
            else
            {
                log.Info("Performance counters are disabled");
            }

            // load nameserver config & initialize file watcher
            if (!string.IsNullOrEmpty(this.ApplicationRootPath) && Directory.Exists(this.ApplicationRootPath))
            {
                this.fileWatcher = new FileSystemWatcher(this.ApplicationRootPath, Settings.Default.NameServerConfig);
                this.fileWatcher.Changed += this.ConfigFileChanged;
                this.fileWatcher.Created += this.ConfigFileChanged;
                this.fileWatcher.Renamed += this.ConfigFileChanged;
                this.fileWatcher.EnableRaisingEvents = true;
            }

            string message;
            if (!this.ReadNameServerConfigurationFile(out message))
            {
                log.Error(message);
                throw new ConfigurationException(message);
            }

            this.Initialize();
        }

        protected virtual void SetupLog4net()
        {
            // Rolling Logfile Appender: 
            GlobalContext.Properties["Photon:ApplicationLogPath"] = Path.Combine(this.ApplicationRootPath, "log");
            GlobalContext.Properties["Photon:UnmanagedLogDirectory"] = this.UnmanagedLogPath;
            GlobalContext.Properties["LogFileName"] = this.ApplicationName;

#if NETSTANDARD2_0 || NETCOREAPP
            var logRepository = log4net.LogManager.GetRepository(Assembly.GetEntryAssembly());
            XmlConfigurator.ConfigureAndWatch(logRepository, new FileInfo(Path.Combine(this.BinaryPath, "log4net.config")));
#else
            XmlConfigurator.ConfigureAndWatch(new FileInfo(Path.Combine(this.BinaryPath, "log4net.config")));
#endif
        }

        protected virtual void Initialize()
        {
            this.CustomAuthHandler = new CustomAuthHandler(new HttpRequestQueueCountersFactory(), Common.Authentication.Configuration.Auth.AuthSettings.Default.HttpQueueSettings);
            this.CustomAuthHandler.InitializeFromConfig();
        }

        private static void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs unhandledExceptionEventArgs)
        {
            log.ErrorFormat("Unhandled exception. IsTerminating={0}, exception:{1}",
                unhandledExceptionEventArgs.IsTerminating, unhandledExceptionEventArgs.ExceptionObject);
        }

        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1408:ConditionalExpressionsMustDeclarePrecedence", Justification = "Reviewed. Suppression is OK here.")]
        protected virtual bool ReadNameServerConfigurationFile(out string message)
        {
            string filename = Path.Combine(this.ApplicationRootPath, Settings.Default.NameServerConfig);

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Reading Name Server cofig file from {0}", filename);
            }

            List<Node> config;
            if (!ConfigurationLoader.TryLoadFromFile(filename, out config, out message))
            {
                message = string.Format("Could not initialize Name Server list from configuration: Invalid configuration file {0}. Error: {1}", filename, message);

                return false;
            }

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Updating Master Server Cache.");
            }
            
            this.ServerCache = new MasterServerCache(config);

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Master Server Cache update done.");
            }

            return true; 
        }

        private void ConfigFileChanged(object sender, FileSystemEventArgs e)
        {
            //no need to reload changes if we rename original NameServer.json to something else, because anyway file will be missing
            //UNIX: tolower may cause problems on *nix
            if (e.ChangeType == WatcherChangeTypes.Renamed && e.Name.ToLower() != GetNameServerConfig().ToLower())
            {
                log.InfoFormat("NameServer config was renamed. No changes applied, staying on previous version");
                return;
            }

            string message; 
            if (this.ReadNameServerConfigurationFile(out message))
            {
                log.InfoFormat("Parsed NameServer config successfully. PhotonApp");
            }
            else
            {
                log.WarnFormat("Could not parse NameServer config. No changes are applied to the existing MasterServerDispatcher. {0}",message);
            }
        }
        
        protected override void TearDown()
        {
        }

        private void SetupTokenCreator()
        {
            var sharedKey = Photon.Common.Authentication.Settings.Default.AuthTokenKey;
            if (string.IsNullOrEmpty(sharedKey))
            {
                log.Warn("AuthTokenKey not specified in config");
            }

            var hmacKey = Photon.Common.Authentication.Settings.Default.HMACTokenKey;
            if (string.IsNullOrEmpty(hmacKey))
            {
                log.Warn("HMACTokenKey not specified in config");
            }

            var expirationTimeSeconds = Photon.Common.Authentication.Settings.Default.AuthTokenExpirationSeconds;
            if (expirationTimeSeconds <= 0)
            {
                log.ErrorFormat("Authentication token expiration to low: expiration={0} seconds", expirationTimeSeconds);
            }

            var expiration = TimeSpan.FromSeconds(expirationTimeSeconds);
            this.TokenCreator = GetAuthTokenFactory();
            this.TokenCreator.Initialize(sharedKey, hmacKey, expiration, "NS:" + Environment.MachineName);

            log.InfoFormat("TokenCreator initialized with an expiration of {0}", expiration);
        }

        protected virtual AuthTokenFactory GetAuthTokenFactory()
        {
            return new AuthTokenFactory();
        }

        protected static string GetNameServerConfig()
        {
            return Settings.Default.NameServerConfig;
        }
    }
}
