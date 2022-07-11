using System;
using Photon.Hive.Common;
using Photon.SocketServer;
using Photon.SocketServer.Annotations;

namespace Photon.LoadBalancing.GameServer
{
    [SettingsMarker("Photon:GameServer")]
    public class GameServerSettings
    {
        // Explicit static constructor to tell C# compiler
        // not to mark type as beforefieldinit
        static GameServerSettings()
        {}

        public class GSS2SSettings
        {
            public string MasterIPAddress { get; set; } = "127.0.0.1";

            public int OutgoingMasterServerPeerPort { get; set; } = 4520;

            public int ConnectRetryInterval { get; set; } = 15;

            public int StopNotificationTimeout { get; set; } = 1000;

        }

        public class GSMasterSettings
        {
            private string publicHostName;

            public int AppStatsPublishInterval { get; set; } = 1000;

            public bool UseGameUpdatesBatcher { get; set; } = true;

            public int MaxGameUpdatesToBatch { get; set; } = 500;

            public int BatchPeriod { get; set; } = 100;

            public byte LoadBalancerPriority { get; set; } = 0;

            public int GamingSecureWebSocketPort { get; set; } = 19091;

            public int GamingTcpPort { get; set; } = 4531;

            public int GamingUdpPort { get; set; } = 5056;

            public int GamingWebSocketPort { get; set; } = 9091;

            public int GamingWebRTCPort { get; set; } = 7072;

            public string GamingWsPath { get; set; } = "";

            public string PublicIPAddress { get; set; } = "127.0.0.1";

            public string PublicIPAddressIPv6 { get; set; } = null;

            public string PublicHostName
            {
                get => this.publicHostName;
                set => this.publicHostName = value != null ? Environment.ExpandEnvironmentVariables(value) : null;
            }

            public string SupportedProtocols { get; set; } = "";

        }

        public class GSLimitSettings
        {
            public class InboundLimitSettings
            {
                public class EventCacheSettings
                {
                    public int SlicesCount { get; set; } = 1000;

                    public int EventsCount { get; set; } = 50000;

                    public int ActorEventsCount { get; set; } = 50000;
                }

                public class PropertiesLimitsSettings
                {
                    /// <summary>
                    /// Limits amount of uniq properties that can be set by one peer. well known are included
                    /// </summary>
                    public int MaxUniqPropertyKeysPerPeer { get; set; } = 1000;

                    /// <summary>
                    /// Limits total size of properties in game
                    /// </summary>
                    public int MaxPropertiesSizePerGame { get; set; } = 51000;

                    /// <summary>
                    /// Limit number of properties in one request
                    /// </summary>
                    public int MaxPropertiesPerRequest { get; set; } = 100;

                    /// <summary>
                    /// limit for size of all properties in one request
                    /// </summary>
                    public int MaxPropertiesSizePerRequest { get; set; } = 10000;
                }

                public class OperationSettings
                {
                    public int SetPropertiesRate { get; set; } = 5;

                    public int GetPropertiesRate { get; set; } = 2;

                    public int JoinGameRate { get; set; } = 2;

                    public int CreateGameRate { get; set; } = 2;

                    public int PingRate { get; set; } = 1;

                    public int ChangeGroupsRate { get; set; } = 1;

                    public int DebugGameRate { get; set; } = 1;

                    public int RpcRate { get; set; } = 10;

                    public int SettingsRate { get; set; } = 1;
                }

                public EventCacheSettings EventCache { get; set; } = new EventCacheSettings();

                public PropertiesLimitsSettings Properties { get; set; } = new PropertiesLimitsSettings();

                public OperationSettings Operations { get; set; } = new OperationSettings();
            }

            public int HttpForwardLimit { get; set; } = 10000;

            public InboundLimitSettings Inbound { get; set; } = new InboundLimitSettings();
        }

        #region Public Properties

        public static GameServerSettings Default { get; } = ApplicationBase.GetConfigSectionAndValidate<GameServerSettings>("Photon:GameServer");

        public GSS2SSettings S2S { get; set; } = new GSS2SSettings();

        public GSMasterSettings Master { get; set; } = new GSMasterSettings();

        public GameHttpQueueSettings HttpQueueSettings { get; set; } = new GameHttpQueueSettings();

        public GSLimitSettings Limits { get; set; } = new GSLimitSettings();

        public bool EnableNamedPipe { get; set; } = false;

        public int LastTouchSecondsDisconnect { get; set; } = 0;

        public int MaxEmptyRoomTTL { get; set; } = 60000;

        public string ServerStateFile { get; set; } = "ServerState.txt";

        public string WorkloadConfigFile { get; set; } = "Workload.config";

        public string PredictionConfigFile { get; set; } = "Prediction.config";

        public int PredictionStatsSaveIntervalSeconds { get; set; } = 60;

        public float PredictionFactor { get; set; } = 1.0f;

        public int JoinErrorCountToReinitialize { get; set; } = 10;

        public int InactivityTimeoutHours { get; set; } = 24; //defines time in hours

        public bool TokenCheckExpectedHostAndGame { get; set; } = true;


        /// <summary>
        /// we use this and next setting to control what room option flags can be used
        /// by client. now server is able to control what flags can be set or reset
        /// by default this options do not have any influence
        /// </summary>
        public int RoomOptionsAndFlags { get; set; } = -1;

        public int RoomOptionsOrFlags { get; set; }

        #endregion
    }
}