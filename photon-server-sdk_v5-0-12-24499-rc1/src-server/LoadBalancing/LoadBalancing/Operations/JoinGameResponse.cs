// --------------------------------------------------------------------------------------------------------------------
// <copyright file="JoinGameResponse.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the JoinGameResponse type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System.Collections;
using Photon.Hive.Operations;
using Photon.SocketServer.Rpc;

namespace Photon.LoadBalancing.Operations
{
    #region

    #endregion

    /// <summary>
    /// used by master code
    /// and by tests to not create extra overloads for JoinGame methods
    /// </summary>
    public class JoinGameResponse : JoinGameResponseBase
    {
        #region Properties

        [DataMember(Code = (byte)ParameterKey.Address, IsOptional = false)]
        public string Address { get; set; }

        [DataMember(Code = (byte)ParameterKey.RoomOptionFlags, IsOptional = true)]
        public int? RoomFlags { get; set; }

        [DataMember(Code = (byte)ParameterKey.GameProperties, IsOptional = true)]
        public Hashtable GameProperties { get; set; }

        [DataMember(Code = (byte)ParameterKey.ActorProperties, IsOptional = true)]
        public Hashtable ActorsProperties { get; set; }
        #endregion
    }
}