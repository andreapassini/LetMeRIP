
using System;
using Photon.Common.LoadBalancer;
using Photon.LoadBalancing.MasterServer;
using Photon.LoadBalancing.MasterServer.GameServer;

namespace Photon.LoadBalancing.UnitTests.UnifiedServer.OfflineExtra.Master
{
    [Serializable]
    public class TestGameApplication : GameApplication
    {
        public TestGameApplication(string applicationId, string version, LoadBalancer<GameServerContext> loadBalancer) 
            : base(applicationId, version, loadBalancer)
        {
        }

        #region Properties

        public int OnBeginReplicationCount { get; set; }

        public int OnFinishReplicationCount { get; set; }

        public int OnStopReplicationCount { get; set; }
        #endregion

        #region Publics

        public override void OnBeginReplication(GameServerContext gameServerContext)
        {
            ++this.OnBeginReplicationCount;
            base.OnBeginReplication(gameServerContext);
        }

        public override void OnFinishReplication(GameServerContext gameServerContext)
        {
            ++this.OnFinishReplicationCount;
            base.OnFinishReplication(gameServerContext);
        }

        public override void OnStopReplication(GameServerContext gameServerContext)
        {
            ++this.OnStopReplicationCount;
            base.OnStopReplication(gameServerContext);
        }

        public void ResetStats()
        {
            this.OnBeginReplicationCount = 0;

            this.OnFinishReplicationCount = 0;

            this.OnStopReplicationCount = 0;
        }

        #endregion
    }
}
