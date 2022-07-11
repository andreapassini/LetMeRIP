using System.IO;
using ExitGames.Logging;
using Photon.LoadBalancing.MasterServer;
using Photon.LoadBalancing.MasterServer.GameServer;
using LoadBalancing.TestInterfaces;
using Microsoft.Extensions.Configuration;

namespace Photon.LoadBalancing.UnitTests.UnifiedServer.OfflineExtra.Master
{

    public class TestMasterApplication : MasterApplication, ITestMasterApplication
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        #region .ctr

        public TestMasterApplication()
        : base(LoadConfiguration())
        {

        }

        #endregion

        #region Properties

        public int OnBeginReplicationCount { get { return ((TestGameApplication)this.DefaultApplication).OnBeginReplicationCount; } }

        public int OnFinishReplicationCount { get { return ((TestGameApplication) this.DefaultApplication).OnFinishReplicationCount; } }

        public int OnStopReplicationCount { get { return ((TestGameApplication) this.DefaultApplication).OnStopReplicationCount; } }

        public int OnServerWentOfflineCount { get; private set; }

        #endregion

        #region Public

        public override void OnServerWentOffline(GameServerContext gameServerContext)
        {
            base.OnServerWentOffline(gameServerContext);
            ++this.OnServerWentOfflineCount;
        }

        public void ResetStats()
        {
            this.OnServerWentOfflineCount = 0;
            ((TestGameApplication) this.DefaultApplication).ResetStats();
            log.DebugFormat("Stats are reset");
        }
        #endregion

        #region Privates

        protected override void Initialize()
        {
            base.Initialize();

            this.DefaultApplication = new TestGameApplication("{Default}", "{Default}", this.LoadBalancer);
        }

        private static IConfiguration LoadConfiguration()
        {
            var cb = new ConfigurationBuilder();
            var cbpath = Path.GetDirectoryName(typeof(TestMasterApplication).Assembly.CodeBase).Remove(0, 6);
            return cb.AddXmlFile(Path.Combine(cbpath, "LoadBalancing.UnitTests.xml.config")).Build();
        }

        #endregion
    }
}
