// --------------------------------------------------------------------------------------------------------------------
// <copyright file="LoadBalancerSection.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Photon.Common.LoadBalancer.LoadShedding;

namespace Photon.Common.LoadBalancer.Configuration
{
    internal class LoadBalancerSection
    {
        public List<LoadBalancerWeight> LoadBalancerWeights { get; } = new List<LoadBalancerWeight>();

        [Required]
        public FeedbackLevel ValueUp { get; set; }

        public FeedbackLevel ValueDown { get; set; } = FeedbackLevel.Highest;

        public float ReserveRatio { get; set; } = 0.0f;
    }
}
