using ExitGames.Logging;

using Photon.LoadBalancing.ServerToServer.Events;

namespace Photon.LoadBalancing.MasterServer.GameServer
{
    public abstract class ReplicationAssistantBase
    {
        #region Fields

        private static readonly ILogger log = LogManager.GetCurrentClassLogger();
        private int countOfUpdates;
        private int countOfExpectedUpdates = -1;// this allows replication to be counted even before setting of this value from GS
        protected readonly GameServerContext gameServerContext;

        #endregion

        #region .ctr

        protected ReplicationAssistantBase(GameServerContext context)
        {
            this.gameServerContext = context;
        }

        #endregion

        #region Propeties

        public bool IsReplicationComplete
        {
            get { return this.countOfExpectedUpdates == this.countOfUpdates; }
        }

        #endregion

        #region Publics

        public void HandleReplicationHelperEvent(ReplicationHelperEvent replicationHelperEvent)
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Handling of ReplicationHelperEvent event. replicated count:{0},expected count:{1}. context:{2}", 
                    this.countOfUpdates, replicationHelperEvent.ExpectedGamesCount, this.gameServerContext);
            }

            if (this.countOfUpdates == -1)
            {
                //we forcibly finished replication
                log.WarnFormat("Got ReplicationHelperEvent after forcibly closed replication, event count:{0}, context:{1}",
                    replicationHelperEvent.ExpectedGamesCount, this.gameServerContext);
                return;
            }

            this.countOfExpectedUpdates = replicationHelperEvent.ExpectedGamesCount;
            if (this.countOfExpectedUpdates == 0 || this.IsReplicationComplete)
            {
                this.OnReplicationFinished();
            }
        }

        public void OnGameUpdateEventHandled(UpdateGameEvent updateGameEvent)
        {
            if (this.IsReplicationComplete)
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("Replication complete. Got event:{0}", Newtonsoft.Json.JsonConvert.SerializeObject(updateGameEvent));
                }

                if (updateGameEvent.IsReplicationEvent)
                {
                    var msg =
                        updateGameEvent.IsEmptyRoomReplication ? $"Got EMPTY game replication when " : $"Got game replication when " +
                        $"replication finished. GameId:{updateGameEvent.GameId}" +
                        $"AppId:{updateGameEvent.ApplicationId}/{updateGameEvent.ApplicationVersion}" +
                        $" got replicas:{this.countOfUpdates}, expected replicas:{this.countOfExpectedUpdates}, context:{this.gameServerContext}";

                    log.Error(msg);
                }

                return;
            }

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Context got game update. ReplicationCode:{2}, gameId:{3}, json:{4}, replicated count:{0}, context:{1}",
                    this.countOfUpdates, this.gameServerContext, updateGameEvent.Replication, updateGameEvent.GameId,
                    Newtonsoft.Json.JsonConvert.SerializeObject(updateGameEvent));
            }

            if (updateGameEvent.IsReplicationEvent)
            {
                ++this.countOfUpdates;

                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("Context got game replica. replicated count:{0}, context:{1}", this.countOfUpdates, this.gameServerContext);
                }

                if (this.IsReplicationComplete)
                {
                    this.OnReplicationFinished();

                    if (log.IsInfoEnabled)
                    {
                        log.InfoFormat("Replication finsihed for server: {0}", this.gameServerContext);
                    }
                }
            }
        }

        public void Stop()
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Stopping game replication from server {0}", this.gameServerContext);
            }

            if (!this.IsReplicationComplete)
            {
                this.OnStopReplication();

                if (log.IsInfoEnabled)
                {
                    log.InfoFormat("Stopped game replication from server {0}", this.gameServerContext);
                }
            }
            else
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("Stopping skipped. No replication in progress for server {0}", this.gameServerContext);
                }
            }
        }

        public void ForceFinishReplication()
        {
            this.FinishReplication();

            log.Warn($"Replication forcibly finished for server.: got replicas: {this.countOfUpdates}, expected replicas: {this.countOfExpectedUpdates} ctx:{this.gameServerContext}");
        }
        #endregion

        #region Privates

        protected abstract void OnReplicationFinished();
        protected abstract void OnStopReplication();

        private void FinishReplication()
        {
            this.OnReplicationFinished();

            this.countOfExpectedUpdates = this.countOfUpdates = -1;
        }

        #endregion
    }
}