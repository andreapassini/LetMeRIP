namespace Photon.Common.Authentication
{
    public static class ErrorMessages
    {
        public const string EmptyAppId = "Empty application id";

        public const string InternalError = "Internal server error";

        public const string AuthTokenMissing = "Authentication token is missing";

        public const string AuthTokenInvalid = "Invalid authentication token";

        public const string AuthTokenExpired = "Authentication token expired";

        public const string AuthTokenTypeNotSupported = "Authentication token type not supported";

        public const string ProtocolNotSupported = "Network protocol not supported";

        public const string InvalidTypeForAuthData = "Invalid type for auth data";

        public const string InvalidEncryptionData = "Encryption data are incomplete. ErrorMsg:{0}";

        public const string InvalidAuthenticationType = "Invalid authentication type. Only Token auth supported by master and gameserver";

        public const string ServerIsNotReady = "Server is not ready. Try to reconnect later";

        public const string InvalidEncryptionMode = "Requested encryption mode is not supported by server. RequestedMode={0}";

        public const string ExpectedGSCheckFailure = "Wrong game server used to create/join game";

        public const string TooManyPropertiesSetByPeer = "Peer got used too many uniq property keys";

        public const string NoAuthRequestInExpectedTime = "No auth request during expected wait time";
    }
}