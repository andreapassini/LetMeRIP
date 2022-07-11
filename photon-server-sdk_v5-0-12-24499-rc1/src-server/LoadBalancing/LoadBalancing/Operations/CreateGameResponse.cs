// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CreateGameResponse.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the CreateGameResponse type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using Photon.Hive.Operations;
using Photon.SocketServer.Rpc;

namespace Photon.LoadBalancing.Operations
{
    #region

    #endregion

    /// <summary>
    /// Defines the response paramters for create game requests.
    /// </summary>
    public class CreateGameResponse : JoinGameResponseBase
    {
        #region Properties

        /// <summary>
        /// Gets or sets the address of the game server.
        /// </summary>
        /// <value>The game server address.</value>
        [DataMember(Code = (byte)ParameterKey.Address)]
        public string Address { get; set; }

        /// <summary>
        /// Gets or sets the game id.
        /// </summary>
        /// <value>The game id.</value>
        [DataMember(Code = (byte)ParameterKey.GameId)]
        public string GameId { get; set; }

        #endregion
    }
}