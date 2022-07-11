// --------------------------------------------------------------------------------------------------------------------
// <copyright file="GetGameListResponse.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the AuthenticateResponse type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Photon.LoadBalancing.Operations
{
    #region using directives

    using System.Collections;
    using System.Collections.Generic;
    using Photon.Hive.Operations;
    using Photon.SocketServer;
    using Photon.SocketServer.Rpc;

    #endregion

    public class GetGameListResponse
    {
        [DataMember(Code = (byte)ParameterKey.GameList, IsOptional = false)]
        public Hashtable GameList { get; set; }
    }
}
