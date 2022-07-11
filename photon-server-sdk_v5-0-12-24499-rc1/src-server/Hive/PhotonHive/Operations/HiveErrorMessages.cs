namespace Photon.Hive.Operations
{
    public class HiveErrorMessages
    {
        public const string OperationIsNotAllowedOnThisJoinStage = "Operation is not allowed on this join stage";

        public const string PeerNotJoinedToRoom = "Room not joined";

        public const string CacheSliceNoAviable = "Requested cache slice={0} not available.";

        public const string MaxTTLExceeded = "Can not create game with EmptyRoomTtl={0} max allowed is {1}.";
        public const string SetPropertiesMaxTTLExceeded = "Can not set EmptyRoomTtl={0}. max allowed is {1}.";

        public const string InvalidReceiverGroup = "Invalid ReceiverGroup ";

        public const string ActorNotFound = "Actor with number {0} not found.";
        public const string ActorJoiningNotComplete = "Joining of actor with number {0} is not complete. Broadcast is not allowed";

        public const string HttpForwardedOperationsLimitReached = "Limit ({0} per second) of WebRPC requests or operation calls with HttpForward flag are reached";

        public const string CantAddSlots = "Server can not add expected users to game";

        public const string UserAlreadyJoined = "Join failed: UserId '{0}' already joined the specified game (JoinMode={1}).";
        public const string UserWithActorIdAlreadyJoined = "Join failed: ActorId '{0}' already joined the specified game (JoinMode={1}).";

        public const string GameIdDoesNotExist = "Game does not exist";

        public const string GameClosed = "Game closed";

        public const string GameFull = "Game full";

        public const string ReinitGameFailed = "Reinit game failed";

        public const string InvalidOperationCode = "Invalid operation code";

        public const string UserNotFound = "User does not exist in this game";

        public const string CanNotUseRejoinOrJoinIfPlayerExpected = "Expected users does not support JoinMode=2";

        public const string CanNotRejoinGameDoesNotSupportRejoin = "Room does not support rejoing. PlayerTTL is 0";

        public const string JoinFailedFoundExcludedUserId = "UserId found in excluded list";

        public const string GameAlreadyExist = "A game with the specified id already exist.";

        public const string WebRpcIsNotEnabled = "WebRPC is not enabled";

        public const string SimulatesPhotonEvent = "Raising of photon events (higher than {0}) is not allowed";

        public const string EmptyPropertiesCollection = "Properties collection can not be empty for SetProperties operation";

        public const string GameClosedCacheDiscarded = "Game closed because of reaching of cached events limit";

        public const string RejoiningBlockedCacheExceeded = "Rejoining blocked because cached events limit was reached";

        public const string RoomClosedCachedEventsLimitExceeded = "Room closed because of exceeding event cache limits";

        public const string RoomClosedPropertiesSizeLimitExceeded = "Room closed because of exceeding properties size limit";

        public const string CachedEventsLimitExceeded = "Rooms event cache limit exceeded";

        public const string SlotCanNotHaveEmptyName = "Slot name can not be empty";

        public const string UsageOfSetPropertiesNotAllowedBeforeContinue = "Usage of SetProperties is prohibited before calling ICrateGameCallInfo.Coninue";

        public const string MasterClientIdIsNotAllowedThroughCreationOrJoin = "It is not allowed to set MasterClientId through Create/Join Game request";

        public const string ExpectedGameCheckFailure = "Wrong game Id used to create/join game";

        public const string UnknownOperationCode = "Unknown operation code";

        public const string MatchMakingActivityLimitsExceeded = "It is not allowed to create/join games anymore";
    }
}
