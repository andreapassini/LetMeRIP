// --------------------------------------------------------------------------------------------------------------------
// <copyright file="GetRegionListResponse.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the GetRegionListResponse type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using Photon.SocketServer.Rpc;

namespace Photon.NameServer.Operations
{
    public class GetRegionListResponse : DataContract
    {
        public GetRegionListResponse()
        {
        }

        [DataMember(Code = (byte)ParameterKey.Region, IsOptional = true)]
        public string[] Region { get; set; }

        [DataMember(Code = (byte)ParameterKey.Address, IsOptional = true)]
        public string[] Endpoints { get; set; }
    }
}
