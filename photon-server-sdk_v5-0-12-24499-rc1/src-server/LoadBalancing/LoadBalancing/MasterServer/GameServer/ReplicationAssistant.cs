using ExitGames.Logging;

namespace Photon.LoadBalancing.MasterServer.GameServer
{
    public class ReplicationAssistant : ReplicationAssistantBase
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();
        private readonly GameApplication application;

        #region .ctr

        public ReplicationAssistant(GameServerContext gameServerContext, GameApplication app)
            : base(gameServerContext)
        {
            this.application = app;
            if (log.IsInfoEnabled)
            {
                log.InfoFormat("Replication started for context:{0}", gameServerContext);
            }
            this.application.OnBeginReplication(this.gameServerContext);
        }

        #endregion

        #region Privates

        protected override void OnReplicationFinished()
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("OnReplicationFinsihed for server: {0}", this.gameServerContext);
            }
            this.application.OnFinishReplication(this.gameServerContext);
        }

        protected override void OnStopReplication()
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("OnStopReplication for server: {0}", this.gameServerContext);
            }
            this.application.OnStopReplication(this.gameServerContext);
        }

        #endregion

    }
}
