// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CreateGameRequest.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the CreateGameRequest type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Photon.Hive.Operations
{
    #region using directives

    using Photon.SocketServer;
    using Photon.SocketServer.Rpc;

    #endregion

    /// <summary>
    /// we use CreateGameRequest only on master, because only on master it is allowed to NOT set GameId. it will be set automatically in this case
    /// on GS we use JoinGameRequest even in case of game creation. because it does not allow empty game Id
    /// </summary>
    public class CreateGameRequest : JoinGameRequest
    {
        public CreateGameRequest(IRpcProtocol protocol, OperationRequest operationRequest, string userId, int maxPropertySize)
            : base(protocol, operationRequest, userId, maxPropertySize)
        {
        }

        public CreateGameRequest()
        {
        }

        [DataMember(Code = (byte)ParameterKey.GameId, IsOptional = true)]
        public override string GameId { get; set; }
    }
}