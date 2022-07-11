// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Utilities.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the Utilities type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Photon.Hive.Common
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;

    using Photon.Hive.Operations;
    using Photon.SocketServer.Rpc.Protocols;

    /// <summary>
    /// A collection of methods useful in one or another context.
    /// </summary>
    public static class Utilities
    {
        private static readonly string amf3IsVisblePropertyKey = ((byte)GameParameter.IsVisible).ToString(CultureInfo.InvariantCulture);

        private static readonly string amf3IsOpenPropertyKey = ((byte)GameParameter.IsOpen).ToString(CultureInfo.InvariantCulture);

        private static readonly string amf3MaxPlayerPropertyKey = ((byte)GameParameter.MaxPlayers).ToString(CultureInfo.InvariantCulture);

        private static readonly string amf3PropertiesPropertyKey = ((byte)GameParameter.LobbyProperties).ToString(CultureInfo.InvariantCulture);

        private static readonly string amf3MasterClientIdPropertyKey = ((byte)GameParameter.MasterClientId).ToString(CultureInfo.InvariantCulture);

        private static readonly string amf3ExpectedUsersPropertyKey = ((byte)GameParameter.ExpectedUsers).ToString(CultureInfo.InvariantCulture);

        private static readonly string amf3PlayerTTLPropertyKey = ((byte)GameParameter.PlayerTTL).ToString(CultureInfo.InvariantCulture);

        private static readonly string amf3EmptyRoomTTLPropertyKey = ((byte)GameParameter.EmptyRoomTTL).ToString(CultureInfo.InvariantCulture);

        private static readonly string amf3NicknamePropertyKey = ((byte)ActorParameter.Nickname).ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// Converts well known properties sent by AS3/Flash clients - from string to byte-keys.
        /// </summary>
        /// <remarks>
        /// Check if peer is a flash (amf3) client because flash clients does not support byte keys in a hastable. 
        /// If a flash client likes to match a game with a specific 'MaxPlayer' value 'MaxPlayer' will be sent
        /// with the string key "255" and the max player value as int.
        /// </remarks>
        /// <param name="gameProps">A game properties hashtable.</param>
        /// <param name="actorProps">A actor properties hashtable.</param>
        public static void ConvertAs3WellKnownPropertyKeys(Hashtable gameProps, Hashtable actorProps, ParameterMetaData gamePropsMeta, ParameterMetaData actorPropsMeta)
        {
            // convert game properties
            if (gameProps != null && gameProps.Count > 0)
            {
                // well known property "is visible"
                UpdatePropertyKeyType(gameProps, gamePropsMeta, (byte)GameParameter.IsVisible, amf3IsVisblePropertyKey);

                // well known property "is open"
                UpdatePropertyKeyType(gameProps, gamePropsMeta, (byte)GameParameter.IsOpen, amf3IsOpenPropertyKey);

                // well known property "max players"
                UpdatePropertyKeyType(gameProps, gamePropsMeta, (byte)GameParameter.MaxPlayers, amf3MaxPlayerPropertyKey);

                // well known property "props listed in lobby"
                UpdatePropertyKeyType(gameProps, gamePropsMeta, (byte)GameParameter.LobbyProperties, amf3PropertiesPropertyKey);

                // well known property "master client id"
                UpdatePropertyKeyType(gameProps, gamePropsMeta, (byte)GameParameter.MasterClientId, amf3MasterClientIdPropertyKey);

                // well known property "expected users"
                UpdatePropertyKeyType(gameProps, gamePropsMeta, (byte)GameParameter.ExpectedUsers, amf3ExpectedUsersPropertyKey);

                // well known property "player ttl"
                UpdatePropertyKeyType(gameProps, gamePropsMeta, (byte)GameParameter.PlayerTTL, amf3PlayerTTLPropertyKey);

                // well known property "empty room ttl"
                UpdatePropertyKeyType(gameProps, gamePropsMeta, (byte) GameParameter.EmptyRoomTTL, amf3EmptyRoomTTLPropertyKey);
            }

            // convert actor properties (if any)
            if (actorProps != null && actorProps.Count > 0)
            {
                // well known property "PlayerName"
                UpdatePropertyKeyType(actorProps, actorPropsMeta, (byte)ActorParameter.Nickname, amf3NicknamePropertyKey);

                // well known property "IsInactive" and "UserId"
                // can't be set by the client
                // will be removed in SetPropertiesHandler and JoinApplyGameStateChanges
            }
        }

        /// <summary>
        /// Converts well known properties sent by AS3/Flash clients - from string to byte-keys.
        /// </summary>
        /// <param name="gamePropertyKeys">The game properties list.</param>
        /// <param name="actorPropertyKeys">The actor properties list.</param>
        public static void ConvertAs3WellKnownPropertyKeys(IList gamePropertyKeys, IList actorPropertyKeys)
        {
            // convert game properties
            if (gamePropertyKeys != null && gamePropertyKeys.Count > 0)
            {
                // well known property "is visible"
                UpdateKeyTypeInList(gamePropertyKeys, (byte)GameParameter.IsVisible, amf3IsVisblePropertyKey);

                // well known property "is open"
                UpdateKeyTypeInList(gamePropertyKeys, (byte)GameParameter.IsOpen, amf3IsOpenPropertyKey);

                // well known property "max players"
                UpdateKeyTypeInList(gamePropertyKeys, (byte)GameParameter.MaxPlayers, amf3MaxPlayerPropertyKey);

                // well known property "props listed in lobby"
                UpdateKeyTypeInList(gamePropertyKeys, (byte)GameParameter.LobbyProperties, amf3PropertiesPropertyKey);

                // well known property "master client id"
                UpdateKeyTypeInList(gamePropertyKeys, (byte)GameParameter.MasterClientId, amf3MasterClientIdPropertyKey);

                // well known property "expected users"
                UpdateKeyTypeInList(gamePropertyKeys, (byte)GameParameter.ExpectedUsers, amf3ExpectedUsersPropertyKey);

                // well known property "player ttl"
                UpdateKeyTypeInList(gamePropertyKeys, (byte)GameParameter.PlayerTTL, amf3PlayerTTLPropertyKey);

                // well known property "empty room ttl"
                UpdateKeyTypeInList(gamePropertyKeys, (byte)GameParameter.EmptyRoomTTL, amf3EmptyRoomTTLPropertyKey);
            }

            // convert actor properties (if any)
            if (actorPropertyKeys != null && actorPropertyKeys.Count > 0)
            {
                // well known property "PlayerName"
                UpdateKeyTypeInList(actorPropertyKeys, (byte)ActorParameter.Nickname, amf3NicknamePropertyKey);

                // well known property "IsInactive"
                // can't be set by the client
                // will be removed in SetPropertiesHandler and JoinApplyGameStateChanges
            }
        }

        private static void UpdateKeyTypeInList(IList propertyKeys, byte byteKey, string strKey)
        {
            var idx = propertyKeys.IndexOf(strKey);
            if (idx != -1)
            {
                propertyKeys.RemoveAt(idx);
                propertyKeys.Add(byteKey);
            }
        }

        private static void UpdatePropertyKeyType(Hashtable gameProps, ParameterMetaData propsMeta, byte byteKey, string strKey)
        {
            if (gameProps.ContainsKey(strKey))
            {
                gameProps[byteKey] = gameProps[strKey];
                gameProps.Remove(strKey);
                UpdatePropertiesKeyMetaData(propsMeta, byteKey, strKey);
            }
        }

        private static void UpdatePropertiesKeyMetaData(ParameterMetaData propsMeta, byte byteKey, string strKey)
        {
            var subMetaData = propsMeta?.SubtypeMetaData;
            if (subMetaData != null)
            {
                var v = subMetaData[strKey];
                subMetaData[byteKey] = v;
                subMetaData.Remove(strKey);
                propsMeta.DataSize -= v.Key - 1;
            }
        }

    }
}
