using NUnit.Framework;
using Photon.LoadBalancing.UnitTests.UnifiedServer;
using Photon.LoadBalancing.UnitTests.UnifiedServer.Policy;
using Photon.LoadBalancing.UnitTests.UnifiedTests;

namespace Photon.LoadBalancing.UnitTests.Offline
{
    [TestFixture("TokenAuth", AuthPolicy.AuthOnNameServer)]
    [TestFixture("TokenAuth", AuthPolicy.AuthOnMaster)]
    [TestFixture("TokenAuth", AuthPolicy.UseAuthOnce)]
    [TestFixture("TokenAuthNoUserIds")]
    public class LoadBalancingOfflineTests : LBApiTestsImpl
    {
        public LoadBalancingOfflineTests(string schemeName)
            : this(schemeName, AuthPolicy.AuthOnNameServer)
        {
        }

        public LoadBalancingOfflineTests(string schemeName, AuthPolicy authPolicy)
            : base(new OfflineConnectPolicy(GetAuthScheme(schemeName)), authPolicy)
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