// --------------------------------------------------------internal ------------------------------------------------------------
// <copyright file="CloudCache.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the CloudCache type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------


using Photon.NameServer.Operations;
using Photon.SocketServer;
using System.Linq;

namespace Photon.NameServer
{
    using System;
    using System.Collections.Generic;
    using ExitGames.Logging;
    using Configuration;

    public class MasterServerCache
    {
        private static readonly Random rnd = new Random();

        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private readonly List<PhotonEndpointInfo> servers = new List<PhotonEndpointInfo>();
        
        public MasterServerCache(IEnumerable<Node> nodes)
        {
            foreach (var nodeInfo in nodes)
            {
                var endPoint = new PhotonEndpointInfo(nodeInfo);
                this.servers.Add(endPoint);
            }

            if (log.IsDebugEnabled)
            {
                foreach (var endpoint in this.servers)
                {
                    log.DebugFormat("Hostname - 1 {0}, UDP: {1}", endpoint.UdpHostname, endpoint.UdpEndPoint);
                }
            }
        }
        
        public bool TryGetPhotonEndpoint(string region, out PhotonEndpointInfo result, out string message)
        {
            message = null;

            result = this.TryGetPhotonEndpoint(region.ToLower());
            return result != null; 
        }

        private PhotonEndpointInfo TryGetPhotonEndpoint(string region)
        {
            if (this.servers.Count > 0)
            {
                region = region.ToLower();
                var matchingServers = this.servers.Where(w => w.Region == region).ToArray();
                var matchingCount = matchingServers.Length;
                if (matchingCount == 0)
                {
                    return null;
                }
                // support multiple masters per server type / cloud / region / cluster. Choose one by random. 
                return matchingServers[rnd.Next(matchingCount)];
            }

            return null; 
        }

        public bool TryGetRegions(GetRegionListRequest regionListRequest, NetworkProtocolType networkProtocol, 
            int port, bool isIPv6, bool useHostnames, 
            out List<string> regions, out List<string> endPoints, out string message)
        {
            regions = new List<string>();
            endPoints = new List<string>();
            message = string.Empty;

            foreach (var server in this.servers)
            {
                var endpoint = server.GetEndPoint(networkProtocol, port, isIPv6, useHostnames);
                if (endpoint == null)
                {
                    continue;
                }

                regions.Add(server.Region);
                endPoints.Add(endpoint);
            }

            return true;
        }
    }
}
