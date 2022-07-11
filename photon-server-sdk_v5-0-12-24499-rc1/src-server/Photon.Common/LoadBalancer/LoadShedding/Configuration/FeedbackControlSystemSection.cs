// --------------------------------------------------------------------------------------------------------------------
// <copyright file="FeedbackControlSystemSection.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System.Collections.Generic;

namespace Photon.Common.LoadBalancer.LoadShedding.Configuration
{
    internal class FeedbackControlSystemSection
    {
        public List<FeedbackControllerElement> FeedbackControllers { get; } = new List<FeedbackControllerElement>();
    }
}
