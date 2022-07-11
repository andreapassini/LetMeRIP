// --------------------------------------------------------------------------------------------------------------------
// <copyright file="GameParameterReader.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Collections;
using Photon.Hive.Operations;
using Photon.SocketServer.Rpc.Protocols;

namespace Photon.Hive.Common
{
    /// <summary>
    /// Provides methods to read build in game properties from a hashtable.
    /// </summary>
    /// <remarks>
    /// Build in game properties in the load balancing project are stored as byte values. 
    /// Because some protocols used by photon (Flash, WebSockets) does not support byte values
    /// the properties will also be searched in the hashtable using there int representation.
    /// If an int representation is found it will be converted to the byte representation of 
    /// the game property.
    /// </remarks>
    public static class GameParameterReader
    {
        #region Public Methods

        public static bool TryReadBooleanParameter(Hashtable hashtable, GameParameter parameter, out bool? result, out object value, ParameterMetaData paramMetaData)
        {
            result = null;

            if (!TryReadGameParameter(hashtable, parameter, out value, paramMetaData))
            {
                return true;
            }

            if (value is bool boolValue)
            {
                result = boolValue;
                return true;
            }

            return false;
        }

        public static bool TryReadByteParameter(Hashtable hashtable, GameParameter parameter, out byte? result, out object value, ParameterMetaData paramMetaData)
        {
            result = null;

            if (!TryReadGameParameter(hashtable, parameter, out value, paramMetaData))
            {
                return true;
            }

            if (value is byte byteVal)
            {
                result = byteVal;
                return true;
            }

            if (value is int intVal)
            {
                result = (byte)intVal;
                hashtable[(byte)parameter] = result;
                return true;
            }

            if (value is double doubleVal)
            {
                result = (byte)doubleVal;
                hashtable[(byte)parameter] = result;
                return true;
            }

            return false;
        }

        public static bool TryReadIntParameter(Hashtable hashtable, GameParameter parameter, out int? result, out object value, ParameterMetaData paramMetaData)
        {
            result = null;

            if (!TryReadGameParameter(hashtable, parameter, out value, paramMetaData))
            {
                return true;
            }

            if (value is byte byteVal)
            {
                result = byteVal;
                hashtable[(byte)parameter] = result;
                return true;
            }

            if (value is int intVal)
            {
                result = intVal;
                return true;
            }

            if (value is double doubleVal)
            {
                result = (int)doubleVal;
                hashtable[(byte)parameter] = result;
                return true;
            }

            return false;
        }

        public static bool TryReadGameParameter(Hashtable hashtable, GameParameter parameter, out object result, ParameterMetaData paramMetaData)
        {
            var byteKey = (byte)parameter;
            if (hashtable.ContainsKey(byteKey))
            {
                result = hashtable[byteKey];
                return true;
            }

            var intKey = (int)parameter;
            if (hashtable.ContainsKey(intKey))
            {
                result = hashtable[intKey];
                hashtable.Remove(intKey);
                hashtable[byteKey] = result;
                UpdateParamMetaData(paramMetaData, byteKey, intKey);
                return true;
            }

            result = null;
            return false;
        }

        private static void UpdateParamMetaData(ParameterMetaData paramMetaData, byte byteKey, int intKey)
        {
            var subMetaData = paramMetaData?.SubtypeMetaData;
            if (subMetaData == null)
            {
                return;
            }

            var v = subMetaData[intKey];
            subMetaData[byteKey] = v;
            subMetaData.Remove(intKey);
            paramMetaData.DataSize -= v.Key - 1;
        }

        #endregion

    }
}