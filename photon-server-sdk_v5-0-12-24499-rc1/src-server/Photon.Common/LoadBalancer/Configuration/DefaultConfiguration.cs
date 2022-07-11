// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DefaultConfiguration.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using Photon.Common.LoadBalancer.LoadShedding;

namespace Photon.Common.LoadBalancer.Configuration
{
    internal static class DefaultConfiguration
    {
        internal static int[] GetDefaultWeights()
        {
            const int levelsCount = (int) FeedbackLevel.LEVELS_COUNT;
            var loadLevelWeights = new int[levelsCount];
            var rest = 100;
            for (var i = 0; i < levelsCount; ++i)
            {
                var step =  ((float)rest/(levelsCount - i));
                 
                loadLevelWeights[i] = (int)(2 * step);

                rest -= loadLevelWeights[i];
            }

            return loadLevelWeights; 
        }
    }
}
