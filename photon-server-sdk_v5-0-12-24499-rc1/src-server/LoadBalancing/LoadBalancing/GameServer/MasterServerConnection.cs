using System.Collections.Generic;
using ExitGames.Logging;
using Photon.Hive.Caching;

namespace Photon.LoadBalancing.GameServer
{
    using System.Threading;
    using Photon.Hive;
    using Photon.Hive.Messages;
    using Photon.LoadBalancing.Operations;
    using Photon.LoadBalancing.ServerToServer.Events;
    using Photon.SocketServer;

    public class MasterServerConnection : MasterServerConnectionBase
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private ManualResetEvent leaveEventResponse;

        public MasterServerConnection(GameApplication controller, string address, int port, int connectRetryIntervalSeconds)
            : base(controller, address, port, connectRetryIntervalSeconds)
        {
        }

        public void RemoveGameState(string gameId, byte closeReason)
        {
            var masterPeer = this.GetPeer();
            if (masterPeer == null || masterPeer.IsRegistered == false)
            {
                return;
            }

            var parameter = new Dictionary<byte, object> 
            {
                {(byte)ParameterCode.GameId, gameId }, 
                {(byte)ParameterCode.GameRemoveReason, closeReason }, 
            };
            var eventData = new EventData { Code = (byte)ServerEventCode.RemoveGameState, Parameters = parameter };
            masterPeer.SendEvent(eventData, new SendParameters());
        }

        public override void UpdateAllGameStates()
        {
            var masterPeer = this.GetPeer();
            if (masterPeer == null || masterPeer.IsRegistered == false)
            {
                return;
            }

            int gamesCount;
            InitiateGamesReplication(this.Application.GameCache, "GlobalGamesList", out gamesCount);

            this.SendReplicationHelperEvent(gamesCount);
        }

        protected static void InitiateGamesReplication(RoomCacheBase gameCache, string appId, out int gamesCount)
        {
            var list = gameCache.GetRoomNames();
            var roomRefList = new List<Room>(list.Count);

            foreach (var gameId in list)
            {
                Room room;
                if (gameCache.TryGetRoomWithoutReference(gameId, out room))
                {
                    roomRefList.Add(room);
                }
            }

            foreach (var room in roomRefList)
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("Sending ReinitializeGameStateOnMaster notification to: '{0}' {1}", room.ToString(), appId);
                }

                room.EnqueueMessage(new RoomMessage((byte) GameMessageCodes.ReinitializeGameStateOnMaster));
            }

            gamesCount = roomRefList.Count;
        }


        protected void SendReplicationHelperEvent(int gamesCount)
        {
            var request = new EventData((byte) ServerEventCode.ExpectedGamesCount)
            {
                Parameters = new Dictionary<byte, object>
                {
                    {(byte)ParameterCode.GameCount, gamesCount}
                }
            };

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Update Expected Games Count event sent. endpoint:{0}, connectionId:{1}", this.EndPoint, this.ConnectionId);
            }
            var peer = this.GetPeer();
            peer.SendEvent(request, new SendParameters());
        }

        protected override OutgoingMasterServerPeer CreateServerPeer()
        {
            return new OutgoingMasterServerPeer(this);
        }

        public void SendLeaveEventAndWaitForResponse(int timeout)
        {
            this.leaveEventResponse = new ManualResetEvent(false);
            using (this.leaveEventResponse)
            {
                this.SendEvent(new EventData((byte)ServerEventCode.GameServerLeave), new SendParameters());
                this.leaveEventResponse.WaitOne(timeout);
            }
        }
    }
}
