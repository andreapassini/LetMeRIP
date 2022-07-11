// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CustomAuthResultCounters.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the CustomAuthResultCounters type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System.Diagnostics;
using Photon.SocketServer.Diagnostics.Counters;
using Photon.SocketServer.Diagnostics.Counters.Wrappers;
#pragma warning disable 649

namespace Photon.Common.Authentication.Diagnostic
{
    [PerfCounterCategory("Photon: Custom Authentication Results", PerformanceCounterInstanceLifetime.Process)]
    public sealed class CustomAuthResultCounters : PerfCounterManagerBase<CustomAuthResultCounters>
    {
        /// <summary>
        /// Dummy static ctor to iniate base static ctor
        /// </summary>
        static CustomAuthResultCounters()
        {
            InitializeWithDefaults();
        }

        #region Counters
        [PerfCounter("Results Data", PerformanceCounterType.NumberOfItems32)]
        private PerSecondCounterWrapper ResultsData;

        [PerfCounter("Results Accepted", PerformanceCounterType.NumberOfItems32)]
        private PerSecondCounterWrapper ResultsAccepted;

        [PerfCounter("Results Denied",PerformanceCounterType.NumberOfItems32)]
        private PerformanceCounterWrapper ResultsDenied;

        [PerfCounter("Errors/sec", PerformanceCounterType.RateOfCountsPerSecond32)]
        private PerformanceCounterWrapper ErrorsPerSec;

        [PerfCounter("Errors", PerformanceCounterType.NumberOfItems32)]
        private PerformanceCounterWrapper Errors;

        [PerfCounter("QueueTimeouts/sec", PerformanceCounterType.RateOfCountsPerSecond32)]
        private PerformanceCounterWrapper QueueTimeoutsPerSec;

        [PerfCounter("QueueTimeouts", PerformanceCounterType.NumberOfItems32)]
        private PerformanceCounterWrapper QueueTimeouts;

        [PerfCounter("QueueFull Errors/sec", PerformanceCounterType.RateOfCountsPerSecond32)]
        private PerformanceCounterWrapper QueueFullErrorsPerSec;

        [PerfCounter("QueueFull Errors", PerformanceCounterType.NumberOfItems32)]
        private PerformanceCounterWrapper QueueFullErrors;

        [PerfCounter("Http Requests Avg ms", PerformanceCounterType.AverageTimer32)]
        private AverageCounterWrapper HttpRequestTime;

        [PerfCounter("Http Requests/sec", PerformanceCounterType.RateOfCountsPerSecond32)]
        private PerformanceCounterWrapper HttpRequestsPerSec;

        [PerfCounter("Http Errors/sec", PerformanceCounterType.RateOfCountsPerSecond32)]
        private PerformanceCounterWrapper HttpErrorsPerSec;

        [PerfCounter("Http Errors", PerformanceCounterType.NumberOfItems32)]
        private PerformanceCounterWrapper HttpErrors;

        [PerfCounter("Http Timeouts/sec", PerformanceCounterType.RateOfCountsPerSecond32)]
        private PerformanceCounterWrapper HttpTimeoutsPerSec;

        [PerfCounter("Http Timeouts", PerformanceCounterType.NumberOfItems32)]
        private PerformanceCounterWrapper HttpTimeouts;

        #endregion

        #region Methods

        public static void IncrementErrors(CustomAuthResultCounters instance)
        {
            if (!isInitialized)
            {
                return;
            }

            if (instance != null)
            {
                instance.ErrorsPerSec.Increment();
                instance.Errors.Increment();
            }

            GlobalInstance.ErrorsPerSec.Increment();
            GlobalInstance.Errors.Increment();
        }

        public static void IncrementQueueFullErrors(CustomAuthResultCounters instance)
        {
            if (!isInitialized)
            {
                return;
            }

            if (instance != null)
            {
                instance.QueueFullErrorsPerSec.Increment();
                instance.QueueFullErrors.Increment();
            }

            GlobalInstance.QueueFullErrorsPerSec.Increment();
            GlobalInstance.QueueFullErrors.Increment();
        }

        public static void IncrementQueueTimeouts(CustomAuthResultCounters instance)
        {
            if (!isInitialized)
            {
                return;
            }

            if (instance != null)
            {
                instance.QueueTimeoutsPerSec.Increment();
                instance.QueueTimeouts.Increment();
            }

            GlobalInstance.QueueTimeoutsPerSec.Increment();
            GlobalInstance.QueueTimeouts.Increment();
        }

        public static void IncrementHttpRequests(long ticks, CustomAuthResultCounters instance)
        {
            if (!isInitialized)
            {
                return;
            }

            if (instance != null)
            {
                instance.HttpRequestsPerSec.Increment();
                instance.HttpRequestTime.Increment(ticks * 1000);
            }

            GlobalInstance.HttpRequestsPerSec.Increment();
            GlobalInstance.HttpRequestTime.Increment(ticks * 1000);
        }

        public static void IncrementResultsAccepted(CustomAuthResultCounters instance)
        {
            if (!isInitialized)
            {
                return;
            }

            if (instance != null)
            {
                instance.ResultsAccepted.Increment();
            }

            GlobalInstance.ResultsAccepted.Increment();
        }

        public static void IncrementResultsDenied(CustomAuthResultCounters instance)
        {
            if (!isInitialized)
            {
                return;
            }

            if (instance != null)
            {
                instance.ResultsDenied.Increment();
            }

            GlobalInstance.ResultsDenied.Increment();
        }

        public static void IncrementResultsData(CustomAuthResultCounters instance)
        {
            if (!isInitialized)
            {
                return;
            }

            if (instance != null)
            {
                instance.ResultsData.Increment();
            }

            GlobalInstance.ResultsData.Increment();
        }

        public static void IncrementHttpErrors(CustomAuthResultCounters instance)
        {
            if (!isInitialized)
            {
                return;
            }

            if (instance != null)
            {
                instance.HttpErrorsPerSec.Increment();
                instance.HttpErrors.Increment();
            }

            GlobalInstance.HttpErrorsPerSec.Increment();
            GlobalInstance.HttpErrors.Increment();
        }

        public static void IncrementHttpTimeouts(CustomAuthResultCounters instance)
        {
            if (!isInitialized)
            {
                return;
            }

            if (instance != null)
            {
                instance.HttpTimeoutsPerSec.Increment();
                instance.HttpTimeouts.Increment();
            }

            GlobalInstance.HttpTimeoutsPerSec.Increment();
            GlobalInstance.HttpTimeouts.Increment();
        }
        #endregion

    }
}
