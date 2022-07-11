namespace Photon.Common
{
    public enum ErrorCode : short
    {
        InvalidRequestParameters = SocketServer.ErrorCodes.InvalidRequestParameters,
        ArgumentOutOfRange = SocketServer.ErrorCodes.ArgumentOutOfRange,

        OperationDenied = SocketServer.ErrorCodes.OperationDenied,
        OperationInvalid = SocketServer.ErrorCodes.OperationInvalid,
        InternalServerError = SocketServer.ErrorCodes.InternalServerError, 

        Ok = 0,

        InvalidAuthentication = SocketServer.ErrorCodes.PhotonApplicationRangeStart - 0, // 32767,  0x7FFF,  codes start at short.MaxValue 
        GameIdAlreadyExists = SocketServer.ErrorCodes.PhotonApplicationRangeStart - 1, // 32766,  0x7FFF - 1,
        GameFull = SocketServer.ErrorCodes.PhotonApplicationRangeStart - 2,// 32765,  0x7FFF - 2,
        GameClosed = SocketServer.ErrorCodes.PhotonApplicationRangeStart - 3, // 32764, 0x7FFF - 3,
        AlreadyMatched = SocketServer.ErrorCodes.PhotonApplicationRangeStart - 4, // 32763,  0x7FFF - 4,
        ServerFull = SocketServer.ErrorCodes.PhotonApplicationRangeStart - 5, // 32762, 0x7FFF - 5,
        UserBlocked = SocketServer.ErrorCodes.PhotonApplicationRangeStart - 6, // 32761, 0x7FFF - 6,
        NoMatchFound = SocketServer.ErrorCodes.PhotonApplicationRangeStart - 7, // 32760, 0x7FFF - 7,
        RedirectRepeat = SocketServer.ErrorCodes.PhotonApplicationRangeStart - 8, // 32759, 0x7FFF - 8,
        GameIdNotExists = SocketServer.ErrorCodes.PhotonApplicationRangeStart - 9, // 32758, 0x7FFF - 9,

        // for authenticate requests. Indicates that the max ccu limit has been reached
        MaxCcuReached = SocketServer.ErrorCodes.PhotonApplicationRangeStart - 10, // 32757, 0x7FFF - 10,

        // for authenticate requests. Indicates that the application is not subscribed to this region / private cloud. 
        InvalidRegion = SocketServer.ErrorCodes.PhotonApplicationRangeStart - 11, // 32756, 0x7FFF - 11,

        // for authenticate requests. Indicates that the call to the external authentication service failed.
        CustomAuthenticationFailed = SocketServer.ErrorCodes.PhotonApplicationRangeStart - 12, // 32755, 0x7FFF - 12,
        CustomAuthenticationOverload = SocketServer.ErrorCodes.PhotonApplicationRangeStart - 13, // 32754, 0x7FFF - 13,

        AuthenticationTokenExpired = SocketServer.ErrorCodes.PhotonApplicationRangeStart - 14, // 32753, 0x7FFF - 14,
        // for authenticate requests. Indicates that the call to the external authentication service failed.

        PluginReportedError = SocketServer.ErrorCodes.PhotonApplicationRangeStart - 15, // 32752, 0x7FFF - 15,
        PluginMismatch = SocketServer.ErrorCodes.PhotonApplicationRangeStart - 16, // 32751, 0x7FFF - 16,

        JoinFailedPeerAlreadyJoined = SocketServer.ErrorCodes.PhotonApplicationRangeStart - 17, // 32750, 0x7FFF - 17,
        JoinFailedFoundInactiveJoiner = SocketServer.ErrorCodes.PhotonApplicationRangeStart - 18, // 32749, 0x7FFF - 18,
        JoinFailedWithRejoinerNotFound = SocketServer.ErrorCodes.PhotonApplicationRangeStart - 19, // 32748, 0x7FFF - 19,
        JoinFailedFoundExcludedUserId = SocketServer.ErrorCodes.PhotonApplicationRangeStart - 20, // 32747, 0x7FFF - 20,
        JoinFailedFoundActiveJoiner = SocketServer.ErrorCodes.PhotonApplicationRangeStart - 21, // 32746, 0x7FFF - 21,

        HttpLimitReached = SocketServer.ErrorCodes.PhotonApplicationRangeStart - 22, // 32745, 0x7FFF - 22,
        ExternalHttpCallFailed = SocketServer.ErrorCodes.PhotonApplicationRangeStart - 23, // 32744, 0x7FFF - 23,

        OperationLimitReached = SocketServer.ErrorCodes.PhotonApplicationRangeStart - 24, // 32743, 0x7FFF - 24,
        SlotError = SocketServer.ErrorCodes.PhotonApplicationRangeStart - 25, // 32742, 0x7FFF - 25,
        //InvalidEncryptionParameters = SocketServer.ErrorCodes.PhotonApplicationRangeStart - 26, // 32741, 0x7FFF - 26,
        SecureConnectionRequired = SocketServer.ErrorCodes.PhotonApplicationRangeStart - 27, // 32740, 0x7FFF - 27,

        EventCacheExceeded = SocketServer.ErrorCodes.PhotonApplicationRangeStart - 28, // 32739, 0x7FFF - 28,

        ExpectedGSCheckFailure = SocketServer.ErrorCodes.PhotonApplicationRangeStart - 29, // 32738, 0x7FFF - 29,
        ExpectedGameCheckFailure = SocketServer.ErrorCodes.PhotonApplicationRangeStart - 30, // 32737, 0x7FFF - 30,
        AuthRequestWaitTimeout = SocketServer.ErrorCodes.PhotonApplicationRangeStart - 31, // 32736, 0x7FFF - 31,

        ConnectionSwitch = SocketServer.ErrorCodes.PhotonApplicationRangeStart - 32, // 32735, 0x7FFF - 32,
        ActorRemoved = SocketServer.ErrorCodes.PhotonApplicationRangeStart - 33, // 32734, 0x7FFF - 33,
    }
}
