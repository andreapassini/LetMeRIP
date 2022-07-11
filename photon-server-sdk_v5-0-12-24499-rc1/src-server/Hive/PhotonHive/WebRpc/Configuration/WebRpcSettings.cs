// --------------------------------------------------------------------------------------------------------------------
// <copyright file="RpcSettings.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Collections.Generic;

using Photon.SocketServer;
using Photon.SocketServer.Annotations;

namespace Photon.Hive.WebRpc.Configuration
{
    /// <summary>
    /// Section may look like this:
    /// \verbatim
    /// <WebRpcSettings Enabled = "True" ReconnectInterval="100" BaseUrl="">
    ///     <ExtraParams >
    ///       <Param1>value1</Param1>
    ///       <Param2>value2</Param2>
    ///       <Param3>value3</Param3>
    ///     </ExtraParams>
    /// </WebRpcSettings>
    /// \endverbatim


    /// </summary>
    [SettingsMarker("Photon:WebRpc")]
    public class WebRpcSettings 
    {
        // Explicit static constructor to tell C# compiler
        // not to mark type as beforefieldinit
        static WebRpcSettings()
        {}

        /// <summary>
        /// specialization to set custom default values
        /// </summary>
        public class HttpQueueSettingsClass : Photon.Common.Configuration.HttpQueueSettings
        {
            public HttpQueueSettingsClass()
            {
                this.LimitHttpResponseMaxSize = 100_000;
                this.HttpRequestTimeout = 30000;
                this.MaxBackoffTime = 10000;
                this.MaxConcurrentRequests = 50;
                this.MaxErrorRequests = 30;
                this.MaxQueuedRequests = 5000;
                this.MaxTimedOutRequests = 30;
                this.QueueTimeout = 50000;
                this.ReconnectInterval = 10000;
            }
        }

        #region Properties

        public static WebRpcSettings Default { get; } = GetSettings();

        public bool Enabled { get; set; }

        public Dictionary<string, string> ExtraParams { get; } = new Dictionary<string, string>();

        public string BaseUrl { get; set; } = "";

        public int HttpCallsLimit { get; set; } = int.MaxValue;

        public HttpQueueSettingsClass HttpQueueSettings { get; set; } = new HttpQueueSettingsClass();

        #endregion

        #region .privates

        private static WebRpcSettings GetSettings()
        {
            const string sectionName = "Photon:WebRpc";

            var result = ApplicationBase.GetConfigSectionAndValidate<WebRpcSettings>(sectionName);

            return result;
        }
        #endregion
    }
}