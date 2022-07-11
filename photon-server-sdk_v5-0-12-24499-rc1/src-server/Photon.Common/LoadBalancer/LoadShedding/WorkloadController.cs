// --------------------------------------------------------------------------------------------------------------------
// <copyright file="WorkloadController.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the WorkloadController type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System;
using ExitGames.Concurrency.Fibers;
using ExitGames.Diagnostics.Counter;
using ExitGames.Logging;
using Photon.Common.Annotations;
using Photon.Common.LoadBalancer.Common;
using Photon.Common.LoadBalancer.LoadShedding.Diagnostics;
using Photon.SocketServer;

namespace Photon.Common.LoadBalancer.LoadShedding
{
    public class WorkloadController
    {
        #region Constants and Fields

        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private const int AverageHistoryLength = 100;

        private AverageCounterReader businessLogicQueueCounter;

        //private readonly AverageCounterReader bytesInCounter;

        //private readonly AverageCounterReader bytesOutCounter;

        private readonly BandwidthCounterReader bytesInOutCounters;

        private readonly CpuUsageCounterReader cpuCounter;

        private AverageCounterReader enetQueueCounter;

        private AverageCounterReader enetThreadsProcessingCounter;
        
        private AverageCounterReader tcpPeersCounter;

        private AverageCounterReader tcpDisconnectsPerSecondCounter;

        private AverageCounterReader tcpClientDisconnectsPerSecondCounter;

        private AverageCounterReader udpPeersCounter;

        private AverageCounterReader udpDisconnectsPerSecondCounter;

        private AverageCounterReader udpClientDisconnectsPerSecondCounter;
        
        private PerformanceCounterReader enetThreadsActiveCounter; 

        private IFeedbackControlSystem feedbackControlSystem;

        private readonly PoolFiber fiber;
        
        private readonly int updateIntervalInMs;
        
        private IDisposable timerControl;

        private ServerState serverState = ServerState.Normal;

        [UsedImplicitly] private string serverId;
        #endregion

        #region Constructors and Destructors

        public WorkloadController(
            ApplicationBase application, string instanceName, int updateIntervalInMs, string serverId, string workLoadConfigFile)
        {
            try
            {
                this.updateIntervalInMs = updateIntervalInMs;
                this.FeedbackLevel = FeedbackLevel.Level3;

                this.fiber = new PoolFiber();
                this.fiber.Start();
                this.serverId = serverId;

                this.cpuCounter = new CpuUsageCounterReader(AverageHistoryLength);
                if (!this.cpuCounter.IsValid)
                {
                    log.WarnFormat("Did not find counter {0}", this.cpuCounter.Name);
                }

                bytesInOutCounters = new BandwidthCounterReader(AverageHistoryLength);
                // amazon instances do not have counter for network interfaces
                if (!bytesInOutCounters.IsValid)
                {
                    log.Warn("Bandwidth counters are not valid");
                }

                this.InitWindowsSpecificCounters(instanceName);

                this.feedbackControlSystem = new FeedbackControlSystem(1000, application.ApplicationRootPath, workLoadConfigFile);

                WorkloadPerformanceCounters.Initialize();

                this.IsInitialized = true;
            }
            catch (Exception e)
            {
                log.Error(string.Format("Exception during WorkloadController construction. Exception Msg: {0}", e.Message), e);
            }
        }

        #endregion

        #region Events

        public event EventHandler FeedbacklevelChanged;

        #endregion

        #region Properties

        public FeedbackLevel FeedbackLevel { get; private set; }

        public ServerState ServerState
        {
            get
            {
                return this.serverState;
            }

            set
            {
                if (value != this.serverState)
                {
                    var oldValue = this.serverState;
                    this.serverState = value;
                    Counter.ServerState.RawValue = (long)this.ServerState;
                    this.RaiseFeedbacklevelChanged();

                    if (log.IsInfoEnabled)
                    {
                        log.InfoFormat("ServerState changed: old={0}, new={1}", oldValue, this.serverState);
                    }

                }
            }
        }

        public bool IsInitialized { get; private set; }

        #endregion

        #region Public Methods

       
        /// <summary>
        ///   Starts the workload controller with a specified update interval in milliseconds.
        /// </summary>
        public void Start()
        {
            if (!this.IsInitialized)
            {
                return;
            }

            if (this.timerControl == null)
            {
                this.timerControl = this.fiber.ScheduleOnInterval(this.Update, 100, (int)this.updateIntervalInMs);
            }
        }

        public void Stop()
        {
            if (this.timerControl != null)
            {
                this.timerControl.Dispose();
            }
        }

        #endregion

        #region Methods

        private void InitWindowsSpecificCounters(string instanceName)
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return;
            }

            this.businessLogicQueueCounter = new AverageCounterReader(AverageHistoryLength, "Photon Socket Server: Threads and Queues", "Business Logic Queue", instanceName);
            if (!this.businessLogicQueueCounter.InstanceExists)
            {
                log.WarnFormat("Did not find counter {0}", this.businessLogicQueueCounter.Name);
            }

            this.enetQueueCounter = new AverageCounterReader(AverageHistoryLength, "Photon Socket Server: Threads and Queues", "ENet Queue", instanceName);
            if (!this.enetQueueCounter.InstanceExists)
            {
                log.WarnFormat("Did not find counter {0}", this.enetQueueCounter.Name);
            }

            this.enetThreadsProcessingCounter = new AverageCounterReader(AverageHistoryLength, "Photon Socket Server: Threads and Queues", "ENet Threads Processing", instanceName);
            if (!this.enetThreadsProcessingCounter.InstanceExists)
            {
                log.WarnFormat("Did not find counter {0}", this.enetThreadsProcessingCounter.Name);
            }

            this.enetThreadsActiveCounter = new PerformanceCounterReader("Photon Socket Server: Threads and Queues", "ENet Threads Active", instanceName);
            if (!this.enetThreadsActiveCounter.InstanceExists)
            {
                log.WarnFormat("Did not find counter {0}", this.enetThreadsActiveCounter.Name);
            }

            this.tcpDisconnectsPerSecondCounter = new AverageCounterReader(AverageHistoryLength, "Photon Socket Server: TCP", "TCP: Disconnected Peers +/sec", instanceName);
            if (!this.tcpDisconnectsPerSecondCounter.InstanceExists)
            {
                log.WarnFormat("Did not find counter {0}", this.tcpDisconnectsPerSecondCounter.Name);
            }

            this.tcpClientDisconnectsPerSecondCounter = new AverageCounterReader(AverageHistoryLength, "Photon Socket Server: TCP", "TCP: Disconnected Peers (C) +/sec", instanceName);
            if (!this.tcpClientDisconnectsPerSecondCounter.InstanceExists)
            {
                log.WarnFormat("Did not find counter {0}", this.tcpClientDisconnectsPerSecondCounter.Name);
            }

            this.tcpPeersCounter = new AverageCounterReader(AverageHistoryLength, "Photon Socket Server: TCP", "TCP: Peers", instanceName);
            if (!this.tcpPeersCounter.InstanceExists)
            {
                log.WarnFormat("Did not find counter {0}", this.tcpPeersCounter.Name);
            }

            this.udpDisconnectsPerSecondCounter = new AverageCounterReader(AverageHistoryLength, "Photon Socket Server: UDP", "UDP: Disconnected Peers +/sec", instanceName);
            if (!this.udpDisconnectsPerSecondCounter.InstanceExists)
            {
                log.WarnFormat("Did not find counter {0}", this.udpDisconnectsPerSecondCounter.Name);
            }

            this.udpClientDisconnectsPerSecondCounter = new AverageCounterReader(AverageHistoryLength, "Photon Socket Server: UDP", "UDP: Disconnected Peers (C) +/sec", instanceName);
            if (!this.udpClientDisconnectsPerSecondCounter.InstanceExists)
            {
                log.WarnFormat("Did not find counter {0}", this.udpClientDisconnectsPerSecondCounter.Name);
            }

            this.udpPeersCounter = new AverageCounterReader(AverageHistoryLength, "Photon Socket Server: UDP", "UDP: Peers", instanceName);
            if (!this.udpPeersCounter.InstanceExists)
            {
                log.WarnFormat("Did not find counter {0}", this.udpPeersCounter.Name);
            }
        }

        private void Update()
        {
            if (!this.IsInitialized)
            {
                return;
            }

            FeedbackLevel oldValue = this.feedbackControlSystem.Output;

            if (this.cpuCounter.IsValid)
            {
                var cpuUsage = (int)this.cpuCounter.GetNextAverage();
                Counter.CpuAvg.RawValue = cpuUsage;

                WorkloadPerformanceCounters.WorkloadCPU.RawValue = cpuUsage;
                FeedbackLevel level;
                this.feedbackControlSystem.SetCpuUsage(cpuUsage, out level);
                WorkloadPerformanceCounters.WorkloadLevelCPU.RawValue = (byte)level;
            }

            if (this.bytesInOutCounters.IsValid)
            {
                int bytes = (int)this.bytesInOutCounters.GetNextAverage();
                Counter.BytesInAndOutAvg.RawValue = bytes;

                WorkloadPerformanceCounters.WorkloadBandwidth.RawValue = bytes;
                FeedbackLevel level;
                this.feedbackControlSystem.SetBandwidthUsage(bytes, out level);
                WorkloadPerformanceCounters.WorkloadLevelBandwidth.RawValue = (byte)level;
            }

            this.FeedbackLevel = this.feedbackControlSystem.Output;
            Counter.LoadLevel.RawValue = (byte)this.FeedbackLevel; 

            if (oldValue != this.FeedbackLevel)
            {
                if (log.IsInfoEnabled)
                {
                    log.InfoFormat("FeedbackLevel changed: old={0}, new={1}", oldValue, this.FeedbackLevel);
                }

                this.RaiseFeedbacklevelChanged();
            }

            this.UpdateWindowsSpecificCounters();
        }

        private void UpdateWindowsSpecificCounters()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return;
            }

            if (this.businessLogicQueueCounter.InstanceExists)
            {
                var businessLogicQueue = (int)this.businessLogicQueueCounter.GetNextAverage();
                Counter.BusinessQueueAvg.RawValue = businessLogicQueue;
            }

            if (this.enetQueueCounter.InstanceExists)
            {
                var enetQueue = (int)this.enetQueueCounter.GetNextAverage();
                Counter.EnetQueueAvg.RawValue = enetQueue;
            }

            if (this.enetThreadsProcessingCounter.InstanceExists && this.enetThreadsActiveCounter.InstanceExists)
            {
                try
                {
                    var enetThreadsProcessingAvg = this.enetThreadsProcessingCounter.GetNextAverage();
                    var enetThreadsActiveAvg = this.enetThreadsActiveCounter.GetNextValue();

                    int enetThreadsProcessing;
                    if (enetThreadsActiveAvg > 0)
                    {
                        enetThreadsProcessing = (int)(enetThreadsProcessingAvg / enetThreadsActiveAvg * 100);
                    }
                    else
                    {
                        enetThreadsProcessing = 0;
                    }

                    Counter.EnetThreadsProcessingAvg.RawValue = enetThreadsProcessing;
                }
                catch (DivideByZeroException)
                {
                    log.WarnFormat("Could not calculate Enet Threads processing quotient: Enet Threads Active is 0");
                }
            }

            if (this.tcpPeersCounter.InstanceExists && this.tcpDisconnectsPerSecondCounter.InstanceExists && this.tcpClientDisconnectsPerSecondCounter.InstanceExists)
            {
                try
                {
                    var tcpDisconnectsTotal = this.tcpDisconnectsPerSecondCounter.GetNextAverage();
                    var tcpDisconnectsClient = this.tcpClientDisconnectsPerSecondCounter.GetNextAverage();
                    var tcpDisconnectsWithoutClientDisconnects = tcpDisconnectsTotal - tcpDisconnectsClient;
                    var tcpPeerCount = this.tcpPeersCounter.GetNextAverage();

                    int tcpDisconnectRate;
                    if (tcpPeerCount > 0)
                    {
                        tcpDisconnectRate = (int)(tcpDisconnectsWithoutClientDisconnects / tcpPeerCount * 1000);
                    }
                    else
                    {
                        tcpDisconnectRate = 0;
                    }

                    Counter.TcpDisconnectRateAvg.RawValue = tcpDisconnectRate;

                }
                catch (DivideByZeroException)
                {
                    log.WarnFormat("Could not calculate TCP Disconnect Rate: TCP Peers is 0");
                }
            }

            if (this.udpPeersCounter.InstanceExists && this.udpDisconnectsPerSecondCounter.InstanceExists && this.udpClientDisconnectsPerSecondCounter.InstanceExists)
            {
                try
                {
                    var udpDisconnectsTotal = this.udpDisconnectsPerSecondCounter.GetNextAverage();
                    var udpDisconnectsClient = this.udpClientDisconnectsPerSecondCounter.GetNextAverage();
                    var udpDisconnectsWithoutClientDisconnects = udpDisconnectsTotal - udpDisconnectsClient;
                    var udpPeerCount = this.udpPeersCounter.GetNextAverage();

                    int udpDisconnectRate;
                    if (udpPeerCount > 0)
                    {
                        udpDisconnectRate = (int)(udpDisconnectsWithoutClientDisconnects / udpPeerCount * 1000);
                    }
                    else
                    {
                        udpDisconnectRate = 0;
                    }

                    Counter.UdpDisconnectRateAvg.RawValue = udpDisconnectRate;
                }
                catch (DivideByZeroException)
                {
                    log.WarnFormat("Could not calculate UDP Disconnect Rate: UDP Peers is 0");
                }
            }
        }

        private void RaiseFeedbacklevelChanged()
        {
            var e = this.FeedbacklevelChanged;
            if (e != null)
            {
                e(this, EventArgs.Empty);
            }
        }

        #endregion
    }
}


