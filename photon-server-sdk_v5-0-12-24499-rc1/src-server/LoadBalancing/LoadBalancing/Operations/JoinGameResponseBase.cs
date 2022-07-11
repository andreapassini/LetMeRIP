using Photon.SocketServer.Rpc;

namespace Photon.LoadBalancing.Operations
{
    public class JoinGameResponseBase
    {
        #region Properties
        /// <summary>
        /// Gets or sets the authentication token.
        /// </summary>
        /// <value>The authentication token.</value>
        [DataMember(Code = (byte)ParameterCode.Token, IsOptional = false)]
        public object AuthenticationToken { get; set; }

        #endregion

    }
}
