using ExitGames.Client.Photon;
using NUnit.Framework;
using Photon.LoadBalancing.UnitTests.UnifiedServer;
using Photon.LoadBalancing.UnitTests.UnifiedServer.Policy;
using Photon.LoadBalancing.UnitTests.UnifiedTests;

namespace Photon.LoadBalancing.UnitTests.Online
{
    [TestFixture("TokenAuth", AuthPolicy.AuthOnNameServer, ConnectionProtocol.Udp)]
    [TestFixture("TokenAuth", AuthPolicy.UseAuthOnce, ConnectionProtocol.Tcp)]
    [TestFixture("TokenAuth", ConnectionProtocol.WebSocket)]
    public class LoadbalancingOnlineTests : LBApiTestsImpl
    {
        public LoadbalancingOnlineTests(string schemeName )
            : this(schemeName, ConnectionProtocol.Tcp)
        { }

        public LoadbalancingOnlineTests(string schemeName, ConnectionProtocol protocol)
            :this(schemeName, AuthPolicy.AuthOnNameServer, protocol)
        {
        }

        public LoadbalancingOnlineTests(string schemeName, AuthPolicy authPolicy, ConnectionProtocol protocol)
            : base(new OnlineConnectPolicy(GetAuthScheme(schemeName), protocol), authPolicy)
        {
            if (schemeName == "TokenAuthNoUserIds")
            {
                this.Player1 = null;
                this.Player2 = null;
                this.Player3 = null;
            }
        }
    }
}