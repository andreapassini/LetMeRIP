using System;
using ExitGames.Client.Photon;
using Photon.LoadBalancing.UnifiedClient.AuthenticationSchemes;
using Photon.UnitTest.Utils.Basic;

namespace Photon.LoadBalancing.UnitTests.UnifiedServer.Policy
{
    public abstract class LBConnectPolicyBase : ConnectPolicy
    {
        protected const string NameServerAppName = "NameServer";

        protected const string MasterServerAppName = "Master";

        protected const string GameServerAppName = "Game";

        protected const string GameServer2AppName = "Game2";

        public string MasterServerAddress 
        {
            get
            {
                switch (this.Protocol)
                {
                    case ConnectionProtocol.WebSocket:
                        return string.Format("ws://{0}:{1}", this.ServerIp, this.ServerPort);
                    case ConnectionProtocol.WebSocketSecure:
                        return string.Format("wss://{0}:{1}", this.ServerIp, this.ServerPort);
                    default:
                        return this.ServerAddress;
                }
            }
        }

        public string GameServerAddress
        {
            get
            {
                switch (this.Protocol)
                {
                    case ConnectionProtocol.Tcp:
                        return String.Format("{0}:{1}", this.ServerIp, 4531);
                    case ConnectionProtocol.Udp:
                        return String.Format("{0}:{1}", this.ServerIp, 5056);
                    case ConnectionProtocol.WebSocket:
                        return String.Format("ws://{0}:{1}", this.ServerIp, 9091);
                    case ConnectionProtocol.WebSocketSecure:
                        return String.Format("wss://{0}:{1}", this.ServerIp, 19091);
                    default:
                        throw new NotSupportedException("Protocol: " + this.Protocol);
                }
            }
        }

        public string NameServerAddress
        {
            get
            {
                switch (this.Protocol)
                {
                    case ConnectionProtocol.Tcp:
                        return String.Format("{0}:{1}", this.ServerIp, 4533);
                    case ConnectionProtocol.Udp:
                        return String.Format("{0}:{1}", this.ServerIp, 5058);
                    case ConnectionProtocol.WebSocket:
                        return String.Format("ws://{0}:{1}", this.ServerIp, 9093);
                    case ConnectionProtocol.WebSocketSecure:
                        return String.Format("wss://{0}:{1}", this.ServerIp, 19093);
                    default:
                        throw new NotSupportedException("Protocol: " + this.Protocol);
                }
            }
        }

        protected LBConnectPolicyBase()
        {
            this.AuthenticatonScheme = new TokenLessAuthenticationScheme();
            this.ApplicationId = MasterServerAppName;
        }
    }
}