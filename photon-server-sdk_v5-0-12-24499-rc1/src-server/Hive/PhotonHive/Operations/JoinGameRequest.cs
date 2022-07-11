// --------------------------------------------------------------------------------------------------------------------
// <copyright file="JoinRequest.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   This class implements the Join operation.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using ExitGames.Logging;

using Photon.Common;
using Photon.Hive.Common;
using Photon.Hive.Events;
using Photon.Hive.Plugin;
using Photon.SocketServer;
using Photon.SocketServer.Diagnostics;
using Photon.SocketServer.Rpc;
using Photon.SocketServer.Rpc.Protocols;

namespace Photon.Hive.Operations
{
    public class JoinModes : JoinModeConstants
    {
    }


    /// <summary>
    /// This class implements the Join operation.
    /// </summary>
    public class JoinGameRequest : Operation, IJoinGameRequest
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private static readonly LogCountGuard metaDataViolationsLogGuard = new LogCountGuard(new TimeSpan(0, 0, 0, 10));
        /// <summary>
        /// Initializes a new instance of the <see cref="JoinGameRequest"/> class.
        /// </summary>
        /// <param name="protocol">
        ///     The protocol.
        /// </param>
        /// <param name="operationRequest">
        ///     Operation request containing the operation parameters.
        /// </param>
        /// <param name="userId"></param>
        /// <param name="maxPropertiesSize"></param>
        /// <param name="onlyLogMetaDataViolations"></param>
        public JoinGameRequest(IRpcProtocol protocol, OperationRequest operationRequest, string userId,
            int maxPropertiesSize, bool onlyLogMetaDataViolations = false)
            : base(protocol, operationRequest)
        {
            this.ValidateAndUpdateData(maxPropertiesSize, onlyLogMetaDataViolations);
            if (!this.IsValid)
            {
                return;
            }

            // special treatment for game and actor properties sent by AS3/Flash or JSON clients
            var protocolId = protocol.ProtocolType;
            if (protocolId == ProtocolType.Json)
            {
                Utilities.ConvertAs3WellKnownPropertyKeys(this.GameProperties, this.ActorProperties, 
                    operationRequest.RequestMetaData?[(byte)ParameterKey.GameProperties], operationRequest.RequestMetaData?[(byte)ParameterKey.ActorProperties]);
            }

            // we have it here to give priority to explicit value. we are not sure what value will be last after automatic parsing
            // see DeleteCacheOnLeave for instance
            if (this.OperationRequest.Parameters.ContainsKey((byte)ParameterKey.RoomOptionFlags))
            {
                if (this.OperationRequest[(byte) ParameterKey.RoomOptionFlags] is int x)
                {
                    this.RoomFlags = x;
                }
            }

            this.SetupRequest(userId);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JoinGameRequest"/> class.
        /// </summary>
        public JoinGameRequest()
        {
        }

        /// <summary>
        /// Gets or sets custom actor properties.
        /// </summary>
        [DataMember(Code = (byte)ParameterKey.ActorProperties, IsOptional = true)]
        public Hashtable ActorProperties { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the actor properties
        /// should be included in the <see cref="JoinEvent"/> event which 
        /// will be sent to all clients currently in the room.
        /// </summary>
        [DataMember(Code = (byte) ParameterKey.Broadcast, IsOptional = true)]
        public bool BroadcastActorProperties { get; set; }

        /// <summary>
        /// Gets or sets the name of the game (room).
        /// </summary>
        [DataMember(Code = (byte)ParameterKey.GameId)]
        public virtual string GameId { get; set; }

        /// <summary>
        /// Gets or sets custom game properties.
        /// </summary>
        /// <remarks>
        /// Game properties will only be applied for the game creator.
        /// </remarks>
        [DataMember(Code = (byte)ParameterKey.GameProperties, IsOptional = true)]
        public Hashtable GameProperties { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether cached events are automaticly deleted for 
        /// actors which are leaving a room.
        /// </summary>
        [DataMember(Code = (byte)ParameterKey.DeleteCacheOnLeave, IsOptional = true)]
        public bool DeleteCacheOnLeave
        {
            get => this.GetRoomFlag(Plugin.RoomOptionFlags.DeleteCacheOnLeave);
            set => this.SetRoomFlag(value, Plugin.RoomOptionFlags.DeleteCacheOnLeave);
        }

        /// <summary>
        /// Gets or sets a value indicating if common room events (Join, Leave) will be suppressed.
        /// </summary>
        /// <remarks>
        /// This property will only be applied for the game creator.
        /// </remarks>
        [DataMember(Code = (byte)ParameterKey.SuppressRoomEvents, IsOptional = true)]
        public bool SuppressRoomEvents
        {
            get => this.GetRoomFlag(Plugin.RoomOptionFlags.SuppressRoomEvents);
            set => this.SetRoomFlag(value, Plugin.RoomOptionFlags.SuppressRoomEvents);
        }

        private int actorNr = 0;
        /// <summary>
        /// Actor number, which will be used for rejoin
        /// </summary>
        [DataMember(Code = (byte)ParameterKey.ActorNr, IsOptional = true)]
        public int ActorNr
        {
            get => this.actorNr;
            set
            {
                this.actorNr = value;
                //if (this.actorNr > 0 && this.CreateIfNotExists)
                if (!this.IsRejoining)
                {
                    this.JoinMode = JoinModeConstants.RejoinOrJoin;
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating how long the room instance will be keeped alive 
        /// in the room cache after all peers have left the room.
        /// </summary>
        /// <remarks>
        /// This property will only be applied for the room creator.
        /// </remarks>
        [DataMember(Code = (byte)ParameterKey.EmptyRoomLiveTime, IsOptional = true)]
        public int EmptyRoomLiveTime { get; set; }

        /// <summary>
        /// The time a player the room waits to allow a player to rejoin after a disconnect.
        /// If player should be allowed to return any time set the value less than 0.
        /// </summary>
        [DataMember(Code = (byte)ParameterKey.PlayerTTL, IsOptional = true)]
        public int PlayerTTL { get; set; }

        /// <summary>
        /// Set true to restrict useres to connect only once.
        /// Default is not to check.
        /// </summary>
        [DataMember(Code = (byte)ParameterKey.CheckUserOnJoin, IsOptional = true)]
        public bool CheckUserOnJoin
        {
            get => this.GetRoomFlag(Plugin.RoomOptionFlags.CheckUserOnJoin);
            set => this.SetRoomFlag(value, Plugin.RoomOptionFlags.CheckUserOnJoin);
        }

        /// <summary>
        /// The lowest slice of cached events the actor expects to recieve.
        /// </summary>
        [DataMember(Code = (byte)ParameterKey.CacheSliceIndex, IsOptional = true)]
        public int? CacheSlice { get; set; }

        [DataMember(Code = (byte)ParameterKey.LobbyName, IsOptional = true)]
        public string LobbyName { get; set; }

        [DataMember(Code = (byte)ParameterKey.LobbyType, IsOptional = true)]
        public byte LobbyType { get; set; }

        [DataMember(Code = (byte)ParameterKey.JoinMode, IsOptional = true)]
        private object InternalJoinMode { get; set; }

        /// <summary>Informs the server of the expected plugin setup.</summary>
        /// <remarks>
        /// The operation will fail in case of a plugin missmatch returning error code PluginMismatch 32757(0x7FFF - 10).
        /// Setting string[]{} means the client expects no plugin to be setup.
        /// Note: for backwards compatibility null omits any check.
        /// </remarks>
        [DataMember(Code = (byte)ParameterKey.Plugins, IsOptional = true)]
        public string[] Plugins { get; set; }

        [DataMember(Code = (byte)ParameterKey.WebFlags, IsOptional = true)]
        public byte WebFlags { get; set; }

        // users to add as expected
        [DataMember(Code = (byte)ParameterKey.AddUsers, IsOptional = true)]
        public string[] AddUsers { get; set; }

        [DataMember(Code = (byte)ParameterKey.PublishUserId, IsOptional = true)]
        public bool PublishUserId
        {
            get => this.GetRoomFlag(RoomOptionFlags.PublishUserId);
            set => this.SetRoomFlag(value, RoomOptionFlags.PublishUserId);
        }

        [DataMember(Code = (byte)ParameterKey.ForceRejoin, IsOptional = true)]
        public bool ForceRejoin { get; set; }

        [DataMember(Code = (byte)ParameterKey.RoomOptionFlags, IsOptional = true)]
        public int RoomFlags { get; set; }

        // no ParameterKey:
        // for backward compatibility this is set through InternalJoinMode
        public byte JoinMode { get; set; }

        // no ParameterKey:
        // for backward compatibility this is set through InternalJoinMode
        public bool CreateIfNotExists => this.JoinMode == JoinModeConstants.CreateIfNotExists;

        public byte OperationCode => this.OperationRequest.OperationCode;

        public Dictionary<byte, object> Parameters => this.OperationRequest.Parameters;

        //cached values of game properties which this request contains
        public Hashtable properties;

        public WellKnownProperties wellKnownPropertiesCache { get; private set; }

        public bool IsRejoining => this.JoinMode == JoinModeConstants.RejoinOnly || this.JoinMode == JoinModeConstants.RejoinOrJoin;

        public ErrorCode FailureReason { get; protected set; }
        public string FailureMessage { get; protected set; }

        public bool DeleteNullProps => this.GetRoomFlag(Plugin.RoomOptionFlags.DeleteNullProps);

        /// <summary>
        /// properties changed event to send after join response.
        /// </summary>
        public PropertiesChangedEvent PropertiesChangedEvent;

        #region .publics

        public Dictionary<string, object> GetCreateGameSettings(HiveGame game)
        {
            var settings = new Dictionary<string, object>();

            // set default properties
            if (wellKnownPropertiesCache.MaxPlayer.HasValue && wellKnownPropertiesCache.MaxPlayer.Value != game.MaxPlayers)
            {
                settings[HiveHostGameState.MaxPlayers.ToString()] = wellKnownPropertiesCache.MaxPlayer.Value;
            }

            if (wellKnownPropertiesCache.IsOpen.HasValue && wellKnownPropertiesCache.IsOpen.Value != game.IsOpen)
            {
                settings[HiveHostGameState.IsOpen.ToString()] = wellKnownPropertiesCache.IsOpen.Value;
            }

            if (wellKnownPropertiesCache.IsVisible.HasValue && wellKnownPropertiesCache.IsVisible.Value != game.IsVisible)
            {
                settings[HiveHostGameState.IsVisible.ToString()] = wellKnownPropertiesCache.IsVisible.Value;
            }

            settings[HiveHostGameState.LobbyId.ToString()] = this.LobbyName;
            settings[HiveHostGameState.LobbyType.ToString()] = this.LobbyType;

            if (wellKnownPropertiesCache.LobbyProperties != null)
            {
                settings[HiveHostGameState.CustomProperties.ToString()] =
                    GetLobbyGameProperties(this.GameProperties, new HashSet<object>(wellKnownPropertiesCache.LobbyProperties));
            }

            settings[HiveHostGameState.EmptyRoomTTL.ToString()] = this.EmptyRoomLiveTime;
            settings[HiveHostGameState.PlayerTTL.ToString()] = this.PlayerTTL;
            settings[HiveHostGameState.CheckUserOnJoin.ToString()] = this.CheckUserOnJoin;
            settings[HiveHostGameState.DeleteCacheOnLeave.ToString()] = this.DeleteCacheOnLeave;
            settings[HiveHostGameState.SuppressRoomEvents.ToString()] = this.SuppressRoomEvents;
            settings[HiveHostGameState.PublishUserId.ToString()] = this.PublishUserId;
            settings[HiveHostGameState.ExpectedUsers.ToString()] = this.AddUsers;

            return settings;
        }

        public string GetPluginName()
        {
            return this.Plugins != null && this.Plugins.Length > 0 ? this.Plugins[0] : string.Empty;
        }

        public string GetNickname()
        {
            if (this.ActorProperties != null)
            {
                return (this.ActorProperties[(byte)255] as string) ?? string.Empty;
            }

            return string.Empty;
        }

        public void OnJoinFailed(ErrorCode reason, string msg)
        {
            this.FailureReason = reason;
            this.FailureMessage = msg;
        }
        public void SetupRequest(string requestOwnerId)
        {
            this.properties = this.GameProperties ?? new Hashtable();
            this.wellKnownPropertiesCache = new WellKnownProperties();

            if (this.properties != null && this.properties.Count > 0)
            {
                this.isValid = this.wellKnownPropertiesCache.TryGetProperties(this.properties, out this.errorMessage, this.RequestMetaData?[(byte)ParameterKey.GameProperties]);
                if (!this.IsValid)
                {
                    return;
                }

                if (this.properties.ContainsKey(GameParameters.MasterClientId))
                {
                    this.isValid = false;
                    this.errorMessage = HiveErrorMessages.MasterClientIdIsNotAllowedThroughCreationOrJoin;
                    return;
                }
            }

            if (this.AddUsers == null)
            {
                this.AddUsers = this.wellKnownPropertiesCache.ExpectedUsers;
            }

            if (this.AddUsers != null)
            {
                this.MakeAddUsersUniq();

                var usersCount = this.AddUsers.Length;
                if (!this.AddUsers.Contains(requestOwnerId))
                {
                    usersCount++;
                }

                if (this.wellKnownPropertiesCache.MaxPlayer.HasValue
                    && this.wellKnownPropertiesCache.MaxPlayer != 0
                    && usersCount > this.wellKnownPropertiesCache.MaxPlayer)
                {
                    this.isValid = false;
                    this.errorMessage = "Reserved slots count is bigger then max player value";
                    return;
                }

                if (this.AddUsers.Any(string.IsNullOrEmpty))
                {
                    this.isValid = false;
                    this.errorMessage = HiveErrorMessages.SlotCanNotHaveEmptyName;
                    return;
                }
            }

            if (this.EmptyRoomLiveTime == 0 && this.wellKnownPropertiesCache.EmptyRoomTTL.HasValue)
            {
                this.EmptyRoomLiveTime = this.wellKnownPropertiesCache.EmptyRoomTTL.Value;
            }

            if (this.PlayerTTL == 0 && this.wellKnownPropertiesCache.PlayerTTL.HasValue)
            {
                this.PlayerTTL = this.wellKnownPropertiesCache.PlayerTTL.Value;
            }
        }

        public int GetAddUsersSize()
        {
            var requestMetaData = this.RequestMetaData;
            if (requestMetaData == null)
            {
                return 0;
            }

            var paramMetaData = requestMetaData[(byte)ParameterKey.AddUsers];
            if (paramMetaData == null)
            {
                return 0;
            }

            return paramMetaData.DataSize;
        }

        #endregion

        #region Privates

        private bool GetRoomFlag(int bitFlag)
        {
            return (this.RoomFlags & bitFlag) != 0;
        }

        private void SetRoomFlag(bool flagValue, int bitFlag)
        {
            this.RoomFlags = flagValue ? this.RoomFlags | bitFlag : this.RoomFlags & ~bitFlag;
        }

        private static Hashtable GetLobbyGameProperties(Hashtable source, HashSet<object> list)
        {
            if (source == null || source.Count == 0)
            {
                return null;
            }

            Hashtable gameProperties;

            if (list != null)
            {
                // filter for game properties is set, only properties in the specified list 
                // will be reported to the lobby 
                gameProperties = new Hashtable(list.Count);

                foreach (object entry in list)
                {
                    if (source.ContainsKey(entry))
                    {
                        gameProperties.Add(entry, source[entry]);
                    }
                }
            }
            else
            {
                // if no filter is set for properties which should be listed in the lobby
                // all properties are send
                gameProperties = source;
                gameProperties.Remove((byte)GameParameter.MaxPlayers);
                gameProperties.Remove((byte)GameParameter.IsOpen);
                gameProperties.Remove((byte)GameParameter.IsVisible);
                gameProperties.Remove((byte)GameParameter.LobbyProperties);
            }

            return gameProperties;
        }

        private void MakeAddUsersUniq()
        {
            this.AddUsers = this.AddUsers.Distinct().ToArray();
        }

        private void ValidateAndUpdateData(int maxPropertiesSize, bool onlyLogMetaDataViolations)
        {
            if (!this.IsValid)
            {
                return;
            }

            if (!this.ValidatePropertiesSize(maxPropertiesSize, onlyLogMetaDataViolations))
            {
                return;
            }

            if (!this.ValidateAndUpdateJoinMode())
            {
                return;
            }
        }

        private bool ValidatePropertiesSize(int maxPropertiesSize, bool onlyLogMetaDataViolations)
        {
            var requestMetaData = this.RequestMetaData;
            if (requestMetaData == null)
            {
                return true;
            }

            var gamePropsMeta = requestMetaData[(byte) ParameterKey.GameProperties];
            var actorPropsMeta = requestMetaData[(byte) ParameterKey.ActorProperties];

            var totalPropsSize = gamePropsMeta != null ? gamePropsMeta.DataSize : 0;
            totalPropsSize += actorPropsMeta != null ? actorPropsMeta.DataSize : 0;

            if (totalPropsSize > maxPropertiesSize)
            {
                if (onlyLogMetaDataViolations)
                {
                    log.Warn(metaDataViolationsLogGuard, $"Limit exceeded Properties size in JoinGameRequest is too big. limit:{maxPropertiesSize} size_{totalPropsSize}");
                    return true;
                }
                this.isValid = false;
                this.errorMessage = "Properties size is too big";
            }
            return this.IsValid;
        }

        private bool ValidateAndUpdateJoinMode()
        {
            var value = this.InternalJoinMode;
            if (value == null)
            {
                return true;
            }
                    
            if (value is bool bvalue && this.JoinMode == JoinModeConstants.JoinOnly && bvalue)
            {
                this.JoinMode = JoinModeConstants.CreateIfNotExists;
                return true;
            }

            try
            {
                this.JoinMode = Convert.ToByte(value);
            }
            catch (Exception e)
            {
                this.isValid = false;
                this.errorMessage = e.Message;
                return false;
            }
            return true;
        }

        #endregion
    }

}