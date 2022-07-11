// --------------------------------------------------------------------------------------------------------------------
// <copyright file="FeedbackLevelElement.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Photon.Common.LoadBalancer.LoadShedding.Configuration
{
    public class FeedbackLevelElement
    {
        public FeedbackLevel Level { get; set; }

        public int Value { get; set; }

        public int ValueDown { get; set; } = -1;
    }
}
