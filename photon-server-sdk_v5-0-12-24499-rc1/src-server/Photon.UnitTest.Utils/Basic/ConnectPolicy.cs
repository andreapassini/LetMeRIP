using System;
using ExitGames.Client.Photon;

namespace Photon.UnitTest.Utils.Basic
{
    public abstract class ConnectPolicy
    {
        public const int WaitTime = 15000;

        public string ApplicationId = string.Empty;
        public string ApplicationVersion = string.Empty;
        public string Region = string.Empty;
        public string ApplicationName = string.Empty;

        public Version ClientVersion = new Version(4, 0, 5, 0); // new Version(4, 2, 255, 255);

        public ushort sdkId = 0x0000;

        protected const bool LogClientMessages = false;

        public ConnectionProtocol Protocol = ConnectionProtocol.Tcp;

        public IAuthenticationScheme AuthenticatonScheme;

        public virtual string ServerIp
        {
            get
            {
                return "127.0.0.1";
            }
        }

        public virtual string ServerPort
        {
            get
            {
                switch (this.Protocol)
                {
                    case ConnectionProtocol.Tcp:
                        return "4530";
                    case ConnectionProtocol.Udp:
                        return "5055";
                    case ConnectionProtocol.WebSocket:
                        return "9090";
                    case ConnectionProtocol.WebSocketSecure:
                        return "19090";

                    default:
                        throw new NotSupportedException("Protocol: " + this.Protocol);
                }
            }
        }

        public string ServerAddress
        {
            get
            {
                return string.Format("{0}:{1}", this.ServerIp, this.ServerPort);
            }
        }

        public virtual bool IsOffline { get { return false; } }
        public bool IsOnline { get { return !this.IsOffline; } }
        public virtual bool IsRemote { get { return false; } }

        public virtual bool IsInited { get { return true; } }

        /// <summary>
        /// use this property to get rid of artificial delay before sending for offline client
        /// we need delay to mimic online communication where it presents naturally
        /// for some tests we need to send a lot without delay.
        /// those tests should set this property explicitly
        /// </summary>
        public bool UseSendDelayForOfflineTests { get; set; } = true;
        public bool UseNSToGetMS { get; protected set; }

        public abstract bool Setup();
        public abstract void TearDown();

        public abstract UnifiedClientBase CreateTestClient();

        public abstract void ConnectToServer(INUnitClient client, string address, object custom);
    }
}
