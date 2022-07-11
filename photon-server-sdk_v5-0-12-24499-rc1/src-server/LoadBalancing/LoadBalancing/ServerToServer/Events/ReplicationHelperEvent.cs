using Photon.LoadBalancing.Operations;
using Photon.SocketServer;
using Photon.SocketServer.Rpc;

namespace Photon.LoadBalancing.ServerToServer.Events
{
    public class ReplicationHelperEvent : DataContract
    {
        #region Constructors and Destructors

        public ReplicationHelperEvent(IRpcProtocol protocol, IEventData eventData)
            : base(protocol, eventData.Parameters)
        {
        }

        #endregion

        #region Properties

        [DataMember(Code = (byte)ParameterCode.GameCount, IsOptional = true)]
        public int ExpectedGamesCount { get; set; }
        #endregion
    }
}
