using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace Photon.Common.Misc
{
    public static class IpEndPointExtensions
    {
        public static bool TryParseIpEndPoint(string endPointString, out IPEndPoint endPoint)
        {
            endPoint = null;

            if (string.IsNullOrEmpty(endPointString))
            {
                return false;
            }

            var endPointParts = endPointString.Split(':');
            if (endPointParts.Length != 2)
            {
                return false;
            }

            IPAddress address;
            if (IPAddress.TryParse(endPointParts[0], out address) == false)
            {
                return false;
            }

            int port;
            if (int.TryParse(endPointParts[1], out port) == false)
            {
                return false;
            }

            endPoint = new IPEndPoint(address, port);
            return true;
        }

        public static bool TryParseIPEndPointOrHostName(string endPointString, out IPEndPoint endPoint)
        {
            endPoint = null;
            if (string.IsNullOrEmpty(endPointString))
            {
                return false;
            }

            var endPointParts = endPointString.Split(':');
            if (endPointParts.Length != 2)
            {
                return false;
            }

            IPAddress ipAddress;
            if (!IPAddress.TryParse(endPointParts[0], out ipAddress))
            {
                // try to look up IP from host name
                var hostEntry = Dns.GetHostEntry(endPointParts[0]);
                if (hostEntry.AddressList == null || hostEntry.AddressList.Length == 0)
                {
                    // not an DNS entry either
                    return false;
                }

                ipAddress = hostEntry.AddressList.First(address => address.AddressFamily == AddressFamily.InterNetwork);

                if (ipAddress == null)
                {
                    return false;
                }
            }

            int port;
            if (int.TryParse(endPointParts[1], out port) == false)
            {
                return false;
            }

            endPoint = new IPEndPoint(ipAddress, port);
            return true;
        }
    }
}
