using System;
using System.Collections.Generic;
using System.Threading;
using Photon.Hive;
using Photon.Hive.Caching;
using Photon.Hive.Operations;
using Photon.Hive.Plugin;
using Photon.LoadBalancing.GameServer;
using SendParameters = Photon.SocketServer.SendParameters;

namespace Photon.LoadBalancing.UnitTests.UnifiedServer.OfflineExtra
{
    public class TestGameWrapper : MarshalByRefObject
    {
        private readonly TestGame game;
        public TestGameWrapper(TestGame game)
        {
            this.game = game;
        }

        public int ActorsCount { get { return this.game.ActorsManager.Count; } }
    }

    public class TestGame : Game
    {
        private readonly AutoResetEvent onDispose = new AutoResetEvent(false);

        public TestGame(GameApplication application, string gameId, RoomCacheBase roomCache = null, 
            IPluginManager pluginManager = null, Dictionary<string, object> environment = null) 
            : base(new LBGameCreateOptions(application, gameId, roomCache, pluginManager, environment))
        {
        }

        protected override bool ProcessBeforeJoinGame(JoinGameRequest joinRequest, SendParameters sendParameters, HivePeer peer)
        {
            if (joinRequest.ActorProperties != null && joinRequest.ActorProperties.ContainsKey("ProcessBeforeJoinException"))
            {
                peer = null;
                joinRequest.CacheSlice = 123;
            }
            return base.ProcessBeforeJoinGame(joinRequest, sendParameters, peer);
        }

        protected override bool ProcessBeforeSetProperties(HivePeer peer, SetPropertiesRequest request, SendParameters sendParameters)
        {
            var value = (string)request.Properties["ActorProperty"];
            if (value == "BeforeSetPropertiesExceptionInContinue")
            {
                peer = null;
                request.TargetActor = null;
                request.ActorNumber = 1;
            }
            return base.ProcessBeforeSetProperties(peer, request, sendParameters);
        }

        protected override bool ProcessSetProperties(HivePeer peer, bool result, string errorMsg, SetPropertiesRequest request, SendParameters sendParameters)
        {
            var value = (string)request.Properties["ActorProperty"];
            if (value == "OnSetPropertiesExceptionInContinue")
            {
                request = null;
            }

            return base.ProcessSetProperties(peer, result, errorMsg, request, sendParameters);
        }

        protected override void Dispose(bool dispose)
        {
            base.Dispose(dispose);
            this.onDispose.Set();
        }

        public bool WaitForDispose(int timeout)
        {
            return this.onDispose.WaitOne(timeout);
        }
    }
}
