// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IFeedbackControlSystem.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the IFeedbackControlSystem type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Photon.Common.LoadBalancer.LoadShedding
{
    internal interface IFeedbackControlSystem
    {
        FeedbackLevel Output { get; }

        void SetPeerCount(int peerCount);

        void SetCpuUsage(int cpuUsage, out FeedbackLevel cpuLevel);

        void SetBandwidthUsage(int bytes, out FeedbackLevel bandwidthLevel);
        
        void SetOutOfRotation(bool isOutOfRotation);
    }
}