using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.Text;
using System.Web;

using ExitGames.Concurrency.Fibers;
using ExitGames.Logging;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Photon.Common.Authentication.CustomAuthentication;
using Photon.Common.Authentication.Data;
using Photon.Common.Authentication.Diagnostic;
using Photon.Common.Configuration;
using Photon.SocketServer;
using Photon.SocketServer.Diagnostics;
using Photon.SocketServer.Misc;
using Photon.SocketServer.Net;

namespace Photon.Common.Authentication
{
    public class CustomAuthHandler
    {
        #region types

        struct RequestParams
        {
            public string ContentType;
            public byte[] PostData;
            public string ClientQueryParameters;
        }

        #endregion

        #region fields and consts

        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        protected readonly IFiber fiber;
        private readonly HttpQueueSettings httpQueueSettings;

        protected Dictionary<ClientAuthenticationType, IClientAuthenticationQueue> authenticationServices = new Dictionary<ClientAuthenticationType, IClientAuthenticationQueue>();

        protected bool isAnonymousAccessAllowed;

        private readonly IHttpRequestQueueCountersFactory httpQueueCountersFactory;
        private readonly LogCountGuard failedToDeserializeResponseGuard = new LogCountGuard(new TimeSpan(0, 1, 0));

        private readonly Action before;
        private readonly Action after;

        #endregion

        #region .ctr

        public CustomAuthHandler(IHttpRequestQueueCountersFactory factory, HttpQueueSettings httpQueueSettings)
            : this(factory, new PoolFiber(), httpQueueSettings)
        {
        }

        protected CustomAuthHandler(IHttpRequestQueueCountersFactory factory, IFiber fiber, HttpQueueSettings httpQueueSettings, Action beforeAction = null, Action afterAction = null)
        {
            this.before = beforeAction;
            this.after = afterAction;

            this.fiber = fiber;

            this.httpQueueSettings = httpQueueSettings;
            this.fiber.Start();
            this.httpQueueCountersFactory = factory;
        }

        #endregion

        #region properties

        public bool IsAnonymousAccessAllowed
        {
            get
            {
                return this.isAnonymousAccessAllowed;
            }
            protected set
            {
                this.isAnonymousAccessAllowed = value;
            }
        }

        public bool IsClientAuthenticationEnabled { get; protected set; }

        #endregion

        public void AuthenticateClient(ICustomAuthPeer peer, IAuthenticateRequest authRequest, AuthSettings authSettings, SendParameters sendParameters, object state, bool strictCustomAuth = true)
        {
            //TBD: why are we enqueuing could be done on the peers fiber
            this.fiber.Enqueue(() => 
                this.OnAuthenticateClient(peer, authRequest, authSettings, sendParameters, state, strictCustomAuth)
            );
        }

        public void InitializeFromConfig()
        {
            this.IsClientAuthenticationEnabled = false;
            var config = Configuration.Auth.AuthSettings.Default;
            if (config == null)
            {
                if (log.IsDebugEnabled)
                {
                    log.Debug("There is no configuration for custom auth in config");
                }
                return;
            }

            if (!config.Enabled)
            {
                if (log.IsDebugEnabled)
                {
                    log.Debug("AuthSettings are disabled in config. No CustomAuth");
                }
                return;
            }

            this.IsClientAuthenticationEnabled = true;
            this.isAnonymousAccessAllowed = config.AllowAnonymous;

            foreach (var provider in config.AuthProviders)
            {
                this.AddNewAuthProvider(provider.AuthUrl, provider.NameValuePairAsQueryString, provider.RejectIfUnavailable,
                    (ClientAuthenticationType)provider.AuthenticationType, provider.ForwardAsJSON, ApplicationBase.Instance.PhotonInstanceName + "_" + provider.Name);
            }
        }

        public void OnCustomAuthenticationError(ICustomAuthPeer peer, IAuthenticateRequest authRequest,
                                       SendParameters sendParameters,
                                       CustomAuthResultCounters counters,
                                       string errorMsg)
        {
            var authenticationType = (ClientAuthenticationType)authRequest.ClientAuthenticationType;

            peer.OnCustomAuthenticationError(
                ErrorCode.CustomAuthenticationFailed,
                errorMsg,
                authRequest,
                sendParameters);

            this.IncrementErrors(authenticationType, counters);
        }

        public IClientAuthenticationQueue AddNewAuthProvider(string url, string nameValuePairAsQueryString, bool rejectIfUnavailable, ClientAuthenticationType authenticationType, 
            bool forwardAsJson, string instanceName)
        {
            var authService = this.CreateClientAuthenticationQueue(url, nameValuePairAsQueryString, rejectIfUnavailable, authenticationType, forwardAsJson, instanceName);

            this.AddNewAuthProvider(authenticationType, authService);

            return authService;
        }

        protected virtual IClientAuthenticationQueue CreateClientAuthenticationQueue(string url,
            string nameValuePairAsQueryString, bool rejectIfUnavailable, ClientAuthenticationType authenticationType,
            bool forwardAsJson, string instanceName)
        {
            var checkUrl = authenticationType != ClientAuthenticationType.Facebook &&
                           authenticationType != ClientAuthenticationType.Steam &&
                           authenticationType != ClientAuthenticationType.Oculus &&
                           authenticationType != ClientAuthenticationType.Viveport;

            var authService = new ClientAuthenticationQueue(
                new ClientAuthenticationQueue.CreateParam
                {
                    Uri = url,
                    QueryStringParameters = nameValuePairAsQueryString,
                    RejectIfUnavailable = rejectIfUnavailable,
                    RequestTimeout = Configuration.Auth.AuthSettings.Default.HttpQueueSettings.HttpRequestTimeout,
                    ForwardAsJSON = forwardAsJson,
                    Before = this.before,
                    After = this.after
                },
                checkUrl
                )
            {
                MaxQueuedRequests = this.httpQueueSettings.MaxQueuedRequests,
                MaxConcurrentRequests = this.httpQueueSettings.MaxConcurrentRequests,
                ReconnectInterval = TimeSpan.FromMilliseconds(this.httpQueueSettings.ReconnectInterval),
                QueueTimeout = TimeSpan.FromMilliseconds(this.httpQueueSettings.QueueTimeout),
                MaxErrorRequests = this.httpQueueSettings.MaxErrorRequests,
                MaxTimedOutRequests = this.httpQueueSettings.MaxTimedOutRequests,
                MaxBackoffTimeInMilliseconds = this.httpQueueSettings.MaxBackoffTime,
                CustomData = CustomAuthResultCounters.GetInstance(instanceName),
                ClientAuthenticationType = authenticationType,
                ResponseMaxSizeLimit = this.httpQueueSettings.LimitHttpResponseMaxSize,
            };

            var counters = this.httpQueueCountersFactory != null ? this.httpQueueCountersFactory.Create(instanceName) : null;
            authService.SetHttpRequestQueueCounters(counters);
            
            return authService;
        }

        protected void AddNewAuthProvider(ClientAuthenticationType authenticationType, IClientAuthenticationQueue authService)
        {
            this.authenticationServices.Add(authenticationType, authService);
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Auth Provider added. provider:{0}, ForwardAsJSON:{1}, QueryString:{2}", 
                    authService.Uri, authService.ForwardAsJSON, authService.QueryStringParameters);
            }
        }

        protected virtual void OnAuthenticateClient(ICustomAuthPeer peer, IAuthenticateRequest authRequest, AuthSettings authSettings, SendParameters sendParameters, object state, bool strictCustomAuth = true)
        {
            try
            {
                // take auth type from auth request (default: custom)
                // ReSharper disable once PossibleInvalidOperationException
                var authenticationType = (ClientAuthenticationType)authRequest.ClientAuthenticationType;

                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("Authenticating client {0} - CustomAuth type: {1}",
                         peer.ConnectionId, authenticationType);
                }

                //anonymous access handling with custom auth
                //new implementation, no custom auth requested from client
                if (strictCustomAuth)
                {
                    if (authenticationType == ClientAuthenticationType.None && this.isAnonymousAccessAllowed)
                    {
                        // instant callback - treat as anonymous user: 
                        if (log.IsDebugEnabled)
                        {
                            log.DebugFormat("Authenticate client: grant access as anonymous user: conId={0}", peer.ConnectionId);
                        }

                        var customResult = new CustomAuthenticationResult { ResultCode = CustomAuthenticationResultCode.Ok };
                        peer.OnCustomAuthenticationResult(customResult, authRequest, sendParameters, state);
                        return;
                    }
                }
                //old implementation, no data and params (+check for console providers first)
                else
                {
                    //ignore isAnonymousAccessAllowed if specified provider is configured but no ClientAuthenticationParams/Data is set (would be handled in next if statement)
                    if (this.authenticationServices.ContainsKey(authenticationType) &&
                        string.IsNullOrWhiteSpace(authRequest.ClientAuthenticationParams) &&
                        authRequest.ClientAuthenticationData == null &&
                        this.isAnonymousAccessAllowed)
                    {
                        switch (authenticationType)
                        {
                            case ClientAuthenticationType.PlayStation:
                            case ClientAuthenticationType.PlayStation5:
                            case ClientAuthenticationType.Xbox:
                            case ClientAuthenticationType.Steam:
                            case ClientAuthenticationType.Oculus:
                            case ClientAuthenticationType.Viveport:
                                //return same error as if AnonymousAccessAllowed = false
                                IClientAuthenticationQueue clientAuthenticationQueue;
                                if (this.authenticationServices.TryGetValue(authenticationType, out clientAuthenticationQueue))
                                {
                                    this.OnCustomAuthenticationError(peer, authRequest, sendParameters, (CustomAuthResultCounters)clientAuthenticationQueue.CustomData, "Parameter invalid");
                                }
                                else
                                {
                                    this.OnCustomAuthenticationError(peer, authRequest, sendParameters, null, "Parameter invalid");
                                }
                                return;

                            default:
                                break;
                        }
                    }

                    if (string.IsNullOrEmpty(authRequest.ClientAuthenticationParams)
                        && authRequest.ClientAuthenticationData == null
                        && this.isAnonymousAccessAllowed)
                    {
                        // instant callback - treat as anonymous user: 
                        if (log.IsDebugEnabled)
                        {
                            log.DebugFormat("Authenticate client: grant access as anonymous user: conId={0}", peer.ConnectionId);
                        }

                        var customResult = new CustomAuthenticationResult { ResultCode = CustomAuthenticationResultCode.Ok };
                        peer.OnCustomAuthenticationResult(customResult, authRequest, sendParameters, state);
                        return;
                    }
                }

                IClientAuthenticationQueue authQueue;
                if (this.authenticationServices.TryGetValue(authenticationType, out authQueue) == false)
                {
                    // TODO log to client bug logger
                    if (log.IsDebugEnabled)
                    {
                        log.DebugFormat("Authentication type not supported: {0} for AppId={1}/{2}", authenticationType,
                            authRequest.ApplicationId, authRequest.ApplicationVersion);
                    }

                    this.OnCustomAuthenticationError(
                        peer,
                        authRequest,
                        sendParameters,
                        null,
                        string.Format("Authentication type '{0}' not supported", authenticationType)
                        );
                    this.IncrementErrors(authenticationType, null);
                    return;
                }

                string errorMsg, authUrl;
                var requestParams = new RequestParams
                {
                    ClientQueryParameters = authRequest.ClientAuthenticationParams,
                };

                if (authQueue.ForwardAsJSON)
                {
                    if (!PrepareRequestJSON(authRequest, authQueue, ref requestParams, out errorMsg))
                    {
                        this.OnCustomAuthenticationError(peer, authRequest, sendParameters, null, errorMsg);

                        return;
                    }
                    authUrl = authQueue.Uri;

                }
                else
                {
                    if (!PrepareRequest(authRequest, authQueue, ref requestParams, out errorMsg))
                    {
                        this.OnCustomAuthenticationError(peer,
                            authRequest,
                            sendParameters,
                            (CustomAuthResultCounters)authQueue.CustomData,
                            errorMsg);

                        return;
                    }

                    authUrl = ConcatenateQueryString(authQueue.Uri, new[]
                    {
                        authQueue.QueryStringParameters, requestParams.ClientQueryParameters
                    });
                }

                if (log.IsDebugEnabled)
                {
                    log.Debug($"Resulting auth url {authUrl}");
                }

                var queueState = new AuthQueueState(peer, authRequest, sendParameters, state);

                switch (authenticationType)
                {
                    case ClientAuthenticationType.Facebook:
                    {
                        if (!this.FacebookAuthenticateClient(authQueue, authRequest, requestParams, queueState))
                        {
                            this.OnCustomAuthenticationError(peer, authRequest, sendParameters, (CustomAuthResultCounters)authQueue.CustomData, "Parameter invalid");
                        }
                        return;
                    }
                    case ClientAuthenticationType.Steam:
                    {
                        if (!this.SteamAuthenticateClient(authQueue, authRequest, requestParams, queueState))
                        {
                            this.OnCustomAuthenticationError(peer, authRequest, sendParameters, (CustomAuthResultCounters)authQueue.CustomData, "Parameter invalid");
                        }
                        return;
                    }
                    case ClientAuthenticationType.Oculus:
                    {
                        if(!this.OculusAuthenticateClient(authQueue, authRequest, requestParams, queueState))
                        {
                            this.OnCustomAuthenticationError(peer, authRequest, sendParameters, (CustomAuthResultCounters)authQueue.CustomData, "Parameter invalid");
                        }
                        return;
                    }

                    case ClientAuthenticationType.Viveport:
                    {
                        if (!this.ViveportAuthenticateClient(authQueue, authRequest, requestParams, queueState))
                        {
                            this.OnCustomAuthenticationError(peer, authRequest, sendParameters, (CustomAuthResultCounters)authQueue.CustomData, "Parameter invalid");
                        }
                        return;
                    }
                }

                authQueue.EnqueueRequest(authUrl, requestParams.PostData, requestParams.ContentType, this.AuthQueueResponseCallback, queueState);
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }

        private static bool PrepareRequestJSON(IAuthenticateRequest authRequest, IClientAuthenticationQueue authQueue, 
            ref RequestParams requestParams, out string errorMsg)
        {
            Dictionary<string, object> dictData = null;
            requestParams.ContentType = "application/json";
            requestParams.ClientQueryParameters = null;

            if (authRequest.ClientAuthenticationData != null)
            {
                dictData = authRequest.ClientAuthenticationData as Dictionary<string, object>;
                if (dictData == null) //still null
                {
                    errorMsg = "JSON mode is on. ClientAuthenticationData could have only type Dictionary<string, object>";
                    return false;
                }
            }

            return HandleAuthDataAsDictJSON(authRequest, authQueue, out requestParams.PostData, dictData, out errorMsg);
        }

        private static bool PrepareRequest(IAuthenticateRequest authRequest, IClientAuthenticationQueue authQueue, ref RequestParams requestParams, out string errorMsg)
        {
            if (authRequest.ClientAuthenticationData != null)
            {
                requestParams.PostData = authRequest.ClientAuthenticationData as byte[];
                if (requestParams.PostData == null)
                {
                    var stringData = authRequest.ClientAuthenticationData as string;
                    if (stringData != null)
                    {
                        requestParams.PostData = Encoding.UTF8.GetBytes(stringData);
                    }
                }

                if (requestParams.PostData == null)
                {
                    var dictData = authRequest.ClientAuthenticationData as Dictionary<string, object>;
                    if (dictData != null)
                    {
                        if (!HandleAuthDataAsDict(authRequest, authQueue, out requestParams.PostData, out requestParams.ClientQueryParameters, dictData, out errorMsg))
                        {
                            return false;
                        }

                        requestParams.ContentType = "application/json";
                        return true;
                    }
                }

                if (requestParams.PostData == null) //still null
                {
                    errorMsg = "Authentication data type not supported";
                    return false;
                }
            }

            requestParams.ClientQueryParameters = RemoveKeyDuplicates(requestParams.ClientQueryParameters, authQueue.QueryStringParametersCollection);
            errorMsg = string.Empty;
            return true;
        }

        private static bool HandleAuthDataAsDictJSON(IAuthenticateRequest authRequest, IClientAuthenticationQueue authQueue, 
            out byte[] authData,  Dictionary<string, object> dictData, out string errorMsg)
        {
            dictData = dictData ?? new Dictionary<string, object>();

            MergeAllToJson(authRequest.ClientAuthenticationParams, dictData, authQueue.QueryStringParametersCollection);

            authData = ConvertToJSON(authRequest, dictData, out errorMsg);
            return authData != null;
        }

        private static bool HandleAuthDataAsDict(IAuthenticateRequest authRequest, IClientAuthenticationQueue authQueue,
            out byte[] authData, out string clientAuthParams, 
            Dictionary<string, object> dictData, out string errorMsg)
        {
            RemoveDuplicatesFromDict(dictData, authQueue.QueryStringParametersCollection);
            clientAuthParams = RemoveKeyDuplicates(authRequest.ClientAuthenticationParams, dictData.Keys);
            clientAuthParams = RemoveKeyDuplicates(clientAuthParams, authQueue.QueryStringParametersCollection);

            authData = ConvertToJSON(authRequest, dictData, out errorMsg);
            return authData != null;
        }

        private void AuthQueueResponseCallback(AsyncHttpResponse response, IClientAuthenticationQueue queue)
        {
            var queueState = (AuthQueueState)response.State;
            var peer = queueState.Peer;
            var authRequest = queueState.AuthenticateRequest;
            var sendParameters = queueState.SendParameters;

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Authenticate client finished: conId={0}, result={1}", peer.ConnectionId, response.Status);
            }

            switch (response.Status)
            {
                case HttpRequestQueueResultCode.Success:
                {
                    if (response.ResponseData == null)
                    {
                        log.ErrorFormat("CustomAuth: failed. ResponseData is empty. AppId={0}/{1}",
                            authRequest.ApplicationId, authRequest.ApplicationId);

                        this.OnCustomAuthenticationError(peer, authRequest, sendParameters, 
                            (CustomAuthResultCounters)queue.CustomData, "CustomAuth got no response data.");
                        return;
                    }

                    this.ProcessAuthResponseData(response, queue, peer, authRequest, sendParameters, queueState);
                    return;
                }

                case HttpRequestQueueResultCode.QueueFull:
                case HttpRequestQueueResultCode.QueueTimeout:
                {
                    if (response.Status == HttpRequestQueueResultCode.QueueFull)
                    {
                        this.IncrementQueueFullErrors((CustomAuthResultCounters)queue.CustomData);
                    }
                    else
                    {
                        this.IncrementQueueTimeouts((CustomAuthResultCounters)queue.CustomData);
                    }

                    break;
                }
                case HttpRequestQueueResultCode.Offline:
                case HttpRequestQueueResultCode.RequestTimeout:
                {
                    this.IncrementHttpTimeouts(queue.ClientAuthenticationType, (CustomAuthResultCounters)queue.CustomData);
                    break;
                }
                case HttpRequestQueueResultCode.Error:
                {
                    if (response.ResponseData != null)
                    {
                        this.ProcessAuthErrorResponseData(response, queue, peer, authRequest, sendParameters, queueState);
                        return;
                    }

                    this.IncrementHttpErrors(queue.ClientAuthenticationType, (CustomAuthResultCounters)queue.CustomData);
                    break;
                }
            }
            // this code is executed in case of failure
            if (response.RejectIfUnavailable)
            {
                this.OnCustomAuthenticationError(peer, authRequest, sendParameters, 
                    (CustomAuthResultCounters)queue.CustomData, "CustomAuth service error: " + response.Status);
            }
            else
            {
                var result = new CustomAuthenticationResult { ResultCode = CustomAuthenticationResultCode.Ok };
                peer.OnCustomAuthenticationResult(result, authRequest, sendParameters, queueState.State);
            }
        }

        private static readonly char[] trimBomAndZeroWidth = { '\uFEFF', '\u200B' };
        private void ProcessAuthResponseData(AsyncHttpResponse response, IClientAuthenticationQueue queue, ICustomAuthPeer peer,
            IAuthenticateRequest authRequest, SendParameters sendParameters, AuthQueueState queueState)
        {
            var responseString = Encoding.UTF8.GetString(response.ResponseData).Trim(trimBomAndZeroWidth);
            if (string.IsNullOrEmpty(responseString))
            {
                this.OnCustomAuthenticationError(peer, authRequest, sendParameters,
                    (CustomAuthResultCounters) queue.CustomData, "CustomAuth got empty response string.");

                // TODO log to client bug logger
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat(
                        "CustomAuth: got empty response string. AppId={0}/{1}, Response={2}, Uri={3}, Data:{4}",
                        authRequest.ApplicationId, authRequest.ApplicationVersion,
                        responseString, queue.Uri, BitConverter.ToString(response.ResponseData,
                            0, Math.Min(100, response.ResponseData.Length)));
                }

                return;
            }

            if (log.IsDebugEnabled)
            {
                log.Debug($"CustomAuth response {responseString}");
            }

            switch (queue.ClientAuthenticationType)
            {
                case ClientAuthenticationType.Facebook:
                {
                    this.FacebookResponseCallback(responseString, response, queue, peer, authRequest, sendParameters, queueState);
                    return;
                }
                case ClientAuthenticationType.Steam:
                {
                    this.SteamResponseCallback(responseString, response, queue, peer, authRequest, sendParameters, queueState);
                    return;
                }
                case ClientAuthenticationType.Oculus:
                {
                    this.OculusResponseCallback(responseString, response, queue, peer, authRequest, sendParameters, queueState);
                    return;
                }
                case ClientAuthenticationType.Viveport:
                {
                    this.ViveportResponseCallback(responseString, response, queue, peer, authRequest, sendParameters, queueState);
                    return;
                }
            }

            // deserialize
            CustomAuthenticationResult customAuthResult;
            try
            {
                customAuthResult = JsonConvert.DeserializeObject<CustomAuthenticationResult>(responseString);
                //TBD: handle backward compatibility in customAuthResult class
                if (customAuthResult.AuthCookie == null)
                {
#pragma warning disable CS0618 // Type or member is obsolete
                    customAuthResult.AuthCookie = customAuthResult.Secure;
#pragma warning restore CS0618 // Type or member is obsolete
                }
            }
            catch (Exception ex)
            {
                log.Warn(this.failedToDeserializeResponseGuard,
                    $"CustomAuth: failed to deserialize response. " +
                    $"AppId={authRequest.ApplicationId}/{authRequest.ApplicationVersion}, " +
                    $"UserId={authRequest.UserId} " +
                    $"Response={responseString.Limit()}, Uri={queue.Uri}, Exception Msg:{ex.Message}");

                this.OnCustomAuthenticationError(peer, authRequest, sendParameters,
                    (CustomAuthResultCounters) queue.CustomData, "CustomAuth deserialization failed: " + ex.Message);
                return;
            }

            this.IncrementResultCounters(customAuthResult, (CustomAuthResultCounters) queue.CustomData, response.ElapsedTicks);
            peer.OnCustomAuthenticationResult(customAuthResult, authRequest, sendParameters, queueState.State);
            return;
        }

        private void ProcessAuthErrorResponseData(AsyncHttpResponse response, IClientAuthenticationQueue queue, ICustomAuthPeer peer,
            IAuthenticateRequest authRequest, SendParameters sendParameters, AuthQueueState queueState)
        {
            var responseString = Encoding.UTF8.GetString(response.ResponseData).Trim(trimBomAndZeroWidth);
            if (string.IsNullOrEmpty(responseString))
            {
                this.OnCustomAuthenticationError(peer, authRequest, sendParameters,
                    (CustomAuthResultCounters) queue.CustomData, "CustomAuth got empty response string.");

                if (log.IsDebugEnabled)
                {
                    log.DebugFormat(
                        "CustomAuth: got empty response string. AppId={0}/{1}, Response={2}, Uri={3}, Data:{4}",
                        authRequest.ApplicationId, authRequest.ApplicationVersion,
                        responseString, queue.Uri, BitConverter.ToString(response.ResponseData,
                            0, Math.Min(100, response.ResponseData.Length)));
                }

                return;
            }

            if (log.IsDebugEnabled)
            {
                log.Debug($"Custom auth response {responseString}");
            }

            // deserialize
            CustomAuthenticationResult customAuthResult;
            try
            {
                customAuthResult = JsonConvert.DeserializeObject<CustomAuthenticationResult>(responseString);
            }
            catch (Exception ex)
            {
                log.Warn(this.failedToDeserializeResponseGuard,
                    $"CustomAuth: failed to deserialize response. " +
                    $"AppId={authRequest.ApplicationId}/{authRequest.ApplicationVersion}, " +
                    $"UserId={authRequest.UserId} " +
                    $"Response={responseString.Limit()}, Uri={queue.Uri}, Exception Msg:{ex.Message}");

                this.OnCustomAuthenticationError(peer, authRequest, sendParameters,
                    (CustomAuthResultCounters) queue.CustomData, "CustomAuth deserialization failed: " + ex.Message);
                return;
            }

            this.IncrementResultCounters(customAuthResult, (CustomAuthResultCounters) queue.CustomData, response.ElapsedTicks);
            peer.OnCustomAuthenticationResult(customAuthResult, authRequest, sendParameters, queueState.State);
        }

        private static byte[] ConvertToJSON(IAuthenticateRequest authRequest, Dictionary<string, object> dictData, out string errorMsg)
        {
            errorMsg = string.Empty;
            try
            {
                return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(dictData));
            }
            catch (Exception e)
            {
                var msg = string.Format(
                    "Exception during ClientAuthenticationData dictionary JSON serialization: {0} for AppId={1}/{2}",
                    authRequest.ClientAuthenticationType,
                    authRequest.ApplicationId, authRequest.ApplicationVersion);

                if (log.IsWarnEnabled)
                {
                    log.Warn(msg, e);
                }

                errorMsg = string.Format("{0}, Exception:{1}", msg, e);
            }

            return null;
        }

        private static void MergeAllToJson(string clientAuthenticationParams, Dictionary<string, object> dictData, NameValueCollection queryStringParametersCollection)
        {
            var clientAuthParams = string.IsNullOrEmpty(clientAuthenticationParams)
                ? new NameValueCollection()
                : HttpUtility.ParseQueryString(clientAuthenticationParams);

            for (var i = 0; i < clientAuthParams.Count; ++i) 
            {
                if (!dictData.ContainsKey(clientAuthParams.GetKey(i)))
                {
                    dictData.Add(clientAuthParams.GetKey(i), clientAuthParams[i]);
                }
            }

            if (queryStringParametersCollection == null || queryStringParametersCollection.Count == 0)
            {
                return;
            }

            for (var i = 0; i < queryStringParametersCollection.Count; ++i)
            {
                dictData[queryStringParametersCollection.GetKey(i)] = queryStringParametersCollection[i];
            }
        }

        protected virtual void IncrementResultCounters(CustomAuthenticationResult customAuthResult, CustomAuthResultCounters instance, long ticks)
        {
            switch (customAuthResult.ResultCode)
            {
                case CustomAuthenticationResultCode.Data:
                    CustomAuthResultCounters.IncrementResultsData(instance);
                    break;
                case CustomAuthenticationResultCode.Ok:
                    CustomAuthResultCounters.IncrementResultsAccepted(instance);
                    break;
                default://CustomAuthenticationResultCode.Failed, CustomAuthenticationResultCode.ParameterInvalid
                    CustomAuthResultCounters.IncrementResultsDenied(instance);
                    break;
            }

            CustomAuthResultCounters.IncrementHttpRequests(ticks, instance);
        }

        protected virtual void IncrementErrors(ClientAuthenticationType authenticationType, CustomAuthResultCounters instance)
        {
            CustomAuthResultCounters.IncrementErrors(instance);
        }

        protected virtual void IncrementQueueTimeouts(CustomAuthResultCounters instance)
        {
            CustomAuthResultCounters.IncrementQueueTimeouts(instance);
        }

        protected virtual void IncrementQueueFullErrors(CustomAuthResultCounters instance)
        {
            CustomAuthResultCounters.IncrementQueueFullErrors(instance);
        }

        protected virtual void IncrementHttpErrors(ClientAuthenticationType queueClientAuthenticationType,
            CustomAuthResultCounters queueCustomData)
        {
            CustomAuthResultCounters.IncrementHttpErrors(queueCustomData);
        }

        protected virtual void IncrementHttpTimeouts(ClientAuthenticationType queueClientAuthenticationType,
            CustomAuthResultCounters queueCustomData)
        {
            CustomAuthResultCounters.IncrementHttpTimeouts(queueCustomData);
        }

        private static string RemoveKeyDuplicates(string clientAuthenticationRequestUrl, ICollection queryStringParametersCollectionKeys)
        {
            if (string.IsNullOrEmpty(clientAuthenticationRequestUrl))
            {
                return string.Empty;
            }

            if (queryStringParametersCollectionKeys == null || queryStringParametersCollectionKeys.Count == 0)
            {
                return clientAuthenticationRequestUrl;
            }

            var clientKeyValues = HttpUtility.ParseQueryString(clientAuthenticationRequestUrl);
            if (clientKeyValues.Count == 0)
            {
                return string.Empty;
            }

            foreach (string keyValue in queryStringParametersCollectionKeys)
            {
                clientKeyValues.Remove(keyValue);
            }

            return clientKeyValues.ToString();
        }

        private static void RemoveDuplicatesFromDict(Dictionary<string, object> clientData, NameValueCollection queryStringParametersCollection)
        {
            if (clientData.Count == 0 || queryStringParametersCollection == null)
            {
                return;
            }

            foreach (var keyValue in queryStringParametersCollection.AllKeys)
            {
                clientData.Remove(keyValue);
            }
        }

        private static string ConcatenateQueryString(string queryString, IEnumerable<string> queryStringsToAppend)
        {
            string result = queryString;

            foreach (var s in queryStringsToAppend)
            {
                if (!string.IsNullOrEmpty(s))
                {
                    if (!result.Contains("?"))
                    {
                        result += "?";
                    }
                    else
                    {
                        if (!result.EndsWith("&"))
                        {
                            result += "&";
                        }
                    }

                    result += s;
                }
            }

            return result;
        }

        #region Facebook

        private bool FacebookAuthenticateClient(IClientAuthenticationQueue authQueue, IAuthenticateRequest authRequest, RequestParams requestParams, AuthQueueState queueState)
        {
            if (string.IsNullOrWhiteSpace(authRequest.ClientAuthenticationParams) || authQueue.QueryStringParametersCollection == null)
            {
                return false;
            }

            var appId = authQueue.QueryStringParametersCollection["appid"];
            var secret = authQueue.QueryStringParametersCollection["secret"];

            //PhotonNetwork.AuthValues.AddAuthParameter("token", aToken);
            var clientKeyValues = HttpUtility.ParseQueryString(authRequest.ClientAuthenticationParams);
            var token = clientKeyValues["token"];
            if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            var authUrl = string.Format("https://graph.facebook.com/debug_token?input_token={0}&access_token={1}|{2}", token, appId, secret);
            if (log.IsDebugEnabled)
            {
                log.DebugFormat(authUrl);
            }
            authQueue.EnqueueRequest(authUrl, null, requestParams.ContentType, this.AuthQueueResponseCallback, queueState, false);
            return true;
        }

        private void FacebookResponseCallback(string responseString, AsyncHttpResponse response, IClientAuthenticationQueue queue, ICustomAuthPeer peer, IAuthenticateRequest authRequest, SendParameters sendParameters, AuthQueueState queueState)
        {
            bool valid = false;
            string userId;
            try
            {
                valid = this.FacebookValidateToken(responseString, out userId);
            }
            catch (Exception ex)
            {
                //which log level?
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("FacebookValidateToken exception: {0}", ex.Message);
                }
            }

            var customAuthResult = new CustomAuthenticationResult { ResultCode = valid ? ResultCodes.Ok : ResultCodes.Failed };
            if (!valid)
            {
                customAuthResult.Message = "no valid";
            }
            //would be new behaviour to set the UserId
//            else
//            {
//                customAuthResult.UserId = userId;
//            }
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Facebook result: {0}/{1}", customAuthResult.ResultCode, customAuthResult.Message);
//                log.DebugFormat("Facebook result: {0}/{1}/{2}", customAuthResult.ResultCode, customAuthResult.Message, customAuthResult.UserId);
            }
            this.IncrementResultCounters(customAuthResult, (CustomAuthResultCounters)queue.CustomData, response.ElapsedTicks);
            peer.OnCustomAuthenticationResult(customAuthResult, authRequest, sendParameters, queueState.State);
        }

        private bool FacebookValidateToken(string response, out string userId)
        {
            userId = null;

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("debug_token - response={0}", response);
            }

            JObject jsonObject = JObject.Parse(response);
            JToken data = jsonObject["data"];
            if (data == null)
            {
                return false;
            }

            JToken isValid = data["is_valid"];
            if (isValid == null)
            {
                return false;
            }

            JToken userIdToken = data["user_id"];
            if (userIdToken == null)
            {
                return false;
            }

            string isValidstring = (string)isValid;
            if (isValidstring.Equals("true", StringComparison.InvariantCultureIgnoreCase))
            {
                userId = userIdToken.ToString();
                return true;
            }

            return false;
        }

        #endregion

        #region Steam

        private bool SteamAuthenticateClient(IClientAuthenticationQueue authQueue, IAuthenticateRequest authRequest, RequestParams requestParams, AuthQueueState queueState)
        {
            if (string.IsNullOrWhiteSpace(authRequest.ClientAuthenticationParams) || authQueue.QueryStringParametersCollection == null)
            {
                return false;
            }

            var key = authQueue.QueryStringParametersCollection["apiKeySecret"];
            var appid = authQueue.QueryStringParametersCollection["appid"];

            //loadBalancingClient.AuthValues.AddAuthParameter("ticket", SteamAuthSessionTicket);
            var clientKeyValues = HttpUtility.ParseQueryString(authRequest.ClientAuthenticationParams);
            var ticket = clientKeyValues["ticket"];

            //do we want to check verifyOwnership/verifyVacBan/verifyPubBan here? it is set to false if invalid. maybe change default to true?
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(appid) || string.IsNullOrWhiteSpace(ticket))
            {
                return false;
            }

            var authUrl = string.Format(
                "https://partner.steam-api.com/{0}?key={1}&appid={2}&ticket={3}",
                "ISteamUserAuth/AuthenticateUserTicket/v1",
                key,
                appid,
                ticket);

            if (log.IsDebugEnabled)
            {
                log.DebugFormat(authUrl);
            }
            authQueue.EnqueueRequest(authUrl, null, requestParams.ContentType, this.AuthQueueResponseCallback, queueState, false);
            return true;
        }

        private void SteamResponseCallback(string responseString, AsyncHttpResponse response, IClientAuthenticationQueue queue, ICustomAuthPeer peer, IAuthenticateRequest authRequest, SendParameters sendParameters, AuthQueueState queueState)
        {
            //change default to true?
            bool validateOwnership;
            bool validateVacBanned;
            bool validatePublisherBanned;

            if (!bool.TryParse(queue.QueryStringParametersCollection["verifyOwnership"], out validateOwnership))
            {
                validateOwnership = true;
            }
            if (!bool.TryParse(queue.QueryStringParametersCollection["verifyVacBan"], out validateVacBanned))
            {
                validateVacBanned = true;
            }
            if (!bool.TryParse(queue.QueryStringParametersCollection["verifyPubBan"], out validatePublisherBanned))
            {
                validatePublisherBanned = true;
            }
            
            Result result;
            try
            {
                result = SteamValidateResponse(responseString, validateOwnership, validateVacBanned, validatePublisherBanned);
            }
            catch (Exception ex)
            {
                //which log level?
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("SteamValidateResponse exception: {0}", ex.Message);
                }
                result = new Result { ResultCode = ResultCodes.Failed, Message = ex.Message };
            }

            CustomAuthenticationResult customAuthResult = new CustomAuthenticationResult { ResultCode = result.ResultCode, UserId = result.UserId };
            if (result.ResultCode != ResultCodes.Ok)
            {
                customAuthResult.Message = result.Message;
            }
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Steam result: {0}/{1}/{2}", customAuthResult.ResultCode, customAuthResult.Message, customAuthResult.UserId);
            }
            this.IncrementResultCounters(customAuthResult, (CustomAuthResultCounters)queue.CustomData, response.ElapsedTicks);
            peer.OnCustomAuthenticationResult(customAuthResult, authRequest, sendParameters, queueState.State);
        }

           
        //{
        //    "response": {
        //        "params": {
        //            "result": "OK",
        //            "steamid": "76561198008509133",
        //            "ownersteamid": "76561198008509133",
        //            "vacbanned": false,
        //            "publisherbanned": false
        //        }
        //    }
        //}
        private Result SteamValidateResponse(string responseString, bool validateOwnership, bool validateVacBanned, bool validatePublisherBanned)
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("response={0} - validate {1}/{2}/{3}", responseString, validateOwnership, validateVacBanned, validatePublisherBanned);
            }

            JObject authResponse = JObject.Parse(responseString);

            if (authResponse["response"]["error"] != null)
            {
                var failedResponse = new Result
                {
                    ResultCode = ResultCodes.Failed,
                    Message = string.Format("Failed({0}): '{1}'", ResultCodes.Failed, authResponse["response"]["error"]["errordesc"])
                };
                return failedResponse;
            }

            string userId = authResponse["response"]["params"]["steamid"].ToString();
            if (string.IsNullOrWhiteSpace(userId))
            {

                var failedResponseUnknown = new Result { ResultCode = ResultCodes.Failed, Message = string.Format("Failed({0}): 'unknown error'", ResultCodes.Failed) };
                return failedResponseUnknown;
            }

            if (validateOwnership)
            {
                string ownerid = authResponse["response"]["params"]["ownersteamid"].ToString();
                if (string.IsNullOrWhiteSpace(ownerid))
                {
                    var response = new Result { ResultCode = ResultCodes.Failed, UserId = userId, Message = "does not own app" };
                    return response;
                }

            }

            if (validateVacBanned)
            {
                string vacbanned = authResponse["response"]["params"]["vacbanned"].ToString();
                bool vacbannedParsed;
                bool parsed = bool.TryParse(vacbanned, out vacbannedParsed);
                if (!parsed || vacbannedParsed)
                {
                    var response = new Result { ResultCode = ResultCodes.Failed, UserId = userId, Message = "VAC ban" };
                    return response;
                }

            }

            if (validatePublisherBanned)
            {
                string publisherbanned = authResponse["response"]["params"]["publisherbanned"].ToString();
                bool pubbannedParsed;
                bool parsed = bool.TryParse(publisherbanned, out pubbannedParsed);
                if (!parsed || pubbannedParsed)
                {
                    var response = new Result { ResultCode = ResultCodes.Failed, UserId = userId, Message = "publisher ban" };
                    return response;
                }
            }

            var resultOk = new Result { ResultCode = ResultCodes.Ok, UserId = userId };
            return resultOk;
        }

        #endregion

        #region Oculus

        private bool OculusAuthenticateClient(IClientAuthenticationQueue authQueue, IAuthenticateRequest authRequest, RequestParams requestParams, AuthQueueState queueState)
        {
            if (string.IsNullOrWhiteSpace(authRequest.ClientAuthenticationParams) || authQueue.QueryStringParametersCollection == null)
            {
                return false;
            }

            var appId = authQueue.QueryStringParametersCollection["appid"];
            var secret = authQueue.QueryStringParametersCollection["appsecret"];

            //loadBalancingClient.AuthValues.AddAuthParameter("userid", oculusId);
            //loadBalancingClient.AuthValues.AddAuthParameter("nonce", oculusNonce);
            var clientKeyValues = HttpUtility.ParseQueryString(authRequest.ClientAuthenticationParams);
            var userId = clientKeyValues["userid"];
            var nonce = clientKeyValues["nonce"];

            if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(nonce))
            {
                return false;
            }

            //new, we allow multiple oculus configurations in dashboard. appid and appsecret separated by semicolon. client sends index to decide which to use. default is old behaviour (single configuration only)
            if (appId.Contains(";") || secret.Contains(";"))
            {
                //default - use first entry
                if (clientKeyValues["authParamIndex"] == null)
                {
                    if (log.IsDebugEnabled)
                    {
                        log.Debug("OculusAuthenticateClient, multiple Oculus entries in dashboard but client didn't send index, using first entry (default)");
                    }

                    appId = appId.Split(';')[0];
                    secret = secret.Split(';')[0];
                }
                else
                {
                    int index;
                    if (!int.TryParse(clientKeyValues["authParamIndex"], out index))
                    {
                        if (log.IsDebugEnabled)
                        {
                            log.DebugFormat("OculusAuthenticateClient, cannot parse index '{0}'", clientKeyValues["authParamIndex"]);
                        }
                        return false;
                    }

                    var appIdSplit = appId.Split(';');
                    var secretSplit = secret.Split(';');

                    if (index >= appIdSplit.Length || index >= secretSplit.Length)
                    {
                        return false;
                    }

                    appId = appIdSplit[index];
                    secret = secretSplit[index];

                    if (log.IsDebugEnabled)
                    {
                        log.DebugFormat("OculusAuthenticateClient, using index '{0}', appId '{1}', secret '{2}'", index, appId, secret);
                    }
                }
            }
            //test for missing configuration in dashboard
            else if (clientKeyValues["authParamIndex"] != null)
            {
                if (!clientKeyValues["authParamIndex"].Equals("0"))
                {
                    if (log.IsDebugEnabled)
                    {
                        log.DebugFormat("OculusAuthenticateClient, client request index '{0}', not configured in dashboard", clientKeyValues["authParamIndex"]);
                    }

                    return false;
                }
            }

            string accessToken = string.Format("OC|{0}|{1}", appId, secret);

            string authUrl = string.Format(
                "https://graph.oculus.com/{0}?access_token={1}&nonce={2}&user_id={3}",
                "user_nonce_validate",
                accessToken,
                nonce,
                userId);
            if (log.IsDebugEnabled)
            {
                log.DebugFormat(authUrl);
            }
            authQueue.EnqueueRequest(authUrl, new byte[0], requestParams.ContentType, this.AuthQueueResponseCallback, queueState, false);
            return true;
        }

        private void OculusResponseCallback(string responseString, AsyncHttpResponse response, IClientAuthenticationQueue queue, ICustomAuthPeer peer, IAuthenticateRequest authRequest, SendParameters sendParameters, AuthQueueState queueState)
        {
            // validate response data
            var valid = responseString.Contains("is_valid") && responseString.Contains("true");

            CustomAuthenticationResult customAuthResult;
            if (valid)
            {
//                var clientKeyValues = HttpUtility.ParseQueryString(authRequest.ClientAuthenticationParams);
                customAuthResult = new CustomAuthenticationResult
                {
                    ResultCode = ResultCodes.Ok,
                    //would be new behaviour to set the UserId
//                    UserId = clientKeyValues["userid"]
                };
            }
            else
            {
                customAuthResult = new CustomAuthenticationResult { ResultCode = ResultCodes.Failed, Message = "Oculus nonce validation failed" };
            }

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Oculus result: {0}/{1}", customAuthResult.ResultCode, customAuthResult.Message);
//                log.DebugFormat("Oculus result: {0}/{1}/{2}", customAuthResult.ResultCode, customAuthResult.Message, customAuthResult.UserId);
            }
            this.IncrementResultCounters(customAuthResult, (CustomAuthResultCounters)queue.CustomData, response.ElapsedTicks);
            peer.OnCustomAuthenticationResult(customAuthResult, authRequest, sendParameters, queueState.State);
        }


        #endregion

        //TODO move to VAppsCustomAuthHandler
        #region Viveport

        private bool ViveportAuthenticateClient(IClientAuthenticationQueue authQueue, IAuthenticateRequest authRequest, RequestParams requestParams, AuthQueueState queueState)
        {
            if (string.IsNullOrWhiteSpace(authRequest.ClientAuthenticationParams) || authQueue.QueryStringParametersCollection == null)
            {
                return false;
            }

            //get SessionToken
            var appId = authQueue.QueryStringParametersCollection["appid"];
            var appSecret = authQueue.QueryStringParametersCollection["appsecret"];

            var clientKeyValues = HttpUtility.ParseQueryString(authRequest.ClientAuthenticationParams);
            var userToken = clientKeyValues["usertoken"];

            if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(appSecret) || string.IsNullOrWhiteSpace(userToken))
            {
                return false;
            }

            var sessionUrl = string.Format("https://www.viveport.com/api/thirdpartygatewayservice/v1/monitor/servicetoken?appId={0}&appSecret={1}", appId, appSecret);

//            var webRequest = (HttpWebRequest)WebRequest.Create(sessionUrl);
//            webRequest.ContentType = requestParams.ContentType;

            authQueue.EnqueueRequest(sessionUrl, null, requestParams.ContentType, this.AuthQueueResponseCallback, queueState, false);
            return true;
        }

        private void ViveportValidateUserToken(IClientAuthenticationQueue authQueue, IAuthenticateRequest authRequest, AuthQueueState queueState, string serviceToken)
        {
            var appId = authQueue.QueryStringParametersCollection["appid"];

            var clientKeyValues = HttpUtility.ParseQueryString(authRequest.ClientAuthenticationParams);
            var userToken = clientKeyValues["usertoken"];

            var postDict = new Dictionary<string, object>
            {
                {"appId", appId},
                {"serviceToken", serviceToken},
                {"userToken", userToken}
            };

            string errorMsg;
            var postData = ConvertToJSON(authRequest, postDict, out errorMsg);

            if (!string.IsNullOrEmpty(errorMsg))
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("ViveportAuthenticateClient, ConvertToJSON error: {0}", errorMsg);
                }
                
                ViveportFailed(authQueue, queueState);
                return;
            }

            var authUrl = "https://www.viveport.com/api/thirdpartygatewayservice/v2/monitor/tokens/validate";

            var httpWebRequest = (HttpWebRequest)WebRequest.Create(authUrl);

            //required to work with Viveport
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Accept = "application/json";
            httpWebRequest.ServicePoint.Expect100Continue = false;

            var expectedStatusCodes = new List<HttpStatusCode> { HttpStatusCode.NotAcceptable, HttpStatusCode.Forbidden };

            authQueue.EnqueueRequestWithExpectedStatusCodes(httpWebRequest, postData, this.AuthQueueResponseCallback, queueState, expectedStatusCodes);
        }

        private void ViveportResponseCallback(string responseString, AsyncHttpResponse response, IClientAuthenticationQueue queue, ICustomAuthPeer peer, IAuthenticateRequest authRequest, SendParameters sendParameters, AuthQueueState queueState)
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("ViveportResponseCallback: {0}", responseString);
            }

            //GetServiceToken response
            if (responseString.Contains("serviceToken"))
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("GetServiceToken response");
                }

                try
                {
                    var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseString);
                    if (!dict.ContainsKey("serviceToken"))
                    {
                        ViveportFailed(queue, queueState);
                        return;
                    }
                    var serviceToken = dict["serviceToken"].ToString();
                    ViveportValidateUserToken(queue, queueState.AuthenticateRequest, queueState, serviceToken);
                }
                catch (Exception)
                {
                    log.WarnFormat("Could not deserialize Viveport GetSessionToken response '{0}'", responseString);

                    ViveportFailed(queue, queueState);
                }
                return;
            }

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("ValidateUserToken response");
            }

            //ValidateUserToken response
            CustomAuthenticationResult customAuthResult;
            // validate response data: {"userId": "00000000-0000-0000-0000-000000000000"}
            // validate response data v2: {"publicName": "XY","userId": "00000000-0000-0000-0000-000000000000"}
            var valid = responseString.ToLower().Contains("userid");

            if (valid)
            {
                JObject authResponse = JObject.Parse(responseString);
                var userId = authResponse["userId"].ToString();
                var publicName = authResponse["publicName"].ToString();
                customAuthResult = new CustomAuthenticationResult { ResultCode = ResultCodes.Ok, UserId = userId, Nickname = publicName };
            }
            else
            {
                //406 Not Acceptable > UserToken error (most likely expired)
                var userTokenExpired = responseString.StartsWith(((int)HttpStatusCode.NotAcceptable).ToString());
                customAuthResult = userTokenExpired
                ? new CustomAuthenticationResult { ResultCode = ResultCodes.Failed, Message = "Viveport validation failed, user token expired" }
                : new CustomAuthenticationResult { ResultCode = ResultCodes.Failed, Message = "Viveport validation failed" };   //403 (Forbidden) SessionToken expired
            }

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Viveport result: {0}/{1}/{2}", customAuthResult.ResultCode, customAuthResult.Message, customAuthResult.UserId);
            }

            this.IncrementResultCounters(customAuthResult, (CustomAuthResultCounters)queue.CustomData, response.ElapsedTicks);
            peer.OnCustomAuthenticationResult(customAuthResult, authRequest, sendParameters, queueState.State);
        }

        private void ViveportFailed(IClientAuthenticationQueue queue, AuthQueueState queueState)
        {
            var result = new CustomAuthenticationResult
            {
                ResultCode = ResultCodes.Failed,
                Message = "Viveport validation failed"
            };

            this.IncrementErrors(queue.ClientAuthenticationType, (CustomAuthResultCounters)queue.CustomData);
            queueState.Peer.OnCustomAuthenticationResult(result, queueState.AuthenticateRequest, queueState.SendParameters, queueState.State);
        }

        #endregion

        #region CustomAuth Common/Helper

        private const long UnixEpochTicks = 621355968000000000L;        //TimeSpan.TicksPerDay * DateTime.DaysTo1970;
        protected static DateTimeOffset FromUnixTimeSeconds(long seconds)
        {
            //repaced with values
            const long MinSeconds = -62135596800L;      //DateTime.MinTicks / TimeSpan.TicksPerSecond - UnixEpochSeconds;
            const long MaxSeconds = 253402300799L;      //DateTime.MaxTicks / TimeSpan.TicksPerSecond - UnixEpochSeconds;

            if (seconds < MinSeconds || seconds > MaxSeconds)
            {
                throw new ArgumentOutOfRangeException("seconds", string.Format("Value must be between {0} and {1}: {2}", MinSeconds, MaxSeconds, seconds));
            }

            long ticks = seconds * TimeSpan.TicksPerSecond + UnixEpochTicks;
            return new DateTimeOffset(ticks, TimeSpan.Zero);
        }

        //TODO keep this classes?
        protected class Result
        {
            public byte ResultCode { get; set; }

            public string UserId { get; set; }

            public string Nickname { get; set; }

            public string Message { get; set; }

            public bool IsSuccess
            {
                get
                {
                    return this.ResultCode == ResultCodes.Ok;
                }
            }
        }

        protected class ResultCodes
        {
            public static byte OkData { get { return 0; } }

            public static byte Ok { get { return 1; } }

            public static byte Failed { get { return 2; } }

            public static byte ParameterInvalid { get { return 3; } }
        }

        #endregion

        protected class AuthQueueState
        {
            public readonly ICustomAuthPeer Peer;

            public readonly IAuthenticateRequest AuthenticateRequest;

            public readonly object State;

            public readonly SendParameters SendParameters;

            public AuthQueueState(ICustomAuthPeer peer, IAuthenticateRequest authenticateRequest, 
                SendParameters sendParameters, object state)
            {
                this.Peer = peer;
                this.AuthenticateRequest = authenticateRequest;
                this.State = state;
                this.SendParameters = sendParameters;
            }
        }
    }
}
