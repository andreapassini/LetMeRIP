using System.Collections.Generic;
using Photon.Common.LoadBalancer.LoadShedding;

namespace Photon.Common.LoadBalancer.Prediction.Configuration
{
    internal static class DefaultConfiguration
    {
        internal static SortedDictionary<FeedbackLevel, FeedbackLevelData> GetDefaultControllers()
        {
            return new SortedDictionary<FeedbackLevel, FeedbackLevelData>
                    {
                        { FeedbackLevel.Level0, new FeedbackLevelData(104, 0) },
                        { FeedbackLevel.Level1, new FeedbackLevelData(209, 104) },
                        { FeedbackLevel.Level2, new FeedbackLevelData(314, 209) },
                        { FeedbackLevel.Level3, new FeedbackLevelData(419, 314) },
                        { FeedbackLevel.Level4, new FeedbackLevelData(524, 419) },
                        { FeedbackLevel.Level5, new FeedbackLevelData(629, 524) },
                        { FeedbackLevel.Level6, new FeedbackLevelData(734, 629) },
                        { FeedbackLevel.Level7, new FeedbackLevelData(838, 734) },
                        { FeedbackLevel.Level8, new FeedbackLevelData(943, 838) },
                        { FeedbackLevel.Level9, new FeedbackLevelData(int.MaxValue, 943) }
                    };
        }
    }
}
