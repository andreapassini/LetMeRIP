namespace Photon.Hive.Plugin
{
    /// <summary>
    /// Code of the reasons why a peer may leave a room.
    /// </summary>
    public static class LeaveReason
    {
        /// <summary>
        /// Indicates that the client called Disconnect()
        /// </summary>
        public const byte ClientDisconnect = 0;
        
        /// <summary>
        ///  Indicates that client has timed-out server. This is valid only when using UDP/ENET.
        /// </summary>
        public const byte ClientTimeoutDisconnect = 1;
        
        /// <summary>
        /// Indicates client is too slow to handle data sent or there is some reason why application called peer.Disconnect
        /// </summary>
        public const byte ManagedDisconnect = 2;
        
        /// <summary>
        /// Indicates low level protocol error which can be caused by data corruption.
        /// </summary>
        public const byte ServerDisconnect = 3;
        
        /// <summary>
        /// Indicates that the server has timed-out client. 
        /// </summary>
        public const byte TimeoutDisconnect = 4;

        /// <summary>
        /// TBD: Not used currently.
        /// </summary>
        public const byte ConnectTimeout = 5;

        /// <summary>
        /// TBD: Not used currently.
        /// </summary>
        public const byte SwitchRoom = 100;
        
        /// <summary>
        /// Indicates that the client called OpLeave().
        /// </summary>
        public const byte LeaveRequest = 101;

        /// <summary>
        /// Indicates that the inactive actor timed-out, meaning the PlayerTtL of the room expired for that actor. 
        /// </summary>
        public const byte PlayerTtlTimedOut = 102;

        /// <summary>
        /// Indicates a very unusual scenario where the actor did not send anything to Photon Servers for 5 minutes. 
        /// Normally peers timeout long before that but Photon does a check for every connected peer's timestamp of 
        /// the last exchange with the servers (called LastTouch) every 5 minutes.
        /// </summary>
        public const byte PeerLastTouchTimedout = 103;

        /// <summary>
        /// Indicates that the actor was removed from ActorList by a plugin.
        /// </summary>
        public const byte PluginRequest = 104;

        /// <summary>
        /// Indicates an internal error in a plugin implementation.
        /// </summary>
        public const byte PluginFailedJoin = 105;

        #region ErrorCode
        
        /// <summary> Indicates the client sent too much data in a short period of time. </summary>
        public const short SendBufferFull = -11; // C

        /// <summary> Indicates that the client was disconnected by server because of a blocked operation. </summary>
        public const short OperationDenied = -3;
        /// <summary> Indicates that the client was disconnected by server because of an invalid operation. </summary>
        public const short OperationInvalid = -2;
        /// <summary> Indicates that the client was disconnected due to an internal server error. </summary>
        public const short InternalServerError = -1;
        
        /// <summary> Indicates that the client could not join because room just became full. </summary>
        public const short GameFull = 32765;
        /// <summary> Indicates that the client could not join because room was just closed. </summary>
        public const short GameClosed = 32764;
        
        /// <summary> Indicates that the client could not join or was disconnected because a plugin reported an error. </summary>
        public const short PluginReportedError = 32752; // R
        
        /// <summary> Indicates that the client could not join because already joined with same session token. </summary>
        public const short JoinFailedPeerAlreadyJoined = 32750;
        /// <summary> Indicates that the client could not join because another inactive actor with same UserId or ActorNr exists in room. </summary>
        public const short JoinFailedFoundInactiveJoiner = 32749;
        /// <summary> Indicates that the client could not rejoin because there is no inactive actor with same UserId or ActorNr in room. </summary>
        public const short JoinFailedWithRejoinerNotFound = 32748;
        /// <summary> Indicates that the client could not rejoin because UserId is in excluded users list. </summary>
        public const short JoinFailedFoundExcludedUserId = 32747;
        /// <summary> Indicates that the client could not join because another client with same UserId or ActorNr already joined. </summary>
        public const short JoinFailedFoundActiveJoiner = 32746;
        /// <summary> Indicates that the client was disconnected because a HTTP limit was reached. </summary>
        public const short HttpLimitReached = 32745;

        /// <summary> Indicates that the client was disconnected because an operation limit was reached. </summary>
        public const short OperationLimitReached = 32743;
        /// <summary> Indicates that the client could not join because room does not have enough spots for all expected users. </summary>
        public const short SlotError = 32742;
        
        /// <summary> Indicates that the client was disconnected because an events cache limit was reached. </summary>
        public const short EventCacheExceeded = 32739;
        
        /// <summary> Indicates that the disconnected client was swapped for the active actor in the room because of a quick rejoin (using same session token). </summary>
        public const short ConnectionSwitch = 32735;
        /// <summary> Indicates the client was disconnected because the corresponding actor was removed from a room. </summary>
        public const short ActorRemoved = 32734;
        
        #endregion

        /// <summary>
        /// Stringify the leave reason code
        /// </summary>
        /// <param name="reason">Leave reason code</param>
        /// <returns>readable form of the leave reason</returns>
        public static string ToString(int reason)
        {
            switch (reason)
            {
                case ClientDisconnect:
                {
                    return "ClientDisconnect";
                }
                case ClientTimeoutDisconnect:
                {
                    return "ClientTimeoutDisconnect";
                }
                case ManagedDisconnect:
                {
                    return "ManagedDisconnect";
                }
                case ServerDisconnect:
                {
                    return "ServerDisconnect";
                }
                case TimeoutDisconnect:
                {
                    return "TimeoutDisconnect";
                }
                case ConnectTimeout:
                {
                    return "ConnectTimeout";
                }
                case SwitchRoom:
                {
                    return "SwitchRoom";
                }
                case LeaveRequest:
                {
                    return "LeaveRequest";
                }
                case PlayerTtlTimedOut:
                {
                    return "PlayerTtlTimedOut";
                }
                case PeerLastTouchTimedout:
                {
                    return "PeerLastTouchTimedout";
                }
                case PluginRequest:
                {
                    return "PluginRequest";
                }
                case PluginFailedJoin:
                {
                    return "PluginFailedJoin";
                }
                case SendBufferFull:
                {
                    return nameof(SendBufferFull);
                }
                case OperationDenied:
                {
                    return nameof(OperationDenied);
                }
                case OperationInvalid:
                {
                    return nameof(OperationInvalid);
                }
                case InternalServerError:
                {
                    return nameof(InternalServerError);
                }
                case GameFull:
                {
                    return nameof(GameFull);
                }
                case GameClosed:
                {
                    return nameof(GameClosed);
                }
                case PluginReportedError:
                {
                    return nameof(PluginReportedError);
                }
                case JoinFailedPeerAlreadyJoined:
                {
                    return nameof(JoinFailedPeerAlreadyJoined);
                }
                case JoinFailedFoundInactiveJoiner:
                {
                    return nameof(JoinFailedFoundInactiveJoiner);
                }
                case JoinFailedWithRejoinerNotFound:
                {
                    return nameof(JoinFailedWithRejoinerNotFound);
                }
                case JoinFailedFoundExcludedUserId:
                {
                    return nameof(JoinFailedFoundExcludedUserId);
                }
                case JoinFailedFoundActiveJoiner:
                {
                    return nameof(JoinFailedFoundActiveJoiner);
                }
                case HttpLimitReached:
                {
                    return nameof(HttpLimitReached);
                }
                case OperationLimitReached:
                {
                    return nameof(OperationLimitReached);
                }
                case SlotError:
                {
                    return nameof(SlotError);
                }
                case EventCacheExceeded:
                {
                    return nameof(EventCacheExceeded);
                }
                case ConnectionSwitch:
                {
                    return nameof(ConnectionSwitch);
                }
                case ActorRemoved:
                {
                    return nameof(ActorRemoved);
                }
                default:
                {
                    return "Unknown:" + reason;
                }
            }
        }
    }
}