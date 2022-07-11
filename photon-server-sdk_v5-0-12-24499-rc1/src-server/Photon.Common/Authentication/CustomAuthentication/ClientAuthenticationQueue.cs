// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ClientAuthenticationQueue.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the HttpRequestQueueResultCode2 type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Web;
using ExitGames.Concurrency.Core;
using ExitGames.Concurrency.Fibers;
using ExitGames.Logging;
using ExitGames.Threading;
using Newtonsoft.Json;

using Photon.Common.Authentication.Data;
using Photon.SocketServer.Diagnostics;
using Photon.SocketServer.Net;

namespace Photon.Common.Authentication.CustomAuthentication
{
    public class AsyncHttpResponse
    {
        public AsyncHttpResponse(HttpRequestQueueResultCode status, bool rejectIfUnavailable, object state)
        {
            this.Status = status;
            this.State = state;
            this.RejectIfUnavailable = rejectIfUnavailable;
        }

        public HttpRequestQueueResultCode Status { get; private set; }

        public object State { get; set; }

        public byte[] ResponseData { get; set; }

        public bool RejectIfUnavailable { get; set; }

        public long ElapsedTicks { get; set; }
    }

    public interface IClientAuthenticationQueue
    {
        NameValueCollection QueryStringParametersCollection { get; }
        string Uri { get; }
        string QueryStringParameters { get; }
        bool RejectIfUnavailable { get; }
        bool ForwardAsJSON { get; }

        ClientAuthenticationType ClientAuthenticationType { get; }

        object CustomData { get; }

        void EnqueueRequest(string clientQueryStringParamters, byte[] postData, string contentType,
            Action<AsyncHttpResponse, IClientAuthenticationQueue> callback, object state, bool checkUrl = true);

        void EnqueueRequestWithExpectedStatusCodes(HttpWebRequest webRequest, byte[] postData, Action<AsyncHttpResponse, IClientAuthenticationQueue> callback, object state, List<HttpStatusCode> expectedStatusCodes);
    }

    public class ClientAuthenticationQueue : IClientAuthenticationQueue
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private readonly PoolFiber fiber;

        private readonly int requestTimeoutMilliseconds;

        private readonly RoundRobinCounter timeoutCounter = new RoundRobinCounter(100);

        private readonly HttpRequestQueue httpRequestQueue;

        private readonly RoundRobinCounter RequestTimeCounter = new RoundRobinCounter(100);

        private readonly LogCountGuard execRequestLogGuard = new LogCountGuard(new TimeSpan(0, 1, 0));

        public struct CreateParam
        {
            public string Uri { get; set; }
            public string QueryStringParameters { get; set; }
            public bool RejectIfUnavailable { get; set; }
            public int RequestTimeout { get; set; }
            public bool ForwardAsJSON { get; set; }
            public Action Before { get; set; }
            public Action After { get; set; }
        }
        public ClientAuthenticationQueue(string uri, string queryStringParameters, bool rejectIfUnavailable,
            int requestTimeout, bool forwardAsJSON)
        : this(new CreateParam { Uri = uri, QueryStringParameters = queryStringParameters, RejectIfUnavailable = rejectIfUnavailable, RequestTimeout = requestTimeout, ForwardAsJSON = forwardAsJSON})
        {

        }

        public ClientAuthenticationQueue(CreateParam param, bool checkUrl = true)
        {
            this.Uri = param.Uri;
            if (checkUrl && !IsValidUrl(param.Uri))
            {
                log.Warn($"Wrong Url was used to create ClientAuthentication Queue. url='{param.Uri}'");
            }

            this.QueryStringParameters = param.QueryStringParameters;

            if (!string.IsNullOrEmpty(param.QueryStringParameters))
            {
                this.QueryStringParametersCollection = HttpUtility.ParseQueryString(param.QueryStringParameters);
            }

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Create authentication queue for address {0}", this.Uri);
            }

            this.requestTimeoutMilliseconds = param.RequestTimeout;
            this.RejectIfUnavailable = param.RejectIfUnavailable;

            var executor = param.Before == null ? (IExecutor)new FailSafeBatchExecutor() : new BeforeAfterExecutor(param.Before, param.After);

            this.fiber = new PoolFiber(executor);

            this.httpRequestQueue = new HttpRequestQueue(this.fiber);
            this.ForwardAsJSON = param.ForwardAsJSON;
        }

        #region Properties
        public int CurrentRequests { get { return this.httpRequestQueue.RunningRequestsCount; } }

        public TimeSpan ReconnectInterval
        {
            get
            {
                return this.httpRequestQueue.ReconnectInterval;
            }
            set
            {
                this.httpRequestQueue.ReconnectInterval = value;
            }
        }

        public TimeSpan QueueTimeout
        {
            get
            {
                return this.httpRequestQueue.QueueTimeout;
            }

            set
            {
                this.httpRequestQueue.QueueTimeout = value;
            }
        }

        public int MaxQueuedRequests
        {
            get
            {
                return this.httpRequestQueue.MaxQueuedRequests;
            }
            set
            {
                this.httpRequestQueue.MaxQueuedRequests = value;
            }
        }

        public int MaxConcurrentRequests
        {
            get
            {
                return this.httpRequestQueue.MaxConcurrentRequests;
            }

            set
            {
                this.httpRequestQueue.MaxConcurrentRequests = value;
            }
        }

        public int MaxErrorRequests
        {
            get
            {
                return this.httpRequestQueue.MaxErrorRequests;
            }

            set
            {
                this.httpRequestQueue.MaxErrorRequests = value;
            }
        }

        public int MaxTimedOutRequests
        {
            get
            {
                return this.httpRequestQueue.MaxTimedOutRequests;
            }

            set
            {
                this.httpRequestQueue.MaxTimedOutRequests = value;
            }
        }

        public int MaxBackoffTimeInMilliseconds
        {
            get
            {
                return this.httpRequestQueue.MaxBackoffInMilliseconds;
            }

            set
            {
                this.httpRequestQueue.MaxBackoffInMilliseconds = value;
            }
        }

        public ClientAuthenticationType ClientAuthenticationType { get; set; }
        public object CustomData { get; set; }

        public NameValueCollection QueryStringParametersCollection { get; private set; }

        public string Uri { get; private set; }

        public string QueryStringParameters { get; private set; }

        public bool RejectIfUnavailable { get; private set; }

        public bool ForwardAsJSON { get; private set; }

        public int ResponseMaxSizeLimit
        {
            get => this.httpRequestQueue.ResponseMaxSizeLimit;
            set => this.httpRequestQueue.ResponseMaxSizeLimit = value;
        }

        #endregion

        #region Publics

        public void SetHttpRequestQueueCounters(IHttpRequestQueueCounters counters)
        {
            this.httpRequestQueue.SetCounters(counters);
        }

        public void EnqueueRequest(string clientQueryStringParameters, byte[] postData, string contentType, Action<AsyncHttpResponse, IClientAuthenticationQueue> callback, object state, bool checkUrl = true)
        {
            this.fiber.Enqueue(() => this.ExecuteRequest(clientQueryStringParameters, postData, contentType, callback, state, checkUrl));
        }

        public void EnqueueRequestWithExpectedStatusCodes(HttpWebRequest webRequest, byte[] postData, Action<AsyncHttpResponse, IClientAuthenticationQueue> callback, object state, List<HttpStatusCode> expectedStatusCodes)
        {
            this.fiber.Enqueue(() => this.ExecuteRequestWithExpectedStatusCodes(webRequest, postData, callback, state, expectedStatusCodes));
        }

        #endregion

        #region .privates

        /// <summary>
        /// we check whether string is http or https url
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private static bool IsValidUrl(string url)
        {
            return System.Uri.TryCreate(url, UriKind.Absolute, out var uriResult)
                   && (uriResult.Scheme == System.Uri.UriSchemeHttp || uriResult.Scheme == System.Uri.UriSchemeHttps);
        }

        private void ExecuteRequest(string clientAuthenticationRequestUrl, byte[] postData, string contentType, Action<AsyncHttpResponse, ClientAuthenticationQueue> callback, object state, bool checkUrl = true)
        {
            try
            {
                if (checkUrl && !IsValidUrl(clientAuthenticationRequestUrl))
                {
                    var message =
                        $"CustomAuth Wrong Url was used to create request for ClientAuthenticationQueue. url='{clientAuthenticationRequestUrl}'";
                    log.Error(this.execRequestLogGuard, message);
                    ThreadPool.QueueUserWorkItem(delegate { callback(new AsyncHttpResponse(HttpRequestQueueResultCode.Error, this.RejectIfUnavailable, state), this); });
                    return;
                }

                var webRequest = (HttpWebRequest)WebRequest.Create(clientAuthenticationRequestUrl);
                webRequest.Proxy = null;
                webRequest.Timeout = this.requestTimeoutMilliseconds;
                webRequest.ContentType = contentType;

                HttpRequestQueueCallback queueCallback =
                    (result, httpRequest, userState) =>
                        this.fiber.Enqueue(() => this.OnCallback(result, httpRequest, userState, callback));


                if (postData != null)
                {
                    webRequest.Method = "POST";
                    this.httpRequestQueue.Enqueue(webRequest, postData, queueCallback, state);
                }
                else
                {
                    webRequest.Method = "GET";
                    this.httpRequestQueue.Enqueue(webRequest, queueCallback, state);
                }
            }
            catch (Exception ex)
            {
                var message = $"CustomAuth Exception ExecuteRequest to url '{clientAuthenticationRequestUrl}'. Msg:{ex.Message}";
                log.Error(this.execRequestLogGuard, message, ex);
                ThreadPool.QueueUserWorkItem(delegate { callback(new AsyncHttpResponse(HttpRequestQueueResultCode.Error, this.RejectIfUnavailable, state), this); });
            }
        }

        //added for Viveport, the HttpWebRequest requires additional configuration
        private void ExecuteRequestWithExpectedStatusCodes(HttpWebRequest webRequest, byte[] postData, Action<AsyncHttpResponse, ClientAuthenticationQueue> callback, object state, List<HttpStatusCode> expectedStatusCodes)
        {
            try
            {
                webRequest.Proxy = null;
                webRequest.Timeout = this.requestTimeoutMilliseconds;

                HttpRequestQueueCallback queueCallback =
                    (result, httpRequest, userState) =>
                        this.fiber.Enqueue(() => this.OnCallbackReturnExpectedStatusCodes(result, httpRequest, userState, callback, expectedStatusCodes));


                if (postData != null)
                {
                    webRequest.Method = "POST";
                    this.httpRequestQueue.Enqueue(webRequest, postData, queueCallback, state);
                }
                else
                {
                    webRequest.Method = "GET";
                    this.httpRequestQueue.Enqueue(webRequest, queueCallback, state);
                }
            }
            catch (Exception ex)
            {
                var message = $"CustomAuth Exception during request to url '{webRequest.RequestUri}'. Msg:{ex.Message}";
                log.Error(message, ex);
                ThreadPool.QueueUserWorkItem(delegate { callback(new AsyncHttpResponse(HttpRequestQueueResultCode.Error, this.RejectIfUnavailable, state), this); });
            }
        }

        private void OnCallback(HttpRequestQueueResultCode resultCode,
            AsyncHttpRequest result, object userState, Action<AsyncHttpResponse, ClientAuthenticationQueue> userCallback)
        {
            try
            {
                var url = result.WebRequest.RequestUri;
                byte[] responseData = result.Response;
                var status = result.Status;
                var exception = result.Exception;
                var webResponse = result.WebResponse;

                this.RequestTimeCounter.AddValue((int) result.Elapsedtime.TotalMilliseconds);

                result.Dispose();

                byte[] resultResponseData = null;
                switch (resultCode)
                {
                    case HttpRequestQueueResultCode.Success:
                    {
                        if (log.IsDebugEnabled)
                        {
                            var responseString = string.Empty;
                            if (responseData != null)
                            {
                                responseString = Encoding.UTF8.GetString(responseData);
                            }

                            log.DebugFormat(
                                "CustomAuth result: uri={0}, status={1}, msg={2}, data={3}",
                                url,
                                status,
                                exception?.Message,
                                responseString);
                        }

                        this.timeoutCounter.AddValue(0);
                        resultResponseData = responseData;
                    }
                        break;
                    case HttpRequestQueueResultCode.RequestTimeout:
                    {
                        if (log.IsDebugEnabled)
                        {
                            log.DebugFormat("CustomAuth timed out: uri={0}, status={1}, msg={2}",
                                url, status, exception?.Message);
                        }

                        this.timeoutCounter.AddValue(1);
                    }
                        break;
                    case HttpRequestQueueResultCode.QueueFull:
                    {
                        if (log.IsDebugEnabled)
                        {
                            log.DebugFormat(
                                "CustomAuth error: queue is full. Requests count {0}, url:{1}, msg:{2}",
                                this.httpRequestQueue.QueuedRequestCount, url,
                                exception?.Message);
                        }
                    }
                        break;
                    case HttpRequestQueueResultCode.QueueTimeout:
                        if (log.IsDebugEnabled)
                        {
                            log.DebugFormat(
                                "CustomAuth error: Queue timed out. uri={0}, status={1}, msg={2}",
                                url, status, exception?.Message);
                        }

                        break;
                    case HttpRequestQueueResultCode.Error:
                    {
                        if (log.IsDebugEnabled)
                        {
                            log.DebugFormat("CustomAuth error: uri={0}, status={1}, msg={2}",
                                url, status, exception?.Message);
                        }

                        switch (result.WebStatus)
                        {
                            //most likely minimum required TLS version not available
                            case WebExceptionStatus.SecureChannelFailure:
                                resultResponseData = this.HandleSecureChannelFailure(exception);
                                break;
                            case WebExceptionStatus.ProtocolError:
                                resultResponseData = this.HandleProtocolError(webResponse, exception);
                                break;
                            case WebExceptionStatus.UnknownError:
                                resultResponseData = this.HandleUnknownError(exception);
                                break;
                            default:
                                resultResponseData = responseData;
                                break;
                        }

                        break;
                    }
                    case HttpRequestQueueResultCode.Offline:
                    {
                        if (log.IsDebugEnabled)
                        {
                            log.DebugFormat("CustomAuth error. Queue is offline. url:{0}, status{1}, msg:{2}",
                                url, status, exception?.Message);
                        }
                    }
                        break;
                }

                var response = new AsyncHttpResponse(resultCode, this.RejectIfUnavailable, userState)
                {
                    ResponseData = resultResponseData,
                    ElapsedTicks = result.ElapsedTicks,
                };

                ThreadPool.QueueUserWorkItem(delegate { userCallback(response, this); });
            }
            catch (Exception e)
            {
                log.Error(e);
                ThreadPool.QueueUserWorkItem(delegate { userCallback(new AsyncHttpResponse(HttpRequestQueueResultCode.Error, this.RejectIfUnavailable, userState), this); });
            }
        }

        private byte[] HandleUnknownError(Exception exception)
        {
            if (!(exception is AsyncRequestHttpException httpException))
            {
                return null;
            }

            var customAuthenticationResult = new CustomAuthenticationResult
            {
                ResultCode = CustomAuthenticationResultCode.Failed,
                Message = httpException.UserMessage
            };
            return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(customAuthenticationResult));

        }

        private byte[] HandleSecureChannelFailure(Exception exception)
        {
            //workaround as we can't pass anything else back, use CustomAuthenticationResult
            var customAuthenticationResult = new CustomAuthenticationResult
            {
                ResultCode = this.RejectIfUnavailable ? CustomAuthenticationResultCode.Failed : CustomAuthenticationResultCode.Ok,
                Message = exception?.Message, //"The request was aborted: Could not create SSL/TLS secure channel."
            };

            return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(customAuthenticationResult));
        }

        private byte[] HandleProtocolError(HttpWebResponse webResponse, Exception exception)
        {
            byte[] result = null;
            if (webResponse != null)
            {
                switch (webResponse.StatusCode)
                {
                    case HttpStatusCode.ServiceUnavailable:
                    {
                        //workaround as we can't pass anything else back, use CustomAuthenticationResult
                        var customAuthenticationResult = new CustomAuthenticationResult
                        {
                            ResultCode = this.RejectIfUnavailable ? CustomAuthenticationResultCode.Failed : CustomAuthenticationResultCode.Ok,
                            Message = exception?.Message
                        };
                        result = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(customAuthenticationResult));
                        break;
                    }
                    default:
                    {
                        //workaround as we can't pass anything else back, use CustomAuthenticationResult
                        var customAuthenticationResult = new CustomAuthenticationResult
                        {
                            ResultCode = CustomAuthenticationResultCode.Failed,
                            Message = exception?.Message
                        };
                        result = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(customAuthenticationResult));
                        break;
                    }
                }
            }

            return result;
        }

        //added for Viveport, they use 4XX status codes when token expires
        private void OnCallbackReturnExpectedStatusCodes(HttpRequestQueueResultCode resultCode, AsyncHttpRequest result, object userState, Action<AsyncHttpResponse, ClientAuthenticationQueue> userCallback, List<HttpStatusCode> expectedStatusCodes)
        {
            try
            {
                var url = result.WebRequest.RequestUri;
                byte[] responseData = null;
                var status = result.Status;
                var exception = result.Exception;

                this.RequestTimeCounter.AddValue((int)result.Elapsedtime.TotalMilliseconds);
                if (result.Response != null)
                {
                    responseData = result.Response;
                }

                if (resultCode == HttpRequestQueueResultCode.Error && expectedStatusCodes != null && expectedStatusCodes.Any(expectedStatusCode => expectedStatusCode == result.WebResponse.StatusCode))
                {
                    resultCode = HttpRequestQueueResultCode.Success;
                    responseData = Encoding.UTF8.GetBytes($"{(int) result.WebResponse.StatusCode}/{result.WebResponse.StatusDescription}");
                }

                result.Dispose();

                byte[] resultResponseData = null;
                switch (resultCode)
                {
                    case HttpRequestQueueResultCode.Success:
                        {
                            if (log.IsDebugEnabled)
                            {
                                var responseString = string.Empty;
                                if (responseData != null)
                                {
                                    responseString = Encoding.UTF8.GetString(responseData);
                                }

                                log.DebugFormat(
                                    "CustomAuth result: uri={0}, status={1}, msg={2}, data={3}",
                                    url,
                                    status,
                                    exception?.Message,
                                    responseString);
                            }

                            this.timeoutCounter.AddValue(0);
                            resultResponseData = responseData;
                        }
                        break;
                    case HttpRequestQueueResultCode.RequestTimeout:
                        {
                            if (log.IsDebugEnabled)
                            {
                                log.DebugFormat("CustomAuth timed out: uri={0}, status={1}, msg={2}",
                                    url, status, exception?.Message);
                            }
                            this.timeoutCounter.AddValue(1);
                        }
                        break;
                    case HttpRequestQueueResultCode.QueueFull:
                        {
                            if (log.IsDebugEnabled)
                            {
                                log.DebugFormat(
                                    "CustomAuth error: queue is full. Requests count {0}, url:{1}, msg:{2}",
                                    this.httpRequestQueue.QueuedRequestCount, url,
                                    exception?.Message);
                            }
                        }
                        break;
                    case HttpRequestQueueResultCode.QueueTimeout:
                        if (log.IsDebugEnabled)
                        {
                            log.DebugFormat("CustomAuth error: Queue timed out. uri={0}, status={1}, msg={2}",
                                url, status, exception?.Message);
                        }
                        break;
                    case HttpRequestQueueResultCode.Error:
                        {
                            if (log.IsDebugEnabled)
                            {
                                log.DebugFormat("CustomAuth error: uri={0}, status={1}, msg={2}",
                                    url, status, exception?.Message);
                            }
                        }
                        break;
                    case HttpRequestQueueResultCode.Offline:
                        {
                            if (log.IsDebugEnabled)
                            {
                                log.DebugFormat("CustomAuth error. Queue is offline. url:{0}, status{1}, msg:{2}",
                                    url, status, exception?.Message);
                            }
                        }
                        break;
                }

                var response = new AsyncHttpResponse(resultCode, this.RejectIfUnavailable, userState)
                {
                    ResponseData = resultResponseData,
                    ElapsedTicks = result.ElapsedTicks,
                };

                ThreadPool.QueueUserWorkItem(delegate { userCallback(response, this); });
            }
            catch (Exception e)
            {
                log.Error(e);
                ThreadPool.QueueUserWorkItem(delegate { userCallback(new AsyncHttpResponse(HttpRequestQueueResultCode.Error, this.RejectIfUnavailable, userState), this); });
            }
        }

        #endregion

        public class RoundRobinCounter
        {
            private readonly int[] values;
            private int sum;
            private int pos;
            private int count;

            public RoundRobinCounter(int size)
            {
                this.values = new int[size];
            }

            public int Sum
            {
                get { return this.sum; }
            }

            public int Average
            {
                get { return this.Sum / (this.count > 0 ? this.count : 1); }
            }

            public void AddValue(int v)
            {
                if (this.count < this.values.Length)
                {
                    this.count++;
                }

                this.sum -= this.values[this.pos];
                this.sum += v;
                this.values[this.pos] = v;
                this.pos = this.pos + 1;

                if (this.pos >= this.values.Length)
                {
                    this.pos = 0;
                }
            }
        }
    }
}
