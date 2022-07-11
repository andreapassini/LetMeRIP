
namespace Photon.Common.Authentication
{
    /// <summary>
    /// Defines error codes for authentication requests.
    /// </summary>
    public enum AuthErrorCode
    {
        /// <summary>The authentication request succeeded.</summary>
        Ok = 0,

        /// <summary>There is currently no master server instance available for the specified application id.</summary>
        ApplicationOffline = 1,

        /// <summary>The authentikation token was not set in the authentication request.</summary>
        AuthTokenMissing = 2,

        /// <summary>The network protocol used by the client is not supported.</summary>
        ProtocolNotSupported = 3,
       
    }
}
