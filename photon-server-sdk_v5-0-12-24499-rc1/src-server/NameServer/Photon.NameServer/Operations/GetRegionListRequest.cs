// --------------------------------------------------------------------------------------------------------------------
// <copyright file="GetRegionListRequest.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the GetRegionListRequest type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using Photon.SocketServer;
using Photon.SocketServer.Rpc;

namespace Photon.NameServer.Operations
{
    public class GetRegionListRequest : Operation
    {
        public GetRegionListRequest(IRpcProtocol protocol, OperationRequest request)
            : base(protocol, request)
        {
        }

        //Appid check for cloud NS
        public bool ValidateApplicationId()
        {
            if (string.IsNullOrEmpty(this.ApplicationId))
            {
                this.errorMessage = Photon.Common.Authentication.ErrorMessages.EmptyAppId;
                return false;
            }
            return true;
        }

        public GetRegionListRequest()
        {
        }

        [DataMember(Code = (byte)ParameterKey.ApplicationId, IsOptional = true)]
        public string ApplicationId { get; set; }
    }
}