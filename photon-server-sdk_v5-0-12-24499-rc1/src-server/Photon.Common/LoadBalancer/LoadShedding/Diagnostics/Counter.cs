// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Counter.cs" company="">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using ExitGames.Diagnostics.Counter;

namespace Photon.Common.LoadBalancer.LoadShedding.Diagnostics
{
    /// <summary>
    /// Counter on Game Server application level.
    /// </summary>
    public static class Counter
    {
        public static readonly NumericCounter IsMasterServer = new NumericCounter("IsMasterServer");

        public static readonly NumericCounter ServerState = new NumericCounter("ServerState");

        public static readonly NumericCounter LoadLevel = new NumericCounter("LoadLevel");

        public static readonly NumericCounter CpuAvg = new NumericCounter("CpuAvg");

        public static readonly NumericCounter BusinessQueueAvg = new NumericCounter("BusinessQueueAvg");

        public static readonly NumericCounter EnetQueueAvg = new NumericCounter("EnetQueueAvg");

        public static readonly NumericCounter BytesInAndOutAvg = new NumericCounter("BytesInAndOutAvg");

        public static readonly NumericCounter EnetThreadsProcessingAvg = new NumericCounter("EnetThreadsProcessingAvg");

        // The number of disconnected TCP peers (per second) / number of total TCP peers
        public static readonly NumericCounter TcpDisconnectRateAvg = new NumericCounter("TcpDisconnectRateAvg");

        // The number of disconnected UDP peers (per second) / number of total UDP peers
        public static readonly NumericCounter UdpDisconnectRateAvg = new NumericCounter("UdpDisconnectRateAvg");

        //TODO name(s)
        public static readonly AverageCounter SelfMonitoringRtt = new AverageCounter("SelfMonitoringRtt");
    }
}
