using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Text;

namespace Photon.SocketServer.NUnit.Utils.Http
{
    public class PortManager
    {
        // IANA suggested range for dynamic or private ports
        private const int MinPort = 49215;
        private const int MaxPort = 65535;

        private static readonly object syncRoot = new object();

        private static readonly HashSet<int> portsTaken = new HashSet<int>();

        public static int TakePort()
        {
            lock (syncRoot)
            {
                int[] usedPorts = { };

                try
                {
                    var properties = IPGlobalProperties.GetIPGlobalProperties();
                    var tcpListeners = properties.GetActiveTcpListeners();
                    usedPorts = tcpListeners.Select(l => l.Port).ToArray();
                }
                catch (OverflowException)
                {
                    // IPGlobalProperties.GetActiveTcpListeners has an issue when running on Windows Subsystem for Linux.
                    // https://github.com/dotnet/corefx/issues/30909
                }


                for (int i = MinPort; i < MaxPort; i++)
                {
                    if (portsTaken.Contains(i) || usedPorts.Contains(i))
                        continue;

                    portsTaken.Add(i);
                    return i;
                }
            }

            return -1;
        }

        public static bool IsPortFree(int port)
        {
            lock (syncRoot)
            {
                int[] usedPorts = { };

                try
                {
                    var properties = IPGlobalProperties.GetIPGlobalProperties();
                    var tcpListeners = properties.GetActiveTcpListeners();
                    usedPorts = tcpListeners.Select(l => l.Port).ToArray();
                }
                catch (OverflowException)
                {
                    // IPGlobalProperties.GetActiveTcpListeners has an issue when running on Windows Subsystem for Linux.
                    // https://github.com/dotnet/corefx/issues/30909
                }


                return !portsTaken.Contains(port) && !usedPorts.Contains(port);
            }
        }

        public static void ReturnPort(int port)
        {
            lock (syncRoot)
            {
                portsTaken.Remove(port);
            }
        }
    }
}
