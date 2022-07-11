using Photon.SocketServer;
using Photon.SocketServer.Annotations;

namespace Photon.LoadBalancing.MasterServer
{
    [SettingsMarker("Photon:Master")]
    public sealed class MasterServerSettings
    {
        // Explicit static constructor to tell C# compiler
        // not to mark type as beforefieldinit
        static MasterServerSettings()
        { }

        public class MSS2SSettings
        {
            public int IncomingGameServerPeerPort { get; set; } = 4520;
            public int GSContextTTL { get; set; } = 20000;
        }

        public class MSGameServerSettings
        {
            public int ReplicationTimeout { get; set; } = 1800000;//30 min
        }

        public class MSLimitSettings
        {
            public class LobbySettings
            {
                public int MaxStatsPublished { get; set; } = int.MaxValue;

                public int Total { get; set; } = 10000;

                //0 is not unlimited!
                //this setting was added to not change the existing GameListLimit behaviour (used at JoinLobby, 0 is unlimited)
                public int MaxGamesOnJoin { get; set; } = 500;

                //0 is not unlimited!
                public int MaxGamesInUpdates { get; set; } = 500;

                //0 is unlimited
                public int MaxGamesInGetGamesListResponse { get; set; } = 100;

            }

            public class InboundLimitSettings
            {
                public int MaxConcurrentJoinRequests { get; set; } = 1;

                public int MaxTotalJoinRequests { get; set; } = 1;

                public int MaxJoinedGames { get; set; } = 3;

                public int MaxPropertiesSizePerRequest { get; set; } = 51000;
            }
            public LobbySettings Lobby { get; set; } = new LobbySettings();

            public InboundLimitSettings Inbound { get; set; } = new InboundLimitSettings();

        }

        #region Public Properties

        public static MasterServerSettings Default { get; } = ApplicationBase.GetConfigSectionAndValidate<MasterServerSettings>("Photon:Master");

        public MSS2SSettings S2S { get; set; } = new MSS2SSettings();

        public MSGameServerSettings GS { get; set; } = new MSGameServerSettings();

        public MSLimitSettings Limits { get; set; } = new MSLimitSettings();

        public int AppStatsPublishInterval { get; set; } = 5000;

        public int GameChangesPublishInterval { get; set; } = 1000;

        public int GameExpiryCheckPeriod { get; set; } = 1;

        public int LobbyStatsPublishInterval { get; set; } = 120;

        public int PersistentGameExpiryMinute { get; set; } = 60;

        public bool OnlyLogQueryDataErrors { get; set; } = false;

        public string SqlQueryBlockList { get; set; } =
            "ALTER;CREATE;DELETE;DROP;EXEC;EXECUTE;INSERT;INSERT INTO;MERGE;SELECT;UPDATE;UNION;UNION ALL";

        #endregion
    }
}