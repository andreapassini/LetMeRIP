// --------------------------------------------------------------------------------------------------------------------
// <copyright file="WorkloadPerformanceCounters.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Creates the Photon workload counters
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System.Diagnostics;
using Photon.SocketServer.Diagnostics.Counters;
using Photon.SocketServer.Diagnostics.Counters.Wrappers;

namespace Photon.Common.LoadBalancer.LoadShedding.Diagnostics
{
    [PerfCounterCategory(WorkloadCounters)]
    public sealed class WorkloadPerformanceCounters : PerfCounterManagerBase<WorkloadPerformanceCounters>
    {
        //use this category for workload counters
        private const string WorkloadCounters = "Photon: Workload";

        static WorkloadPerformanceCounters()
        {
            InitializeWithDefaults();
        }

        [PerfCounter("Workload: Level CPU", PerformanceCounterType.NumberOfItems32, "Workload level for CPU", WorkloadCounters)]
        public static PerformanceCounterWrapper WorkloadLevelCPU;

        [PerfCounter("Workload: Level Bandwidth", PerformanceCounterType.NumberOfItems32, "Workload level for Bandwidth", WorkloadCounters)]
        public static PerformanceCounterWrapper WorkloadLevelBandwidth;

        [PerfCounter("Workload: CPU", PerformanceCounterType.NumberOfItems32, "Workload CPU", WorkloadCounters)]
        public static PerformanceCounterWrapper WorkloadCPU;

        [PerfCounter("Workload: Bandwidth", PerformanceCounterType.NumberOfItems32, "Workload Bandwidth", WorkloadCounters)]
        public static PerformanceCounterWrapper WorkloadBandwidth;
    }
}
