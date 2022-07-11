// --------------------------------------------------------------------------------------------------------------------
// <copyright file="NodeConfig.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the NodeConfig type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Photon.NameServer.Configuration
{
    public class NodeList
    {
        [DataMember(IsRequired = true)]
        public List<Node> Nodes { get; set; }
    }

    /// <summary>
    /// The node config.
    /// </summary>
    public class Node
    {
        [DataMember(IsRequired = true)]
        public string IpAddress { get; set; }

        [DataMember(IsRequired = true)]
        public string Region { get; set; }

        [DataMember(IsRequired = false)]
        public string IpAddressIPv6 { get; set; }

        [DataMember(IsRequired = false)]
        public string Hostname { get; set; }

        [DataMember(IsRequired = false)]
        public int PortTcp { get; set; }

        [DataMember(IsRequired = false)]
        public int PortUdp { get; set; }

        [DataMember(IsRequired = false)]
        public int PortWebSocket { get; set; }

        [DataMember(IsRequired = false)]
        public int PortSecureWebSocket { get; set; }

        [DataMember(IsRequired = false)]
        public string WsUrlPath { get; set; }

        //TODO add to config?
        [DataMember(IsRequired = false)]
        public int PortWebRTC { get; set; }
    }
}
