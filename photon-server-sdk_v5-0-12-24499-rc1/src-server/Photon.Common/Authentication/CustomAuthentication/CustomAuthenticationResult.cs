// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CustomAuthenticationResult.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the CustomAuthenticationResult type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Photon.Common.Authentication.CustomAuthentication
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    public class CustomAuthenticationResult
    { 
        [DataMember(IsRequired = true)]
        [JsonConverter(typeof(CustomByteConverter))]
        public byte ResultCode { get; set; }

        [DataMember(IsRequired = false)]
        public string Message { get; set; }

        [DataMember(IsRequired = false)]
        public string UserId { get; set; }

        [DataMember(IsRequired = false)]
        public string Nickname { get; set; }

        [DataMember(IsRequired = false)]
        public Dictionary<string, object> Data { get; set; }

        [Obsolete("Replaced by AuthCookie - only kept for backwards compatibility")]
        [DataMember(IsRequired = false)]
        public Dictionary<string, object> Secure { get; set; }

        [DataMember(IsRequired = false)]
        public Dictionary<string, object> AuthCookie { get; set; }

        [DataMember(IsRequired = false)]
        public long? ExpireAt { get; set; }
    }

    public class CustomByteConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return (objectType == typeof(int));
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JValue jsonValue = serializer.Deserialize<JValue>(reader);

            if (jsonValue.Type == JTokenType.Float)
            {
                return (byte)Math.Round(jsonValue.Value<double>());
            }
            else if (jsonValue.Type == JTokenType.Integer)
            {
                return jsonValue.Value<byte>();
            }

            throw new FormatException();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }
    }
}
