using System;
using System.Collections.Generic;

using ExitGames.Logging;

using Photon.Common.Configuration;
using Photon.Hive.WebRpc.Configuration;
using Photon.SocketServer.Net;

namespace Photon.Hive.WebRpc
{
    public class WebRpcManager
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private Dictionary<string, object> environment = new Dictionary<string, object>();

        /// <summary>
        /// queue which manages requests
        /// </summary>
        private readonly HttpRequestQueue httpRequestQueue = new HttpRequestQueue();
        /// <summary>
        /// timeout for every individual request
        /// </summary>
        private int httpQueueRequestTimeout;
        /// <summary>
        /// base url for webrpc
        /// </summary>
        private string baseUrl;

        public WebRpcManager(Dictionary<string, object> environment)
        {
            var settings = WebRpcSettings.Default;
            var webRpcEnabled = settings.Enabled;
            var baseUrlString = webRpcEnabled ? settings.BaseUrl : string.Empty;

            this.Init(webRpcEnabled, baseUrlString, environment, settings.HttpQueueSettings);
        }

        public WebRpcManager(bool enabled, string baseUrl, Dictionary<string, object> environment, HttpQueueSettings httpQueueSettings)
        {
            this.Init(enabled, baseUrl, environment, httpQueueSettings);
        }

        public bool IsRpcEnabled { get; private set; }

        public WebRpcHandler GetWebRpcHandler()
        {
            if (this.IsRpcEnabled)
            {
                return new WebRpcHandler(this.baseUrl, this.environment, this.httpRequestQueue, this.httpQueueRequestTimeout);
            }
            return null;
        }

        #region Methods

        private void Init(bool enabled, string baseUrlString, Dictionary<string, object> env, HttpQueueSettings httpQueueSettings)
        {
            this.environment = env;
            this.baseUrl = baseUrlString;

            this.httpRequestQueue.MaxErrorRequests = httpQueueSettings.MaxErrorRequests;
            this.httpRequestQueue.MaxTimedOutRequests = httpQueueSettings.MaxTimedOutRequests;
            this.httpRequestQueue.ReconnectInterval = TimeSpan.FromMilliseconds(httpQueueSettings.ReconnectInterval);
            this.httpRequestQueue.QueueTimeout = TimeSpan.FromMilliseconds(httpQueueSettings.QueueTimeout);
            this.httpRequestQueue.MaxQueuedRequests = httpQueueSettings.MaxQueuedRequests;
            this.httpRequestQueue.MaxBackoffInMilliseconds = httpQueueSettings.MaxBackoffTime;
            this.httpRequestQueue.MaxConcurrentRequests = httpQueueSettings.MaxConcurrentRequests;
            this.httpRequestQueue.ResponseMaxSizeLimit = httpQueueSettings.LimitHttpResponseMaxSize;

            this.httpQueueRequestTimeout = httpQueueSettings.HttpRequestTimeout;

            this.IsRpcEnabled = enabled;
        }

        #endregion
    }
}
