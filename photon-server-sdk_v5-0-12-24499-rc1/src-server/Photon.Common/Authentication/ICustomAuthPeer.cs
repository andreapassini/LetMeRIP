
using Photon.Common.Authentication.CustomAuthentication;
using Photon.SocketServer;

namespace Photon.Common.Authentication
{
    public interface ICustomAuthPeer
    {
        int ConnectionId { get; }

        string UserId { get; set; }

        void OnCustomAuthenticationError(ErrorCode errorCode, string debugMessage, IAuthenticateRequest authenticateRequest, SendParameters sendParameters);

        void OnCustomAuthenticationResult(
            CustomAuthenticationResult customAuthResult, 
            IAuthenticateRequest authenticateRequest, 
            SendParameters sendParameters, 
            object state);
    }
}
