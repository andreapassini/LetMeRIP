using System.IO;

using Microsoft.Extensions.Configuration;

namespace Photon.LoadBalancing.UnitTests.UnifiedServer.OfflineExtra
{
    public class NameServerTestsApplication : Photon.NameServer.PhotonApp
    {
        public NameServerTestsApplication()
            : base(LoadConfiguration())
        {

        }
        private static IConfiguration LoadConfiguration()
        {
            var cb = new ConfigurationBuilder();

            var cbpath = Path.GetDirectoryName(typeof(NameServerTestsApplication).Assembly.CodeBase).Remove(0, 6);
            return cb.AddXmlFile(Path.Combine(cbpath, "LoadBalancing.UnitTests.xml.config")).Build();
        }

    }
}
