using Photon.Common.Configuration;

namespace Photon.Hive.Common
{
    /// <summary>
    /// Provides http queue settings that we set in game server config section
    /// </summary>
    public class GameHttpQueueSettings : HttpQueueSettings
    {
        public GameHttpQueueSettings()
        {
            this.MaxErrorRequests = 1;
            this.MaxTimedOutRequests = 1;
            this.LimitHttpResponseMaxSize = 300_000;
            this.HttpRequestTimeout = 10_000;
            this.QueueTimeout = 30_000;
            this.MaxBackoffTime = 10_000;
            this.ReconnectInterval = 60_000;
            this.MaxQueuedRequests = 5_000;
            this.MaxConcurrentRequests = 1;
        }
    }
}
