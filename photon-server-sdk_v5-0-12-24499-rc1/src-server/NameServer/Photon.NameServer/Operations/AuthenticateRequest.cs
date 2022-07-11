using System;

using Photon.Common.Authentication;

using Photon.SocketServer;
using Photon.SocketServer.Rpc;

namespace Photon.NameServer.Operations
{

    public class AuthenticateRequest : Operation, IAuthenticateRequest
    {
        #region Constructors and Destructors

        public AuthenticateRequest(IRpcProtocol protocol, OperationRequest operationRequest)
            : base(protocol, operationRequest)
        {
            this.ApplicationId = this.ApplicationId?.Trim();
            this.ApplicationVersion = this.ApplicationVersion?.Trim();
        }

        public AuthenticateRequest()
        {
        }

        #endregion

        #region Properties

        [DataMember(Code = (byte)ParameterKey.ApplicationId, IsOptional = true)]
        public string ApplicationId { get; set; }

        [DataMember(Code = (byte)ParameterKey.AppVersion, IsOptional = true)]
        public string ApplicationVersion { get; set; }

        [DataMember(Code = (byte)ParameterKey.Token, IsOptional = true)]
        public string Token { get; set; }

        [DataMember(Code = (byte)ParameterKey.UserId, IsOptional = true)]
        public string UserId { get; set; }

        [DataMember(Code = (byte)ParameterKey.ClientAuthenticationType, IsOptional = true)]
        public object InternalClientAuthenticationType { get; set; }

        public byte ClientAuthenticationType
        {
            get
            {
                if (this.InternalClientAuthenticationType != null)
                {
                    try
                    {
                        return Convert.ToByte(this.InternalClientAuthenticationType);
                    }
                    catch (Exception)
                    {
                        //nothing to do
                    }
                }

                if (Common.Authentication.Settings.Default.MissingClientAuthenticationTypeIsTypeCustom)
                {
                    return (byte)Common.Authentication.Data.ClientAuthenticationType.Custom;
                }

                return (byte)Common.Authentication.Data.ClientAuthenticationType.None;
            }
            set => this.InternalClientAuthenticationType = value;
        }

        [DataMember(Code = (byte)ParameterKey.ClientAuthenticationParams, IsOptional = true)]
        public string ClientAuthenticationParams { get; set; }

        [DataMember(Code = (byte)ParameterKey.ClientAuthenticationData, IsOptional = true)]
        public object ClientAuthenticationData { get; set; }

        [DataMember(Code = (byte)ParameterKey.Region, IsOptional = false)]
        public string Region { get; set; }

        [DataMember(Code = (byte)ParameterKey.Flags, IsOptional = true)]
        public int Flags { get; set; }

        #endregion
    }
}
