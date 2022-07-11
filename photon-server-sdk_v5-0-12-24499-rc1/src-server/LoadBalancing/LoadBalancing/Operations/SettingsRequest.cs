using Photon.SocketServer;
using Photon.SocketServer.Rpc;

namespace Photon.LoadBalancing.Operations
{
    public static class SettingsRequestParameters
    {
        public const byte LobbyStats = 0;
    }

    public class SettingsRequest : Operation
    {
        #region .ctr

        public SettingsRequest(IRpcProtocol protocol, OperationRequest operationRequest)
            : base(protocol, operationRequest)
        {
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets a value indicating whether the client wants to receive lobby statistics.
        /// </summary>
        [DataMember(Code = SettingsRequestParameters.LobbyStats, IsOptional = true)]
        public bool? ReceiveLobbyStatistics { get; set; }

        #endregion
    }
}
