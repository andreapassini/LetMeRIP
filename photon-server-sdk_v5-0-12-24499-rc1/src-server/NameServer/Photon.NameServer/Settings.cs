// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Settings.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the Settings type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System.Runtime;
using Photon.SocketServer;
using Photon.SocketServer.Annotations;

namespace Photon.NameServer
{

    [SettingsMarker("Photon:NameServer")]
    public class Settings
    {
        // Explicit static constructor to tell C# compiler
        // not to mark type as beforefieldinit
        static Settings()
        { }

        public static Settings Default { get; } = ApplicationBase.GetConfigSectionAndValidate<Settings>("Photon:NameServer");

        public ushort MasterServerPortUdp { get; set; } = 5055;

        public ushort MasterServerPortTcp { get; set; } = 4530;

        public ushort MasterServerPortWebSocket { get; set; } = 9090;

        public string MasterServerWsPath { get; set; } = "Master";

        public ushort MasterServerPortWebRTC { get; set; } = 7071;

        public string NameServerConfig { get; set; } = "Nameserver.json";

        public ushort MasterServerPortSecureWebSocket { get; set; } = 19090;

        public bool EnablePerformanceCounters { get; set; } = true;

        public int MinDisconnectTime { get; set; } = 3000;

        public int MaxDisconnectTime { get; set; } = 10000;

        public int AuthTimeout { get; set; } = 20_000;//20 seconds

    }
}
