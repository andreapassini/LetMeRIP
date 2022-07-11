using System;

namespace LoadBalancing.TestInterfaces
{
    public interface ITestMasterApplication
    {
        int OnBeginReplicationCount { get; }
        int OnFinishReplicationCount { get; }
        int OnStopReplicationCount { get; }
        int OnServerWentOfflineCount { get; }
        void ResetStats();
        int PeerCount { get; }
    }
}
