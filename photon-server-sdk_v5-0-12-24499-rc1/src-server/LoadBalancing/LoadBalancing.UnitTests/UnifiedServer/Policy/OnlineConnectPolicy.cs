using System.Diagnostics;
using ExitGames.Client.Photon;
using Photon.LoadBalancing.UnifiedClient;
using Photon.UnitTest.Utils.Basic;
using Photon.UnitTest.Utils.Basic.NUnitClients;

namespace Photon.LoadBalancing.UnitTests.UnifiedServer.Policy
{
    public class OnlineConnectPolicy : LBConnectPolicyBase
    {
        public Process photonProcess;

        public OnlineConnectPolicy()
        {
        }

        public OnlineConnectPolicy(IAuthenticationScheme scheme, ConnectionProtocol protocol = ConnectionProtocol.Tcp)
            : this()
        {
            this.Protocol = protocol;
            this.AuthenticatonScheme = scheme;
        }

        public override bool Setup()
        {
            this.photonProcess = PhotonStarter.Start(
                this.MasterServerAddress,
                MasterServerAppName,
                this.Protocol,
                "/run LoadBalancing /config PhotonServer.LB.Dev.config"); 

                return true;

        }

        public override void TearDown()
        {
            if (this.photonProcess != null)
            {
                if (!this.photonProcess.HasExited)
                {
                    this.photonProcess.Kill();
                    this.photonProcess.WaitForExit();
                }

                this.photonProcess.Close();
                this.photonProcess = null;
            }
        }

        public override UnifiedClientBase CreateTestClient()
        {
            return new UnifiedTestClient(new OnlineNUnitClient(this, LogClientMessages), this.AuthenticatonScheme);
        }

        public override void ConnectToServer(INUnitClient client, string address, object custom)
        {
            var nclient = (OnlineNUnitClient)client;
            nclient.Connect(address, this.ApplicationId, null, custom);
        }
    }
}
