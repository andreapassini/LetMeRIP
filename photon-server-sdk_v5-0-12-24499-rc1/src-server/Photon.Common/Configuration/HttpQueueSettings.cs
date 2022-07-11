namespace Photon.Common.Configuration
{
    public class HttpQueueSettings
    {
        public int HttpRequestTimeout { get; set; }

        public int MaxBackoffTime { get; set; }

        public int MaxConcurrentRequests { get; set; }

        public int MaxErrorRequests { get; set; }

        public int MaxQueuedRequests { get; set; }

        public int MaxTimedOutRequests { get; set; }

        public int QueueTimeout { get; set; }

        public int ReconnectInterval { get; set; }

        public int LimitHttpResponseMaxSize { get; set; }
    }
}
