
namespace Photon.LoadBalancing.Operations
{
    public enum JoinRandomType
    {
        /// <summary>Fills up rooms (oldest first) to get players together as fast as possible. Default.</summary>
        /// <remarks>Makes most sense with MaxPlayers > 0 and games that can only start with more players.</remarks>
        FillRoom = 0,
        /// <summary>Distributes players across available rooms sequentially but takes filter into account. Without filter, rooms get players evenly distributed.</summary>
        SerialMatching = 1,
        /// <summary>Joins a (fully) random room. Expected properties must match but aside from this, any available room might be selected.</summary>
        RandomMatching = 2,
        /// <summary>
        /// If SQL lobby matchmaking finds no match for query first (joinable) game is returned
        /// Removed again because Philip doesn't want features that no one uses
        /// </summary>
//        JoinRandomOnSqlNoMatch = 3
    }
}
