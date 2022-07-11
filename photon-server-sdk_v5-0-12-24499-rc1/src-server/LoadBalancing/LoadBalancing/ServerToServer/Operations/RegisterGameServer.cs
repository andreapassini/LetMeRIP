// --------------------------------------------------------------------------------------------------------------------
// <copyright file="RegisterGameServer.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the parameters which should be send from game server instances to
//   register at the master application.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System.Collections.Generic;

using Photon.SocketServer;
using Photon.SocketServer.Rpc;

namespace Photon.LoadBalancing.ServerToServer.Operations
{
    public interface IRegisterGameServer
    {
        /// <summary>
        ///   Gets or sets the public game server ip address.
        /// </summary>
        string GameServerAddress { get; set; }

        /// <summary>
        ///   Gets or sets a unique server id.
        ///   This id is used to sync reconnects.
        /// </summary>
        string ServerId { get; set; }

        /// <summary>
        ///   Gets or sets the TCP port of the game server instance.
        /// </summary>
        /// <value>The TCP port.</value>
        int? TcpPort { get; set; }

        /// <summary>
        ///   Gets or sets the UDP port of the game server instance.
        /// </summary>
        /// <value>The UDP port.</value>
        int? UdpPort { get; set; }

        /// <summary>
        ///   Gets or sets the port of the game server instance used for WebSocket connections.
        /// </summary>
        int? WebSocketPort { get; set; }

        /// <summary>
        ///   Gets or sets the initial server state of the game server instance.
        /// </summary>
        int ServerState { get; set; }

        /// <summary>
        ///   Gets or sets the port of the game server instance used for secure WebSocket connections.
        /// </summary>
        int? SecureWebSocketPort { get; set; }

        /// <summary>
        ///   Gets or sets the path of the game server application instance used for ws/wss connections.
        /// </summary>
        string GamingWsPath { get; set; }

        int? WebRTCPort { get; set; }
        
        /// <summary>
        ///   Gets or sets the public game server ip address.
        /// </summary>
        string GameServerAddressIPv6 { get; set; }

        /// <summary>
        ///   Gets or sets the fully qualified public host name of the game server instance (used for WebSocket connections).
        /// </summary>
        string GameServerHostName { get; set; }

        /// <summary>
        ///   Gets prediction data which were collected on GS
        /// </summary>
        Dictionary<byte, int[]> PredictionData { get; set; }

        /// <summary>
        ///   Gets how many load levels used by GS
        /// </summary>
        byte LoadLevelCount { get; set; }

        /// <summary>
        ///   Defines priority server belongs to
        /// </summary>
        byte LoadBalancerPriority { get; set; }

        /// <summary>
        /// current load level
        /// </summary>
        byte LoadIndex { get; set; }

        /// <summary>
        /// List of protocols supported by Game Server
        /// </summary>
        byte[] SupportedProtocols { get; set; }
    }

    /// <summary>
    ///   Defines the parameters which should be send from game server instances to 
    ///   register at the master application.
    /// </summary>
    public class RegisterGameServer : Operation, IRegisterGameServer
    {
        #region Constructors and Destructors

        /// <summary>
        ///   Initializes a new instance of the <see cref = "RegisterGameServer" /> class.
        /// </summary>
        /// <param name = "rpcProtocol">
        ///   The rpc Protocol.
        /// </param>
        /// <param name = "operationRequest">
        ///   The operation request.
        /// </param>
        public RegisterGameServer(IRpcProtocol rpcProtocol, OperationRequest operationRequest)
            : base(rpcProtocol, operationRequest)
        {
        }

        /// <summary>
        ///   Initializes a new instance of the <see cref = "RegisterGameServer" /> class.
        /// </summary>
        public RegisterGameServer()
        {
        }

        #endregion

        #region Properties

        /// <summary>
        ///   Gets or sets the public game server ip address.
        /// </summary>
        [DataMember(Code = 4, IsOptional = false)]
        public string GameServerAddress { get; set; }

        //[DataMember(Code = 5, IsOptional = true)]
        //public byte LocalNode { get; set; }

        /// <summary>
        ///   Gets or sets a unique server id.
        ///   This id is used to sync reconnects.
        /// </summary>
        [DataMember(Code = 3, IsOptional = false)]
        public string ServerId { get; set; }

        /// <summary>
        ///   Gets or sets the TCP port of the game server instance.
        /// </summary>
        /// <value>The TCP port.</value>
        [DataMember(Code = 2, IsOptional = true)]
        public int? TcpPort { get; set; }

        /// <summary>
        ///   Gets or sets the UDP port of the game server instance.
        /// </summary>
        /// <value>The UDP port.</value>
        [DataMember(Code = 1, IsOptional = true)]
        public int? UdpPort { get; set; }

        /// <summary>
        ///   Gets or sets the port of the game server instance used for WebSocket connections.
        /// </summary>
        [DataMember(Code = 6, IsOptional = true)]
        public int? WebSocketPort { get; set; }

        /// <summary>
        ///   Gets or sets the initial server state of the game server instance.
        /// </summary>
        [DataMember(Code = 7, IsOptional = true)]
        public int ServerState { get; set; }

        /// <summary>
        ///   Gets or sets the port of the game server instance used for secure WebSocket connections.
        /// </summary>
        [DataMember(Code = 9, IsOptional = true)]
        public int? SecureWebSocketPort { get; set; }

        /// <summary>
        ///   Gets or sets the public game server ip address.
        /// </summary>
        [DataMember(Code = 12, IsOptional = true)]
        public string GameServerAddressIPv6 { get; set; }


        /// <summary>
        ///   Gets or sets the fully qualified public host name of the game server instance (used for WebSocket connections).
        /// </summary>
        [DataMember(Code = 13, IsOptional = true)]
        public string GameServerHostName { get; set; }

        /// <summary>
        ///   Gets prediction data which were collected on GS
        /// </summary>
        [DataMember(Code = 14, IsOptional = true)]
        public Dictionary<byte, int[]> PredictionData { get; set; }

        /// <summary>
        ///   Gets how many load levels used by GS
        /// </summary>
        [DataMember(Code = 15, IsOptional = true)]
        public byte LoadLevelCount { get; set; }

        /// <summary>
        ///   Defines priority server belongs to
        /// </summary>
        [DataMember(Code = 16, IsOptional = true)]
        public byte LoadBalancerPriority { get; set; }

        [DataMember(Code = 17, IsOptional = true)]
        public int? WebRTCPort { get; set; }

        [DataMember(Code = 18, IsOptional = true)]
        public byte LoadIndex { get; set; }

        [DataMember(Code = 19, IsOptional = true)]
        public byte[] SupportedProtocols { get; set; }

        [DataMember(Code = 20, IsOptional = true)]
        public string GamingWsPath { get; set; }

        #endregion
    }

    public class RegisterGameServerDataContract : DataContract, IRegisterGameServer
    {
        #region Constructors and Destructors

        /// <summary>
        ///   Initializes a new instance of the <see cref = "RegisterGameServer" /> class.
        /// </summary>
        /// <param name = "rpcProtocol">
        ///   The rpc Protocol.
        /// </param>
        /// <param name = "parameters">
        ///   dictionary from which we will take parameters.
        /// </param>
        public RegisterGameServerDataContract(IRpcProtocol rpcProtocol, Dictionary<byte, object> parameters)
            : base(rpcProtocol, parameters)
        {
        }

        /// <summary>
        ///   Initializes a new instance of the <see cref = "RegisterGameServer" /> class.
        /// </summary>
        public RegisterGameServerDataContract()
        {
        }

        #endregion

        #region Properties

        /// <summary>
        ///   Gets or sets the public game server ip address.
        /// </summary>
        [DataMember(Code = 4, IsOptional = false)]
        public string GameServerAddress { get; set; }

        //[DataMember(Code = 5, IsOptional = true)]
        //public byte LocalNode { get; set; }

        /// <summary>
        ///   Gets or sets a unique server id.
        ///   This id is used to sync reconnects.
        /// </summary>
        [DataMember(Code = 3, IsOptional = false)]
        public string ServerId { get; set; }

        /// <summary>
        ///   Gets or sets the TCP port of the game server instance.
        /// </summary>
        /// <value>The TCP port.</value>
        [DataMember(Code = 2, IsOptional = true)]
        public int? TcpPort { get; set; }

        /// <summary>
        ///   Gets or sets the UDP port of the game server instance.
        /// </summary>
        /// <value>The UDP port.</value>
        [DataMember(Code = 1, IsOptional = true)]
        public int? UdpPort { get; set; }

        /// <summary>
        ///   Gets or sets the port of the game server instance used for WebSocket connections.
        /// </summary>
        [DataMember(Code = 6, IsOptional = true)]
        public int? WebSocketPort { get; set; }

        /// <summary>
        ///   Gets or sets the initial server state of the game server instance.
        /// </summary>
        [DataMember(Code = 7, IsOptional = true)]
        public int ServerState { get; set; }

        /// <summary>
        ///   Gets or sets the port of the game server instance used for secure WebSocket connections.
        /// </summary>
        [DataMember(Code = 9, IsOptional = true)]
        public int? SecureWebSocketPort { get; set; }

        /// <summary>
        ///   Gets or sets the public game server ip address.
        /// </summary>
        [DataMember(Code = 12, IsOptional = true)]
        public string GameServerAddressIPv6 { get; set; }


        /// <summary>
        ///   Gets or sets the fully qualified public host name of the game server instance (used for WebSocket connections).
        /// </summary>
        [DataMember(Code = 13, IsOptional = true)]
        public string GameServerHostName { get; set; }

        /// <summary>
        ///   Gets prediction data which were collected on GS
        /// </summary>
        [DataMember(Code = 14, IsOptional = true)]
        public Dictionary<byte, int[]> PredictionData { get; set; }

        /// <summary>
        ///   Gets how many load levels used by GS
        /// </summary>
        [DataMember(Code = 15, IsOptional = true)]
        public byte LoadLevelCount { get; set; }

        /// <summary>
        ///   Defines priority server belongs to
        /// </summary>
        [DataMember(Code = 16, IsOptional = true)]
        public byte LoadBalancerPriority { get; set; }

        [DataMember(Code = 17, IsOptional = true)]
        public int? WebRTCPort { get; set; }

        [DataMember(Code = 18, IsOptional = true)]
        public byte LoadIndex { get; set; }

        [DataMember(Code = 19, IsOptional = true)]
        public byte[] SupportedProtocols { get; set; }


        [DataMember(Code = 20, IsOptional = true)]
        public string GamingWsPath { get; set; }
        #endregion

        public static string GetKey(RegisterGameServerDataContract registerRequest)
        {
            return $"{registerRequest.GameServerAddress}-{registerRequest.UdpPort}-{registerRequest.TcpPort}";
        }
    }

}