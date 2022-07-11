using System.IO;

using LoadBalancing.TestInterfaces;

using Microsoft.Extensions.Configuration;

using Photon.LoadBalancing.GameServer;

namespace Photon.LoadBalancing.UnitTests.UnifiedServer.OfflineExtra
{
    public class TestApplication : GameApplication, ITestGameServerApplication
    {
        public override GameCache GameCache { get; protected set; }

        public TestApplication()
        : base(LoadConfiguration())
        {
        }

        protected override void Setup()
        {
            base.Setup();
            this.GameCache = new TestGameCache(this);
        }

        public bool TryGetRoomWithoutReference(string roomId, out TestGameWrapper game)
        {
            game = null;
            if (!this.GameCache.TryGetRoomWithoutReference(roomId, out var room))
            {
                return false;
            }

            game = new TestGameWrapper((TestGame) room);
            return true;
        }

        public bool WaitGameDisposed(string gameName, int timeout)
        {
            if (!this.GameCache.TryGetRoomWithoutReference(gameName, out var room))
            {
                return true;
            }

            return ((TestGame) room).WaitForDispose(timeout);
        }

        public void SetGamingTcpPort(int port)
        {
            this.GamingTcpPort = port;
        }

        public int RoomOptionsAndFlags
        {
            get => GameServerSettings.Default.RoomOptionsAndFlags;
            set => GameServerSettings.Default.RoomOptionsAndFlags = value;
        }
        public int RoomOptionsOrFlags
        {
            get => GameServerSettings.Default.RoomOptionsOrFlags;
            set => GameServerSettings.Default.RoomOptionsOrFlags = value;
        }

        private static IConfiguration LoadConfiguration()
        {
            var cb = new ConfigurationBuilder();
            var cbpath = Path.GetDirectoryName(typeof(TestApplication).Assembly.CodeBase).Remove(0, 6);
            return cb.AddXmlFile(Path.Combine(cbpath, "LoadBalancing.UnitTests.xml.config")).Build();
        }

    }
}
