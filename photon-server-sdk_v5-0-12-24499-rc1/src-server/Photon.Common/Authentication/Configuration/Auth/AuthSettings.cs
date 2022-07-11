using System.Collections.Generic;

using Microsoft.Extensions.Configuration;

using Photon.SocketServer;
using Photon.SocketServer.Annotations;

namespace Photon.Common.Authentication.Configuration.Auth
{
    [SettingsMarker("Photon:CustomAuth")]
    public class AuthSettings
    {
        public class HttpQueueSettingsClass : Photon.Common.Configuration.HttpQueueSettings
        {
            public HttpQueueSettingsClass()
            {
                this.HttpRequestTimeout = 30000;
                this.MaxBackoffTime = 10000;
                this.MaxConcurrentRequests = 50;
                this.MaxErrorRequests = 10;
                this.MaxQueuedRequests = 5000;
                this.MaxTimedOutRequests = 10;
                this.QueueTimeout = 20000;
                this.ReconnectInterval = 60000;
                this.LimitHttpResponseMaxSize = 1000;
            }
    }

        // Explicit static constructor to tell C# compiler
        // not to mark type as beforefieldinit
        static AuthSettings()
        { }

        #region Properties

        public static AuthSettings Default { get; } = GetSectionAndLoadProviders();

        public bool Enabled { get; set; } = false;

        public bool AllowAnonymous { get; set; } = false;

        public HttpQueueSettingsClass HttpQueueSettings { get; set; }= new HttpQueueSettingsClass();

        public List<AuthProvider> AuthProviders { get; } = new List<AuthProvider>();
        #endregion

        #region privates

        private static AuthSettings GetSectionAndLoadProviders()
        {
            var result = ApplicationBase.GetConfigSectionAndValidate<AuthSettings>("Photon:CustomAuth");

            result.AuthProviders.Clear();

            if (result.Enabled == false)
            {
                return result;
            }

            var authProviderSection = ApplicationBase.Instance.Configuration.GetSection("Photon:CustomAuth:AuthProviders:AuthProvider");

            foreach (var p in authProviderSection.GetChildren())
            {
                var r = p.Get<AuthProvider>();
                if (r == null)
                {
                    continue;
                }
                r.PostDeserialize();
                result.AuthProviders.Add(r);
            }
            return result;
        }

        #endregion
    }
}
