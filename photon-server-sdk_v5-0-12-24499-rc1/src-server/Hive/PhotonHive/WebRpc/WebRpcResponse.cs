// --------------------------------------------------------------------------------------------------------------------
// <copyright file="RpcResponse.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the RpcResponse type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Photon.Hive.WebRpc
{
    public class WebRpcResponse
    {
        public byte ResultCode { get; set; }

        public string Message { get; set; }

        public object Data { get; set; }

        public override string ToString()
        {
            return string.Format("[ResultCode:{0};Message:'{1}']", this.ResultCode, this.Message);
        }
    }
}
