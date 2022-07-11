using System.Collections.Generic;
using ExitGames.Logging;
using Photon.Common.LoadBalancer.LoadShedding;

namespace Photon.Common.LoadBalancer.Prediction
{

    public class LoadPrediction 
    {
        #region Constants and Fields

        // ReSharper disable once UnusedMember.Local
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private readonly FeedbackControllerCollection controllerCollection;

        #endregion

        #region .ctor

        public LoadPrediction(byte levelCount, Dictionary<byte, int[]> values)
        {
            var controlers = new List<FeedbackController>();

            foreach (var value in values)
            {
                var dict = new SortedDictionary<FeedbackLevel, FeedbackLevelData>();
                var thresholds = value.Value;
                for (var i = 0; i < thresholds.Length /3; ++i)
                {
                    var upDownValues = new FeedbackLevelData
                    {
                        UpperBound = thresholds[3 * i + 1], LowerBound = thresholds[3 * i + 2]
                    };
                    if (i == (int) FeedbackLevel.Highest)
                    {
                        upDownValues.UpperBound = int.MaxValue;
                    }
                    dict.Add((FeedbackLevel)thresholds[3 * i], upDownValues);
                }

                var controller = new FeedbackController((FeedbackName) value.Key, dict, 0, FeedbackLevel.Lowest);
                controlers.Add(controller);
            }
            this.controllerCollection = new FeedbackControllerCollection(controlers.ToArray());
        }
        
        #endregion

        #region Properties

        public FeedbackLevel Output
        {
            get
            {
                return this.controllerCollection.Output;
            }
        }

        #endregion

        #region Publics

        public void UpdatePredictionLevels(Dictionary<byte, int[]> predictionData)
        {
            foreach (var value in predictionData)
            {
                var dict = new SortedDictionary<FeedbackLevel, FeedbackLevelData>();
                var thresholds = value.Value;
                for (var i = 0; i < thresholds.Length / 3; ++i)
                {
                    var upDownValues = new FeedbackLevelData
                    {
                        UpperBound = thresholds[3 * i + 1],
                        LowerBound = thresholds[3 * i + 2]
                    };
                    if (i == (int)FeedbackLevel.Highest)
                    {
                        upDownValues.UpperBound = int.MaxValue;
                    }
                    dict.Add((FeedbackLevel)thresholds[3 * i], upDownValues);
                }

                this.controllerCollection.UpdateFeedbackController((FeedbackName)value.Key, dict);
            }
        }

        public void SetPeerCount(int peerCount)
        {
            this.controllerCollection.SetInput(FeedbackName.PeerCount, peerCount);
        }

        #endregion

        #region Methods

        #endregion
    }
}
