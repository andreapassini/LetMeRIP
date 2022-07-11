// --------------------------------------------------------------------------------------------------------------------
// <copyright file="FeedbackController.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the FeedbackController type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Diagnostics;
    using ExitGames.Logging;

namespace Photon.Common.LoadBalancer.LoadShedding
{
    [DebuggerDisplay("Upper={UpperBound}; Lower={LowerBound})")]
    public struct FeedbackLevelData
    {
        public int UpperBound;
        public int LowerBound;

        public FeedbackLevelData(int up, int down)
        {
            this.UpperBound = up;
            this.LowerBound = down == -1 ? up : down;
        }

        public override string ToString()
        {
            return string.Format("UpperBound:{0},LowerBound:{1}", this.UpperBound, this.LowerBound);
        }
    }

    internal class FeedbackController
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private readonly FeedbackName feedbackName;

        private readonly SortedDictionary<FeedbackLevel, FeedbackLevelData> thresholdValues;

        private FeedbackLevel currentFeedbackLevel;
        private int currentInput;

        public FeedbackController(
            FeedbackName feedbackName, SortedDictionary<FeedbackLevel, FeedbackLevelData> thresholdValues, int initialInput, FeedbackLevel initalFeedbackLevel)
        {
            this.thresholdValues = thresholdValues;
            this.feedbackName = feedbackName;
            this.currentFeedbackLevel = initalFeedbackLevel;
            this.currentInput = initialInput;
        }

        public FeedbackName FeedbackName
        {
            get
            {
                return this.feedbackName;
            }
        }

        public FeedbackLevel Output
        {
            get
            {
                return this.currentFeedbackLevel;
            }
        }

        #region Publics

        public bool SetInput(int input)
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("SetInput: {0} value {1}", this.FeedbackName, input);
            }

            if (input > this.currentInput)
            {
                if (this.currentFeedbackLevel == FeedbackLevel.Highest)
                {
                    return false;
                }

                FeedbackLevel last;
                var next = this.currentFeedbackLevel;
                do
                {
                    last = next;
                    int threshold;
                    this.GetUpperThreshold(last, out threshold);
                    if (input > threshold)
                    {
                        next = this.GetNextHigherThreshold(last, out threshold);
                    }
                } while (next != last);

                this.currentInput = input;
                if (last != this.currentFeedbackLevel)
                {
                    if (log.IsInfoEnabled)
                    {
                        log.InfoFormat("Transit {0} from {1} to {2} with input {3}", this.FeedbackName, this.currentFeedbackLevel, last, input);
                    }

                    this.currentFeedbackLevel = last;
                    return true;
                }
            }
            else if (input < this.currentInput && this.currentFeedbackLevel != FeedbackLevel.Lowest)
            {
                int threshold;

                GetLowerThreshold(this.currentFeedbackLevel, out threshold);

                var next = this.currentFeedbackLevel;

                while (input < threshold && next != FeedbackLevel.Lowest)
                {
                    next = this.GetNextLowerThreshold(next, out threshold);
                }

                this.currentInput = input;
                if (next != this.currentFeedbackLevel)
                {
                    if (log.IsInfoEnabled)
                    {
                        log.InfoFormat("Transit {0} from {1} to {2} with input {3}", this.FeedbackName, this.currentFeedbackLevel, next, input);
                    }

                    this.currentFeedbackLevel = next;
                    return true;
                }
            }

            return false;
        }

        internal void UpdateThresholds(SortedDictionary<FeedbackLevel, FeedbackLevelData> dict)
        {
            foreach (var feedbackLevelData in dict)
            {
                this.thresholdValues[feedbackLevelData.Key] = feedbackLevelData.Value;
            }
            var t = this.currentInput;
            this.currentInput = 0;
            this.currentFeedbackLevel = FeedbackLevel.Lowest;
            this.SetInput(t);
        }

        #endregion

        #region Privates

        private FeedbackLevel GetNextHigherThreshold(FeedbackLevel level, out int result)
        {
            var next = level;

            while (next != FeedbackLevel.Highest)
            {
                next = this.GetNextHigher(next);
                if (this.GetUpperThreshold(next, out result))
                {
                    return next;
                }
            }

            GetUpperThreshold(level, out result);

            return level;
        }

        private FeedbackLevel GetNextLowerThreshold(FeedbackLevel level, out int result)
        {
            var next = level;

            while (next != FeedbackLevel.Lowest)
            {
                next = this.GetNextLower(next);
                if (this.GetLowerThreshold(next, out result))
                {
                    return next;
                }
            }

            this.GetLowerThreshold(next, out result);
            return level;
        }

        private FeedbackLevel GetNextHigher(FeedbackLevel next)
        {
            return next == FeedbackLevel.Highest ? next : next + 1;
        }

        private FeedbackLevel GetNextLower(FeedbackLevel current)
        {
            return current == FeedbackLevel.Lowest ? current : current - 1;
        }

        private bool GetUpperThreshold(FeedbackLevel level, out int result)
        {
            FeedbackLevelData values;
            if (this.thresholdValues.TryGetValue(level, out values))
            {
                result = values.UpperBound;
                return true;
            }

            if (level == FeedbackLevel.Highest)
            {
                result = int.MaxValue;
                return true;
            }

            result = 0;
            return false;
        }

        private bool GetLowerThreshold(FeedbackLevel level, out int result)
        {
            FeedbackLevelData values;
            if (this.thresholdValues.TryGetValue(level, out values))
            {
                result = values.LowerBound;
                return true;
            }
            result = 0;
            return false;
        }

        #endregion
    }
}