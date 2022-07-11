// --------------------------------------------------------------------------------------------------------------------
// <copyright file="RegisterGameServerResponse.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the RegisterGameServerResponse type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System.Collections.Generic;

namespace Photon.LoadBalancing.ServerToServer.Operations
{
    using System.Collections;

    using Photon.SocketServer;
    using Photon.SocketServer.Rpc;

    public class RegisterGameServerResponse : DataContract
    {
        #region Constructors and Destructors

        public RegisterGameServerResponse(IRpcProtocol protocol, OperationResponse response)
            : base(protocol, response.Parameters)
        {
        }

        protected RegisterGameServerResponse(IRpcProtocol protocol, Dictionary<byte, object> dataContrct)
            : base(protocol, dataContrct)
        {
        }

        public RegisterGameServerResponse()
        {
        }

        #endregion

        #region Properties

        [DataMember(Code = 1, IsOptional = true)]
        public byte[] ExternalAddress { get; set; }

        [DataMember(Code = 2)]
        public byte[] InternalAddress { get; set; }

        #endregion
    }

    public class RegisterGameServerInitResponse : RegisterGameServerResponse
    {
        public RegisterGameServerInitResponse(IRpcProtocol protocol, Dictionary<byte, object> dataContrct)
            : base(protocol, dataContrct)
        {
        }

        public RegisterGameServerInitResponse()
        { }

        [DataMember(Code = 10)]
        public short ReturnCode { get; set; }

        [DataMember(Code = 11, IsOptional = true)]
        public string DebugMessage { get; set; }

    }
}