using System;

using Photon.NameServer.Configuration;
using Photon.SocketServer;

namespace Photon.NameServer
{
    public class PhotonEndpointInfo
    {
        public PhotonEndpointInfo(Node nodeInfo)
        {
            var udpPort = nodeInfo.PortUdp > 0 ? nodeInfo.PortUdp : Settings.Default.MasterServerPortUdp;
            var tcpPort = nodeInfo.PortTcp > 0 ? nodeInfo.PortTcp : Settings.Default.MasterServerPortTcp;
            var webSocketPort = nodeInfo.PortWebSocket > 0 ? nodeInfo.PortWebSocket : Settings.Default.MasterServerPortWebSocket;
            var secureWebSocketPort = nodeInfo.PortSecureWebSocket > 0 ? nodeInfo.PortSecureWebSocket : Settings.Default.MasterServerPortSecureWebSocket;
            var wsUrlPath = !string.IsNullOrEmpty(nodeInfo.WsUrlPath)  ? "/" + nodeInfo.WsUrlPath : string.IsNullOrEmpty(Settings.Default.MasterServerWsPath) ? string.Empty : "/" + Settings.Default.MasterServerWsPath;
            var webRTCPort = nodeInfo.PortWebRTC > 0 ? nodeInfo.PortWebRTC : Settings.Default.MasterServerPortWebRTC;

            var ipAddress = nodeInfo.IpAddress; 
            this.UdpEndPoint = $"{ipAddress}:{udpPort}";
            this.TcpEndPoint = $"{ipAddress}:{tcpPort}";
            this.WebRTCEndPoint = $"{ipAddress}:{webRTCPort}";
            var ipAddressIPv6 = nodeInfo.IpAddressIPv6;
            if (ipAddressIPv6 != null)
            {
                this.UdpIPv6EndPoint = $"[{ipAddressIPv6}]:{udpPort}";
                this.TcpIPv6EndPoint = $"[{ipAddressIPv6}]:{tcpPort}";
            }

            if (!string.IsNullOrEmpty(nodeInfo.Hostname))
            {
                this.UdpHostname = $"{nodeInfo.Hostname}:{udpPort}";
                this.TcpHostname = $"{nodeInfo.Hostname}:{tcpPort}";
                this.WebSocketEndPoint = $"ws://{nodeInfo.Hostname}:{webSocketPort}{wsUrlPath}";

                this.SecureWebSocketEndPoint = $"wss://{nodeInfo.Hostname}:{secureWebSocketPort}{wsUrlPath}";

                if (ipAddressIPv6 != null)
                {
                    this.WebSocketIPv6EndPoint = this.WebSocketEndPoint;
                    this.SecureWebSocketIPv6EndPoint = this.SecureWebSocketEndPoint;
                }
            }

            // internal use: 

            this.Region = nodeInfo.Region.ToLower();
        }

        //public string GetHostnameBasedEndpoint();

        public string SecureWebSocketIPv6EndPoint { get; set; }

        public string WebSocketIPv6EndPoint { get; set; }

        public string TcpIPv6EndPoint { get; set; }

        public string UdpIPv6EndPoint { get; set; }

        public string UdpEndPoint { get; }

        public string UdpHostname { get; }

        public string TcpEndPoint { get; }

        public string TcpHostname { get; }

        public string WebSocketEndPoint { get; }

        public string SecureWebSocketEndPoint { get; }

        public string WebRTCEndPoint { get; }



        public string Region { get; }

        public override string ToString()
        {
            return $"MasterServerConfig - Region:{this.Region}";
        }

        public string GetEndPoint(NetworkProtocolType networkProtocolType, int port, bool isIPv6 = false, bool useHostnames = false)
        {
            switch (networkProtocolType)
            {
                default:
                    throw new NotSupportedException("No Master server endpoint configured for network protocol " + networkProtocolType);

                case NetworkProtocolType.Udp:
                    return useHostnames ? this.UdpHostname :  (isIPv6 ? this.UdpIPv6EndPoint : this.UdpEndPoint);

                case NetworkProtocolType.Tcp:
                    return useHostnames ? this.TcpHostname :  (isIPv6 ? this.TcpIPv6EndPoint : this.TcpEndPoint);

                case NetworkProtocolType.WebSocket:
                    return isIPv6 ? this.WebSocketIPv6EndPoint : this.WebSocketEndPoint;
                    
                case NetworkProtocolType.SecureWebSocket:
                  return isIPv6 ? this.SecureWebSocketIPv6EndPoint : this.SecureWebSocketEndPoint; 

                case NetworkProtocolType.WebRTC:
                    //TODO
//                    return "192.168.78.204:7071";
                    return this.WebRTCEndPoint;
            }
        }
    }
}
