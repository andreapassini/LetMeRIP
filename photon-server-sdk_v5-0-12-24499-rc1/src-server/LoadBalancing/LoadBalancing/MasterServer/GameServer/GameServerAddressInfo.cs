using System.Net;
using System.Net.Sockets;

using ExitGames.Logging;

using Photon.LoadBalancing.ServerToServer.Operations;

namespace Photon.LoadBalancing.MasterServer.GameServer
{
    public class GameServerAddressInfo
    {
        #region Properties

        public string Address { get; private set; }

        public string AddressIPv6 { get; private set; }

        public string Hostname { get; private set; }

        // IPv4
        public string TcpAddress { get; private set; }

        public string UdpAddress { get; private set; }

        public string WebSocketAddress { get; private set; }

        public string WebRTCAddress { get; private set; }
  
        // IPv6
        public string TcpAddressIPv6 { get; private set; }

        public string UdpAddressIPv6 { get; private set; }

        public string WebSocketAddressIPv6 { get; private set; }

        // Hostname
        public string TcpHostname { get; private set; }

        public string UdpHostname { get; private set; }

        public string WebSocketHostname { get; private set; }

        public string SecureWebSocketHostname { get; private set; }

        #endregion

        public static GameServerAddressInfo CreateAddressInfo(IRegisterGameServer registerRequest, ILogger log)
        {
            var result = new GameServerAddressInfo
            {
                Address = registerRequest.GameServerAddress
            };

            if (registerRequest.GameServerAddressIPv6 != null
                && IPAddress.Parse(registerRequest.GameServerAddressIPv6).AddressFamily == AddressFamily.InterNetworkV6)
            {
                result.AddressIPv6 = $"[{IPAddress.Parse(registerRequest.GameServerAddressIPv6)}]";
            }
            result.Hostname = registerRequest.GameServerHostName;

            if (registerRequest.UdpPort.HasValue)
            {
                result.UdpAddress = string.IsNullOrEmpty(result.Address) ? null : $"{result.Address}:{registerRequest.UdpPort}";
                result.UdpAddressIPv6 = string.IsNullOrEmpty(result.AddressIPv6) ? null : $"{result.AddressIPv6}:{registerRequest.UdpPort}";
                result.UdpHostname = string.IsNullOrEmpty(result.Hostname) ? null : $"{result.Hostname}:{registerRequest.UdpPort}";
            }

            if (registerRequest.TcpPort.HasValue)
            {
                result.TcpAddress = string.IsNullOrEmpty(result.Address) ? null : $"{result.Address}:{registerRequest.TcpPort}";
                result.TcpAddressIPv6 = string.IsNullOrEmpty(result.AddressIPv6) ? null : $"{result.AddressIPv6}:{registerRequest.TcpPort}";
                result.TcpHostname = string.IsNullOrEmpty(result.Hostname) ? null : $"{result.Hostname}:{registerRequest.TcpPort}";
            }

            if (registerRequest.WebSocketPort.HasValue && registerRequest.WebSocketPort != 0)
            {
                result.WebSocketAddress = string.IsNullOrEmpty(result.Address)
                    ? null
                    : $"ws://{result.Address}:{registerRequest.WebSocketPort}{registerRequest.GamingWsPath}";

                result.WebSocketAddressIPv6 = string.IsNullOrEmpty(result.AddressIPv6)
                    ? null
                    : $"ws://{result.AddressIPv6}:{registerRequest.WebSocketPort}{registerRequest.GamingWsPath}";

                result.WebSocketHostname = string.IsNullOrEmpty(result.Hostname)
                    ? null
                    : $"ws://{result.Hostname}:{registerRequest.WebSocketPort}{registerRequest.GamingWsPath}";
            }

            if (registerRequest.WebRTCPort.HasValue && registerRequest.WebRTCPort != 0)
            {
                result.WebRTCAddress = string.IsNullOrEmpty(result.Address)
                    ? null
                    : $"{result.Address}:{registerRequest.WebRTCPort}";
            }

            // HTTP & WebSockets require a proper domain name (especially for certificate validation on secure Websocket & HTTPS connections): 
            if (string.IsNullOrEmpty(result.Hostname))
            {
                log.WarnFormat("HTTPs & Secure WebSockets not supported. GameServer {0} does not have a public hostname.", result.Address);
            }
            else
            {
                if (registerRequest.SecureWebSocketPort.HasValue && registerRequest.SecureWebSocketPort != 0)
                {
                    result.SecureWebSocketHostname = $"wss://{result.Hostname}:{registerRequest.SecureWebSocketPort}{registerRequest.GamingWsPath}";
                }
            }
            return result;
        }
    }
}
