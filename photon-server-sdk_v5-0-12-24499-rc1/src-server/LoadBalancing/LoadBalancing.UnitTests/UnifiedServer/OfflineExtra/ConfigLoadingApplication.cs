using System;
using System.IO;
using Microsoft.Extensions.Configuration;

using Photon.SocketServer;

namespace Photon.LoadBalancing.UnitTests.UnifiedServer.OfflineExtra
{
    public class ConfigLoadingApplication : ApplicationBase
    {
        public ConfigLoadingApplication()
        : this(LoadConfiguration())
        {}

        private static IConfiguration LoadConfiguration()
        {
            var cb = new ConfigurationBuilder();
            var cbpath = Path.GetDirectoryName(typeof(ConfigLoadingApplication).Assembly.CodeBase).Remove(0, 6);
            return cb.AddXmlFile(Path.Combine(cbpath, "LoadBalancing.UnitTests.xml.config")).Build();
        }

        protected ConfigLoadingApplication(IConfiguration configuration) : base(configuration)
        {
        }

        protected override PeerBase CreatePeer(InitRequest initRequest)
        {
            throw new NotImplementedException();
        }

        protected override void Setup()
        {
            throw new NotImplementedException();
        }

        protected override void TearDown()
        {
            throw new NotImplementedException();
        }
    }
}
