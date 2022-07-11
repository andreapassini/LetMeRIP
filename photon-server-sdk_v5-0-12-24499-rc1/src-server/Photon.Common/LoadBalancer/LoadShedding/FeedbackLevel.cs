// --------------------------------------------------------------------------------------------------------------------
// <copyright file="FeedbackLevel.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the FeedbackLevel type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Photon.Common.LoadBalancer.LoadShedding
{
    public enum FeedbackLevel
    {
        Lowest = 0,
        Level0 = Lowest,
        Level1 = 1,
        Level2 = 2,
        Level3 = 3,
        Level4 = 4,
        Level5 = 5,
        Level6 = 6,
        Level7 = 7,
        Level8 = 8,
        Level9 = 9,
        Highest = Level9,
        LEVELS_COUNT
    }
}