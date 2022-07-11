
namespace Photon.LoadBalancing.Operations
{
    using Photon.SocketServer;
    using Photon.SocketServer.Rpc;
    using System;

    public static class FindFriendsOptions
    {
        public const int Default     = 0x00;
        public const int CreatedOnGS = 0x01;
        public const int Visible     = 0x02;
        public const int Open        = 0x04;
    }
    public class FindFriendsRequest : Operation
    {
        public FindFriendsRequest()
        {
        }

        public FindFriendsRequest(IRpcProtocol protocol, OperationRequest operationRequest)
            : base(protocol, operationRequest)
        {
        }

        [DataMember(Code=1, IsOptional=false)]
        public string[] UserList { get; set; }

        [DataMember(Code=2, IsOptional=true)]
        public int OperationOptions { get; set; }
    }
}
