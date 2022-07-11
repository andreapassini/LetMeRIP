
namespace Photon.NameServer.Operations
{
    using System.Collections.Generic;

    using Photon.SocketServer;
    using Photon.SocketServer.Rpc;

    public class AuthenticateResponse : DataContract
    {
        public AuthenticateResponse()
        {
        }

        public AuthenticateResponse(IRpcProtocol protocol, Dictionary<byte, object> parameter)
            : base(protocol, parameter)
        {
        }

        [DataMember(Code = (byte)ParameterKey.Address, IsOptional = true)]
        public string MasterEndpoint { get; set; }

        [DataMember(Code = (byte)ParameterKey.Token, IsOptional = true)]
        public object AuthenticationToken { get; set; }

        [DataMember(Code = (byte)ParameterKey.Data, IsOptional = true)]
        public Dictionary<string, object> Data { get; set; }

        [DataMember(Code = (byte)ParameterKey.Nickname, IsOptional = true)]
        public string Nickname { get; set; }

        [DataMember(Code = (byte)ParameterKey.UserId, IsOptional = true)]
        public string UserId { get; set; }
        
        [DataMember(Code = (byte)ParameterKey.EncryptionData, IsOptional = true)]
        public Dictionary<byte, object> EncryptionData { get; set; }
    }
}
