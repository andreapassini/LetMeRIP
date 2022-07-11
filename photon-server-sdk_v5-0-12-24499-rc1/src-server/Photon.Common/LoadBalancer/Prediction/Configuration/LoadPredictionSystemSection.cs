// --------------------------------------------------------------------------------------------------------------------
// <copyright file="FeedbackControlSystemSection.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;

using Photon.Common.LoadBalancer.LoadShedding.Configuration;

namespace Photon.Common.LoadBalancer.Prediction.Configuration
{
    [Serializable]
    public class LoadPredictionSystemSection
    {
        public List<FeedbackControllerElement> FeedbackControllers { get; } = new List<FeedbackControllerElement>();
    }
}
