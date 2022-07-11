namespace Photon.LoadBalancing.MasterServer
{
    using System;
    using System.Collections.Generic;

    using ExitGames.Concurrency.Fibers;
    using ExitGames.Logging;

    using Photon.LoadBalancing.Events;
    using Photon.LoadBalancing.MasterServer.GameServer;
    using Photon.SocketServer;

    public class ApplicationStats
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();
        private readonly PoolFiber fiber;

        private readonly Dictionary<GameServerContext, GameServerApplicationState> gameServerStats 
            = new Dictionary<GameServerContext, GameServerApplicationState>();

        private readonly HashSet<MasterClientPeer> subscribers = new HashSet<MasterClientPeer>();

        private readonly int publishIntervalMs;

        private IDisposable publishSchedule;

        public ApplicationStats(int publishIntervalMs)
        {
            this.publishIntervalMs = publishIntervalMs;
            this.fiber = new PoolFiber();
            this.fiber.Start();
        }

        public int GameCount { get; private set; }

        public int PeerCount 
        { 
            get
            {
                return this.PeerCountMaster + this.PeerCountGameServer;
            } 
        }

        public int PeerCountGameServer { get; private set; }

        public int PeerCountMaster { get; private set; }

        public void IncrementMasterPeerCount()
        {
            this.fiber.Enqueue(() => this.OnUpdateMasterPeerCount(1));
        }

        public void DecrementMasterPeerCount()
        {
            this.fiber.Enqueue(() => this.OnUpdateMasterPeerCount(-1));
        }

        public void UpdateGameServerStats(GameServerContext gameServerContext, int peerCount, int gameCount)
        {
            this.fiber.Enqueue(() => this.OnUpdateGameServerStats(gameServerContext, peerCount, gameCount));
        }

        public void HandleGameServerRemoved(GameServerContext gameServerContext)
        {
            this.fiber.Enqueue(() => this.OnGameServerRemoved(gameServerContext));
        }

        public void AddSubscriber(MasterClientPeer peer)
        {
            this.fiber.Enqueue(() => this.OnAddSubscriber(peer));
        }

        public void RemoveSubscriber(MasterClientPeer peer)
        {
            this.fiber.Enqueue(() => this.OnRemoveSubscriber(peer));
        }

        private void OnUpdateMasterPeerCount(int diff)
        {
            this.PeerCountMaster += diff;
            this.OnStatsUpdated();
        }

        private void OnUpdateGameServerStats(GameServerContext gameServerContext, int peerCount, int gameCount)
        {
            GameServerApplicationState stats;
            if (this.gameServerStats.TryGetValue(gameServerContext, out stats) == false)
            {
                stats = new GameServerApplicationState();
                this.gameServerStats.Add(gameServerContext, stats);
            }

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Updating game server stats. new values peerCount={0}, gameCount={1}", peerCount, gameCount);
            }

            int playerDiff = peerCount - stats.PlayerCount;
            int gameDiff = gameCount - stats.GameCount;
            this.PeerCountGameServer += playerDiff;
            this.GameCount += gameDiff;
            stats.PlayerCount = peerCount;
            stats.GameCount = gameCount;

            if (playerDiff != 0 || gameDiff != 0)
            {
                this.OnStatsUpdated();
            }
        }

        private void OnGameServerRemoved(GameServerContext gameServerContext)
        {
            GameServerApplicationState stats;
            if (this.gameServerStats.TryGetValue(gameServerContext, out stats) == false)
            {
                return;
            }

            this.gameServerStats.Remove(gameServerContext);
            this.PeerCountGameServer -= stats.PlayerCount;
            this.GameCount += stats.GameCount;

            if (stats.PlayerCount != 0 || stats.GameCount != 0)
            {
                if (this.PeerCountGameServer < 0 || this.GameCount < 0)
                {
                    log.WarnFormat("Negative stats values PeerCountGameServer:{0}, GameCount:{1}", this.PeerCountGameServer, this.GameCount);
                    this.PeerCountGameServer = 0;
                    this.GameCount = 0;
                }
                this.OnStatsUpdated();
            }
        }

        private void OnStatsUpdated()
        {
            if (this.subscribers.Count == 0)
            {
                return;
            }

            if (this.publishSchedule != null)
            {
                return;
            }

            this.publishSchedule = this.fiber.Schedule(this.PublishApplicationStats, this.publishIntervalMs);
        }

        private void OnAddSubscriber(MasterClientPeer subscriber)
        {
            this.subscribers.Add(subscriber);
            EventData eventData = this.GetAppStatsEventData();
            subscriber.SendEvent(eventData, new SendParameters { Unreliable = true });
        }

        private void OnRemoveSubscriber(MasterClientPeer subscriber)
        {
            this.subscribers.Remove(subscriber);
        }

        private void PublishApplicationStats()
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Publishing application stats. MasterPeerCount={0}, PlayerCount={1}, GameCount={2}",
                    this.PeerCountMaster, this.PeerCountGameServer, this.GameCount);
            }
            this.publishSchedule.Dispose();
            this.publishSchedule = null;

            EventData eventData = this.GetAppStatsEventData();
            eventData.SendTo(this.subscribers, new SendParameters { Unreliable = true });
        }

        private EventData GetAppStatsEventData()
        {
            var e = new AppStatsEvent
            {
                MasterPeerCount = this.PeerCountMaster,
                PlayerCount = this.PeerCountGameServer,
                GameCount = this.GameCount
            };

            return new EventData((byte)EventCode.AppStats, e);
        }

        private sealed class GameServerApplicationState
        {
            public int GameCount { get; set; }

            public int PlayerCount { get; set; }
        }
    }
}
