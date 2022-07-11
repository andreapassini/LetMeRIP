using System;
using Photon.Common.Authentication;
using Photon.SocketServer;
using Photon.SocketServer.Rpc;
using Photon.SocketServer.Security;

namespace Photon.NameServer.Operations
{
    public class AuthOnceRequest : AuthenticateRequest, IAuthOnceRequest
    {
        public AuthOnceRequest(IRpcProtocol protocol, OperationRequest operationRequest)
            : base(protocol, operationRequest)
        {
            if (!Enum.IsDefined(typeof(EncryptionModes), this.EncryptionMode))
            {
                this.isValid = false;
                this.errorMessage = string.Format(ErrorMessages.InvalidEncryptionMode, this.EncryptionMode);
            }
        }

        public AuthOnceRequest()
        {
        }

        [DataMember(Code = (byte)ParameterKey.ExpectedProtocol, IsOptional = false)]
        public byte Protocol { get; set; }

        [DataMember(Code = (byte)ParameterKey.EncryptionMode, IsOptional = false)]
        public byte EncryptionMode { get; set; }
    }
}
