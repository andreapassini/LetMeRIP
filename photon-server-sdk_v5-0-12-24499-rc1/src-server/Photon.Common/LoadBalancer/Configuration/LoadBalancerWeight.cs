// --------------------------------------------------------------------------------------------------------------------
// <copyright file="LoadBalancerWeightElement.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using Photon.Common.LoadBalancer.LoadShedding;

namespace Photon.Common.LoadBalancer.Configuration
{
    internal class LoadBalancerWeight
    {
        public FeedbackLevel Level { get; set; }

        public int Value { get; set; }
    }
}
