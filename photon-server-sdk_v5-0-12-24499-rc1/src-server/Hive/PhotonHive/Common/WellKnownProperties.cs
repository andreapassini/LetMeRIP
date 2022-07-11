using Photon.Hive.Operations;
using Photon.SocketServer.Rpc.Protocols;
using System;
using System.Collections;
using System.Linq;

namespace Photon.Hive.Common
{
    public class WellKnownProperties
    {
        public bool? IsOpen { get; set; }
        public bool? IsVisible { get; set; }
        public byte? MaxPlayer { get; set; }
        public int? MasterClientId { get; set; }
        public int? PlayerTTL { get; set; }
        public int? EmptyRoomTTL { get; set; }
        public string[] ExpectedUsers { get; set; }
        public object[] LobbyProperties { get; set; }

        public bool TryGetProperties(Hashtable propertyTable, out string errorMsg, ParameterMetaData paramMetaData)
        {
            if (propertyTable == null)
            {
                errorMsg = "Property table is null";
                return false;
            }

            if (!CheckForWrongProperties(propertyTable, out errorMsg))
            {
                return false;
            }

            if (!TryGetProperties(propertyTable, out var maxPlayer, out var isOpen, out var isVisible, out errorMsg, paramMetaData))
            {
                return false;
            }

            if (!GameParameterReader.TryReadIntParameter(propertyTable, GameParameter.MasterClientId, out var masterClientId, out var value, paramMetaData))
            {
                errorMsg = GetInvalidGamePropertyTypeMessage(GameParameter.MasterClientId, typeof (int), value);
                return false;
            }

            if (!GameParameterReader.TryReadIntParameter(propertyTable, GameParameter.PlayerTTL, out var playerTTL, out value, paramMetaData))
            {
                errorMsg = GetInvalidGamePropertyTypeMessage(GameParameter.PlayerTTL, typeof(int), value);
                return false;
            }

            if (!GameParameterReader.TryReadIntParameter(propertyTable, GameParameter.EmptyRoomTTL, out var emptyRoomTTL, out value, paramMetaData))
            {
                errorMsg = GetInvalidGamePropertyTypeMessage(GameParameter.EmptyRoomTTL, typeof(int), value);
                return false;
            }

            string[] expectedUsers = null;
            if (GameParameterReader.TryReadGameParameter(propertyTable, GameParameter.ExpectedUsers, out value, paramMetaData))
            {
                if (value != null)
                {
                    if (value is string[] == false)
                    {
                        errorMsg = GetInvalidGamePropertyTypeMessage(GameParameter.ExpectedUsers, typeof(string[]), value);
                        return false;
                    }
                    expectedUsers = RemoveNullsAndDuplicates((string[])value);
                    propertyTable[(byte) GameParameter.ExpectedUsers] = expectedUsers;
                }
            }

            object properties = null;
            if (GameParameterReader.TryReadGameParameter(propertyTable, GameParameter.LobbyProperties, out value, paramMetaData))
            {
                if (value != null && value is object[] == false)
                {
                    errorMsg = GetInvalidGamePropertyTypeMessage(GameParameter.LobbyProperties, typeof(object[]), value);
                    return false;
                }

                if (value is string[] strings)
                {
                    properties = RemoveNullsAndDuplicates(strings);
                }
                else
                {
                    properties = RemoveNullsAndDuplicates((object[])value);
                }
                propertyTable[(byte)GameParameter.LobbyProperties] = properties;
            }

            this.IsOpen = isOpen;
            this.IsVisible = isVisible;
            this.MaxPlayer = maxPlayer;
            this.LobbyProperties = (object[])properties;
            this.ExpectedUsers = expectedUsers;
            this.PlayerTTL = playerTTL;
            this.EmptyRoomTTL = emptyRoomTTL;
            this.MasterClientId = masterClientId;
            return true;
        }

        public static bool TryGetProperties(Hashtable propertyTable, out byte? maxPlayer, out bool? isOpen, out bool? isVisible, out string debugMessage, ParameterMetaData paramMetaData)
        {
            isVisible = null;
            isOpen = null;
            debugMessage = null;
            if (GameParameterReader.TryReadByteParameter(propertyTable, GameParameter.MaxPlayers, out maxPlayer, out var value, paramMetaData) == false)
            {
                debugMessage = GetInvalidGamePropertyTypeMessage(GameParameter.MaxPlayers, typeof(byte), value);
                return false;
            }

            if (GameParameterReader.TryReadBooleanParameter(propertyTable, GameParameter.IsOpen, out isOpen, out value, paramMetaData) == false)
            {
                debugMessage = GetInvalidGamePropertyTypeMessage(GameParameter.IsOpen, typeof(bool), value);
                return false;
            }

            if (GameParameterReader.TryReadBooleanParameter(propertyTable, GameParameter.IsVisible, out isVisible, out value, paramMetaData) == false)
            {
                debugMessage = GetInvalidGamePropertyTypeMessage(GameParameter.IsVisible, typeof(bool), value);
                return false;
            }

            return true;
        }


        #region Helpers
        private static bool CheckForWrongProperties(Hashtable propertyTable, out string errorMsg)
        {
            if (propertyTable.ContainsKey((byte)GameParameter.Removed)
                || propertyTable.ContainsKey((byte)GameParameter.PlayerCount))
            {
                errorMsg = "Properties contain one or many of internal properties with ids [251, 252...]";
                return false;
            }

            errorMsg = string.Empty;
            return true;
        }

        private static string GetInvalidGamePropertyTypeMessage(GameParameter parameter, Type expectedType, object value)
        {
            return
                $"Invalid type for property {parameter}. Expected type {expectedType} but is {(value == null ? "null" : value.GetType().ToString())}";
        }

        private static string[] RemoveNullsAndDuplicates(string[] propertyKeys)
        {
            if (propertyKeys != null && propertyKeys.Length > 0)
            {
                return propertyKeys.Where(o => o != null).Distinct().ToArray();
            }

            return propertyKeys;
        }

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
