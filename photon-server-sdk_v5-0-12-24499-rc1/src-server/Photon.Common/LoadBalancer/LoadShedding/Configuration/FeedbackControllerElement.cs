// --------------------------------------------------------------------------------------------------------------------
// <copyright file="FeedbackControllerElement.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;

using Microsoft.Extensions.Configuration;

namespace Photon.Common.LoadBalancer.LoadShedding.Configuration
{
    [Serializable]
    public class FeedbackControllerElement
    {
        public FeedbackName Name { get; set; }

        public int InitialInput { get; set; }

        public FeedbackLevel InitialLevel { get; set; }

        public List<FeedbackLevelElement> Levels { get; } = new List<FeedbackLevelElement>();

        public void Deserialize(IConfigurationSection controllerSection)
        {
            this.Levels.Clear();
            var feedBackLevels = controllerSection.GetSection("FeedbackLevels:Level");
            foreach (var levelSection in feedBackLevels.GetChildren())
            {
                var level = levelSection.Get<FeedbackLevelElement>();

                if (level != null)
                {
                    this.Levels.Add(level);
                }
            }
        }
    }
}
