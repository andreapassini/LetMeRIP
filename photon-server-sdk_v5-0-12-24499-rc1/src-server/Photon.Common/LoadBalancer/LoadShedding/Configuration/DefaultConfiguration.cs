// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DefaultConfiguration.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Photon.Common.LoadBalancer.LoadShedding.Configuration
{
    using System.Collections.Generic;

    internal class DefaultConfiguration
    {
        internal static List<FeedbackController> GetDefaultControllers()
        {
            var cpuController = new FeedbackController(
            FeedbackName.CpuUsage,
            new SortedDictionary<FeedbackLevel, FeedbackLevelData>
                    {
                        { FeedbackLevel.Level0, new FeedbackLevelData(10, 0) },
                        { FeedbackLevel.Level1, new FeedbackLevelData(20, 9 ) },
                        { FeedbackLevel.Level2, new FeedbackLevelData(30, 19) },
                        { FeedbackLevel.Level3, new FeedbackLevelData(40, 29) },
                        { FeedbackLevel.Level4, new FeedbackLevelData(50, 38) },
                        { FeedbackLevel.Level5, new FeedbackLevelData(60, 48) },
                        { FeedbackLevel.Level6, new FeedbackLevelData(70, 57) },
                        { FeedbackLevel.Level7, new FeedbackLevelData(80, 67) },
                        { FeedbackLevel.Level8, new FeedbackLevelData(90, 77) },
                        { FeedbackLevel.Level9, new FeedbackLevelData(int.MaxValue, 77) }
                    },
            0,
            FeedbackLevel.Lowest);
      
        const int megaByte = 1024 * 1024;
        var thresholdValues = new SortedDictionary<FeedbackLevel, FeedbackLevelData> 
                {
                    { FeedbackLevel.Level0, new FeedbackLevelData(megaByte, 0) }, 
                    { FeedbackLevel.Level5, new FeedbackLevelData(4 * megaByte, megaByte - megaByte / 100)  }, 
                    { FeedbackLevel.Level8, new FeedbackLevelData(8 * megaByte, 4 * megaByte - megaByte / 100) }, 
                    { FeedbackLevel.Level9, new FeedbackLevelData(10 * megaByte, 8 * megaByte - megaByte / 100) }
                };
        var bandwidthController = new FeedbackController(FeedbackName.Bandwidth, thresholdValues, 0, FeedbackLevel.Lowest);

            return new List<FeedbackController> { cpuController, bandwidthController }; 
        }
    }
}
