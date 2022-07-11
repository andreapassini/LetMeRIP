// --------------------------------------------------------------------------------------------------------------------
// <copyright file="GetPropertiesRequest.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   The get properties request.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Photon.Hive.Operations
{
    using System;
    using System.Collections;
    using System.Linq;
    using Photon.Hive.Common;
    using Photon.SocketServer;
    using Photon.SocketServer.Rpc;
    using Photon.SocketServer.Rpc.Protocols;

    /// <summary>
    ///   The get properties request.
    /// </summary>
    public class GetPropertiesRequest : Operation
    {
        #region Constructors and Destructors

        /// <summary>
        ///   Initializes a new instance of the <see cref = "GetPropertiesRequest" /> class.
        /// </summary>
        /// <param name = "protocol">
        ///   The protocol.
        /// </param>
        /// <param name = "operationRequest">
        ///   Operation request containing the operation parameters.
        /// </param>
        public GetPropertiesRequest(IRpcProtocol protocol, OperationRequest operationRequest)
            : base(protocol, operationRequest)
        {
            // special treatment for game and actor properties sent by JSON clients
            if (protocol.ProtocolType == ProtocolType.Json)
            {
                Utilities.ConvertAs3WellKnownPropertyKeys(this.GamePropertyKeys, this.ActorPropertyKeys);
            }

            if (this.ActorNumbers != null && this.ActorNumbers.Length > 1)
            {
                this.ActorNumbers = this.ActorNumbers.Distinct().ToArray();
            }

            this.ActorPropertyKeys = RemoveNullsAndDuplicates(this.ActorPropertyKeys);
            this.GamePropertyKeys = RemoveNullsAndDuplicates(this.GamePropertyKeys);
        }

        /// <summary>
        ///   Initializes a new instance of the <see cref = "GetPropertiesRequest" /> class.
        /// </summary>
        public GetPropertiesRequest()
        {
        }

        #endregion

        #region Properties

        /// <summary>
        ///   Gets or sets the actor numbers for which to get the properties.
        /// </summary>
        [DataMember(Code = (byte)ParameterKey.Actors, IsOptional = true)]
        public int[] ActorNumbers { get; set; }

        /// <summary>
        ///   Gets or sets ActorPropertyKeys.
        /// </summary>
        [DataMember(Code = (byte)ParameterKey.ActorProperties, IsOptional = true)]
        public object[] ActorPropertyKeys { get; set; }

        /// <summary>
        ///   Gets or sets GamePropertyKeys.
        /// </summary>
        [DataMember(Code = (byte)ParameterKey.GameProperties, IsOptional = true)]
        public object[] GamePropertyKeys { get; set; }

        /// <summary>
        ///   Gets or sets PropertyType.
        /// </summary>
        [DataMember(Code = (byte)ParameterKey.Properties, IsOptional = true)]
        public byte PropertyType { get; set; }

        #endregion

        #region Methods
        private static object[] RemoveNullsAndDuplicates(object[] propertyKeys)
        {
            if (propertyKeys != null && propertyKeys.Length > 0)
            {
                return propertyKeys.Where(o => o != null).Distinct().ToArray();
            }

            return propertyKeys;
        }

        #endregion
    }
}