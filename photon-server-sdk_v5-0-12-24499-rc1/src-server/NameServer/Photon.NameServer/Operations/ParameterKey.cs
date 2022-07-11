// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ParameterKey.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the ParameterKey type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Photon.NameServer.Operations
{
    public enum ParameterKey : byte
    {
        /// <summary>
        ///   The game id.
        /// </summary>
        GameId = 255,

        /// <summary>
        ///   The actor nr
        ///   used as op-key and ev-key
        /// </summary>
        ActorNr = 254,

        /// <summary>
        ///   The target actor nr.
        /// </summary>
        TargetActorNr = 253,

        /// <summary>
        ///   The actors.
        /// </summary>
        Actors = 252,

        /// <summary>
        ///   The properties.
        /// </summary>
        Properties = 251,

        /// <summary>
        ///   The broadcast.
        /// </summary>
        Broadcast = 250,

        /// <summary>
        ///   The actor properties.
        /// </summary>
        ActorProperties = 249,

        /// <summary>
        ///   The game properties.
        /// </summary>
        GameProperties = 248,

        /// <summary>
        ///   Event parameter to indicate whether events are cached for new actors.
        /// </summary>
        Cache = 247,

        /// <summary>
        ///   Event parameter containing a <see cref="Photon.Hive.Operations.ReceiverGroup"/> value.
        /// </summary>
        ReceiverGroup = 246,

        /// <summary>
        ///   The data.
        /// </summary>
        Data = 245,

        /// <summary>
        ///   The paramter code for the <see cref="RaiseEventRequest">raise event</see> operations event code.
        /// </summary>
        Code = 244,

        /// <summary>
        ///   the flush event code for raise event.
        /// </summary>
        Flush = 243,

        /// <summary>
        /// Event parameter to indicate whether cached events are deleted automaticly for actors leaving a room.
        /// </summary>
        DeleteCacheOnLeave = 241,

        /// <summary>
        /// The group this event should be sent to. No error is happening if the group is empty or not existing.
        /// </summary>
        Group = 240,

        /// <summary>
        /// Groups to leave. Null won't remove any groups. byte[0] will remove ALL groups. Otherwise only the groups listed will be removed.
        /// </summary>
        GroupsForRemove = 239,

        /// <summary>
        /// Groups to enter. Null won't add groups. byte[0] will add ALL groups. Otherwise only the groups listed will be added.
        /// </summary>
        GroupsForAdd = 238,

        /// <summary>
        /// A parameter indicating if common room events (Join, Leave) will be suppressed.
        /// </summary>
        SuppressRoomEvents = 237,

        /// <summary>
        /// A parameter indicating how long a room instance should be kept alive in the 
        /// room cache after all players left the room.
        /// </summary>
        EmptyRoomLiveTime = 236,

        /// <summary>
        /// A parameter indicating how long a player is allowd to rejoin.
        /// </summary>
        PlayerTTL = 235,

        /// <summary>
        /// A parameter indicating that content of room event should be forwarded to some server
        /// </summary>
        HttpForward = 234,
        WebFlags = 234,


        // load balancing project specific parameters
        Address = 230,
        PeerCount = 229,
        GameCount = 228,
        MasterPeerCount = 227,
        UserId = 225,
        ApplicationId = 224,
        Position = 223,
        GameList = 222,
        Token = 221,
        AppVersion = 220,
        NodeId = 219,
        Info = 218,
        ClientAuthenticationType = 217,
        ClientAuthenticationParams = 216,
        CreateIfNotExists = 215,
        ClientAuthenticationData = 214,
        LobbyName = 213,
        LobbyType = 212,
        LobbyStats = 211,
        Region = 210,
        Nickname = 202,
        Flags = 199,
        CloudType = 198,
        GameRemoveReason = 197,
        Cluster = 196,
        ExpectedProtocol = 195,
        CustomInitData = 194,
        EncryptionMode = 193,
        EncryptionData = 192,
        AuthMode = 191,
    }
}
