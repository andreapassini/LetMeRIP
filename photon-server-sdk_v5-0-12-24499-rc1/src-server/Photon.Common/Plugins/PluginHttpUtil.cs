using ExitGames.Concurrency.Fibers;
using ExitGames.Logging;
using Photon.Plugins.Common;
using Photon.SocketServer.Net;
using System;
using System.Collections.Specialized;
using System.Net;
using System.Text;

namespace Photon.Common.Plugins
{
    public interface IPluginHttpUtilClient
    {
        IExtendedFiber ExecutionFiber { get; }
        ILogger Log { get; }
        HttpRequestQueue HttpRequestQueue { get; }
        int HttpQueueRequestTimeout { get;  }

        void ResumeClient(Action resumeAction);
        void SuspendClient(int timeout, Action timeoutAction);
        void PluginReportError(short errorCode, Exception ex, object state);
    }

    public static class PluginHttpUtil
    {
        public static void HttpRequest(IPluginHttpUtilClient client, HttpRequest request, ICallInfo info)
        {
            if (request.Callback == null)
            {
                var url = request.Url;
                client.Log.Debug("HttpRequest Callback is not set. Using default to log in case of error. " + url);

                request.Callback = (response, state) =>
                {
                    if (response.Status != HttpRequestQueueResult.Success)
                    {
                        client.Log.Warn(
                            string.Format(
                                "Request to '{0}' failed. reason={1}, httpcode={2} webstatus={3}, HttpQueueResult={4}.",
                                url,
                                response.Reason,
                                response.HttpCode,
                                response.WebStatus,
                                response.Status));
                    }
                };
            }

            var callInfo = (ICallInfoImpl)info;

            const int RequestRetryCount = 3;
            if (request.Async)
            {
                callInfo.InternalDefer();
            }
            else
            {
                var timeout = (int)client.HttpRequestQueue.QueueTimeout.TotalMilliseconds +
                    (RequestRetryCount + 1) * client.HttpQueueRequestTimeout + 5000;

                Action timeoutAction = () =>
                {
                    client.Log.ErrorFormat("Game did not resumed after {0} ms. http call to {1}. Headers: {2}, CustomHeaders:{3}, " +
                                    "Accept:{4}, ContentType:{5}, Method:{6}",
                        timeout, request.Url,
                        request.Headers != null ? Newtonsoft.Json.JsonConvert.SerializeObject(request.Headers) : "<null>",
                        request.CustomHeaders != null ? Newtonsoft.Json.JsonConvert.SerializeObject(request.CustomHeaders) : "<null>",
                        request.Accept, request.ContentType, request.Method);

                    var response = new HttpResponseImpl(
                        request,
                        info,
                        HttpRequestQueueResultCode.RequestTimeout,
                        string.Empty,
                        (int)WebExceptionStatus.Timeout);
                    request.Callback(response, request.UserState);
                };

                client.SuspendClient(timeout, timeoutAction);
                callInfo.Pause();
            }

            try
            {
                var webRequest = (HttpWebRequest)WebRequest.Create(request.Url);
                webRequest.Proxy = null;
                webRequest.Method = request.Method ?? "GET";
                webRequest.ContentType = request.ContentType;
                webRequest.Accept = request.Accept;
                webRequest.Timeout = client.HttpQueueRequestTimeout;

                if (request.CustomHeaders != null)
                {
                    AddCustomHttpHeaders(client, request, webRequest);
                }

                if (request.Headers != null)
                {
                    AddPredefinedHttpHeaders(client, request, webRequest);
                }

                HttpRequestQueueCallback callback = (result, httpRequest, state) =>
                {
                    if (client.Log.IsDebugEnabled)
                    {
                        client.Log.DebugFormat("callback for request is called.url:{0}.IsAsync:{1}",
                            httpRequest.WebRequest.RequestUri, request.Async);
                    }

                    if (request.Async)
                    {
                        client.ExecutionFiber.Enqueue(() => HttpRequestCallback(client, request, result, httpRequest, state, info));
                    }
                    else
                    {
                        client.ResumeClient(() => HttpRequestCallback(client, request, result, httpRequest, state, info));
                    }
                };

                EnqueuWebRequest(client.HttpRequestQueue, request, RequestRetryCount, webRequest, callback);
            }
            catch (WebException e)
            {
                client.Log.Error(string.Format("Exception calling Url:{0}", request.Url), e);
                var response = new HttpResponseImpl(request, info, HttpRequestQueueResultCode.Error, e.Message, (int)e.Status);
                request.Callback(response, request.UserState);
            }
            catch (Exception ex)
            {
                client.ExecutionFiber.Resume();

                client.Log.Error(string.Format("Exception calling Url:{0}", request.Url), ex);
                var response = new HttpResponseImpl(
                    request,
                    info,
                    HttpRequestQueueResultCode.Error,
                    ex.Message,
                    (int)WebExceptionStatus.UnknownError);
                request.Callback(response, request.UserState);
            }
        }

        private static void EnqueuWebRequest(HttpRequestQueue queue, HttpRequest request, int RequestRetryCount, HttpWebRequest webRequest, HttpRequestQueueCallback callback)
        {
            switch (webRequest.Method)
            {
                case "GET":
                case "TRACE":
                case "HEAD":
                    queue.Enqueue(webRequest, callback, request.UserState, RequestRetryCount);
                    return;
            }
            if (request.DataStream != null)
            {
                queue.Enqueue(webRequest, request.DataStream.ToArray(), callback, request.UserState, RequestRetryCount);
            }
            else
            {
                queue.Enqueue(webRequest, callback, request.UserState, RequestRetryCount);
            }
        }

        private static void AddPredefinedHttpHeaders(IPluginHttpUtilClient client, HttpRequest request, HttpWebRequest webRequest)
        {
            foreach (var kv in request.Headers)
            {
                try
                {
                    if (!ApplyRestrictedHeader(client, kv.Key, kv.Value, webRequest))
                    {
                        webRequest.Headers.Add(kv.Key, kv.Value);
                    }
                }
                catch (Exception e)
                {
                    client.Log.Warn(string.Format("Header add exception:'{0}' with value '{1}'. Game:{2}. Excpetion Msg:{3}",
                        kv.Key, kv.Value, client, e.Message), e);
                }
            }
        }

        private static bool ApplyRestrictedHeader(IPluginHttpUtilClient client, HttpRequestHeader key, string value, HttpWebRequest webRequest)
        {
            switch (key)
            {
                case HttpRequestHeader.Accept:
                    webRequest.Accept = value;
                    break;
                case HttpRequestHeader.ContentType:
                    webRequest.ContentType = value;
                    break;
                case HttpRequestHeader.Date:
                    webRequest.Date = DateTime.Parse(value);
                    break;
                case HttpRequestHeader.Expect:
                    webRequest.Expect = value;
                    break;
                case HttpRequestHeader.IfModifiedSince:
                    webRequest.IfModifiedSince = DateTime.Parse(value);
                    break;
                case HttpRequestHeader.Referer:
                    webRequest.Referer = value;
                    break;
                case HttpRequestHeader.UserAgent:
                    webRequest.UserAgent = value;
                    break;
                case HttpRequestHeader.TransferEncoding:
                    webRequest.SendChunked = true;
                    webRequest.TransferEncoding = value;
                    break;
                case HttpRequestHeader.Host:
                case HttpRequestHeader.Range:
                case HttpRequestHeader.Connection:
                case HttpRequestHeader.ContentLength:
                    // "Proxy-Connection" is restricted but does not have enum value
                    client.Log.WarnFormat("Usage of restricted http header :'{0}' with value '{1}'. Context:{2}. ", key, value, client);
                    return true;
                default:
                    return false;
            }
            return true;
        }

        private static bool ApplyRestrictedHeader(IPluginHttpUtilClient client, string key, string value, HttpWebRequest webRequest)
        {
            switch (key)
            {
                case "Accept":
                    webRequest.Accept = value;
                    break;
                case "Date":
                    webRequest.Date = DateTime.Parse(value);
                    break;
                case "Expect":
                    webRequest.Expect = value;
                    break;
                case "Referer":
                    webRequest.Referer = value;
                    break;
                case "If-Modified-Since":
                    webRequest.IfModifiedSince = DateTime.Parse(value);
                    break;
                case "Content-Type":
                    webRequest.ContentType = value;
                    break;
                case "User-Agent":
                    webRequest.UserAgent = value;
                    break;
                case "Transfer-Encoding":
                    webRequest.SendChunked = true;
                    webRequest.TransferEncoding = value;
                    break;
                //case "Host":
                //case "Range":
                //case "Connection":
                //case "Content-Length":
                //case "Proxy-Connection":
                default:
                    if (WebHeaderCollection.IsRestricted(key))
                    {
                        client.Log.WarnFormat("Usage of restricted http header :'{0}' with value '{1}'. Context:{2}. ", key, value, client);
                        break;
                    }
                    return false;
            }
            return true;
        }

        private static void AddCustomHttpHeaders(IPluginHttpUtilClient client, HttpRequest request, HttpWebRequest webRequest)
        {
            foreach (var kv in request.CustomHeaders)
            {
                try
                {
                    if (!ApplyRestrictedHeader(client, kv.Key, kv.Value, webRequest))
                    {
                        webRequest.Headers.Add(kv.Key, kv.Value);
                    }
                }
                catch (Exception e)
                {
                    client.Log.Warn(string.Format("Custom header add exception:'{0}' with value '{1}'. Game:{2}. Exception Msg:{3}",
                        kv.Key, kv.Value, client, e.Message), e);
                }
            }
        }

        private static void HttpRequestHttpCallback(IPluginHttpUtilClient client, HttpRequest request, ICallInfo info,
            HttpRequestQueueResultCode result, AsyncHttpRequest asyncHttpRequest, object state)
        {

            var response = HttpResponseImpl.CreateHttpResponse(request, info, result, asyncHttpRequest);

            client.Log.Debug("Got HttpResoonse - executing callback.");


            // Sync request triggers an event to release the waiting Request thread to continue
            try
            {
                request.Callback(response, state);
            }
            catch (Exception ex)
            {
                client.Log.Error(ex);
                if (request.Async)
                {
                    client.Log.Error(ex);
                    client.PluginReportError(ErrorCodes.AsyncCallbackException, ex, state);
                }
                else
                {
                    throw;
                }
            }
        }

        private static void HttpRequestCallback(IPluginHttpUtilClient client, HttpRequest request, HttpRequestQueueResultCode result, AsyncHttpRequest httpRequest, object state, ICallInfo info)
        {
            var callInfo = (ICallInfoImpl)info;

            var doCheck = !callInfo.IsProcessed;
            if (doCheck)
            {
                callInfo.Reset();
            }

            var response = HttpResponseImpl.CreateHttpResponse(request, info, result, httpRequest);

            client.Log.Debug("Got HttpResponse - executing callback.");

            try
            {
                request.Callback(response, state);

                string errorMsg;
                // and check that one of methods was called
                if (doCheck && !info.StrictModeCheck(out errorMsg))
                {
                    var infoTypeName = info.GetType().ToString();
                    client.PluginReportError(ErrorCodes.MissingCallProcessing, null, infoTypeName);
                    info.Fail(string.Format("HttpRequestCallback: {0}", errorMsg));
                }
            }
            catch (Exception ex)
            {
                client.Log.Error(ex);
                client.PluginReportError(ErrorCodes.AsyncCallbackException, ex, state);
                if (!callInfo.IsProcessed)
                {
                    callInfo.Fail(ex.ToString());
                }
            }
        }
    }

    public class HttpResponseImpl : IHttpResponse
    {
        #region Constants and Fields

        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private readonly int httpCode;

        private readonly string reason;

        private readonly byte[] responseData;

        private readonly string responseText;

        private readonly byte status;

        private readonly int webStatus;

        #endregion

        #region Constructors and Destructors

        public HttpResponseImpl(HttpRequest request, ICallInfo info, byte[] responseData,
            HttpRequestQueueResultCode status, int httpCode, string reason, int webStatus, NameValueCollection headers)
        {
            this.Request = request;
            this.responseData = responseData;
            this.responseText = responseData == null ? null : Encoding.UTF8.GetString(responseData, 0, responseData.Length);
            this.status = (byte)status;
            this.httpCode = httpCode;
            this.reason = reason;
            this.webStatus = webStatus;
            this.CallInfo = info;
            this.Headers = headers;
        }

        public HttpResponseImpl(HttpRequest request, ICallInfo info, HttpRequestQueueResultCode status, string reason, int webStatus)
            : this(request, info, null, status, 0, reason, webStatus, null)
        {
        }

        #endregion

        #region Properties

        public HttpRequest Request { get; private set; }

        public int HttpCode
        {
            get
            {
                return this.httpCode;
            }
        }

        public string Reason
        {
            get
            {
                return this.reason;
            }
        }

        public byte[] ResponseData
        {
            get
            {
                return this.responseData;
            }
        }

        public string ResponseText
        {
            get
            {
                return this.responseText;
            }
        }

        public byte Status
        {
            get
            {
                return this.status;
            }
        }

        public int WebStatus
        {
            get
            {
                return this.webStatus;
            }
        }

        public ICallInfo CallInfo { get; private set; }
        public NameValueCollection Headers { get; private set; }

        #endregion

        #region Publics

        public static HttpResponseImpl CreateHttpResponse(HttpRequest request, ICallInfo info,
            HttpRequestQueueResultCode result, AsyncHttpRequest asyncHttpRequest)
        {
            var statusCode = -1;
            string statusDescription;
            byte[] responseData = null;
            NameValueCollection headers = null;
            try
            {
                switch (result)
                {
                    case HttpRequestQueueResultCode.Success:
                        statusCode = (int)asyncHttpRequest.WebResponse.StatusCode;
                        statusDescription = asyncHttpRequest.WebResponse.StatusDescription;
                        responseData = asyncHttpRequest.Response;
                        headers = asyncHttpRequest.WebResponse.Headers;
                        break;

                    case HttpRequestQueueResultCode.Error:
                        if (asyncHttpRequest.WebResponse != null)
                        {
                            statusCode = (int)asyncHttpRequest.WebResponse.StatusCode;
                            statusDescription = asyncHttpRequest.WebResponse.StatusDescription;
                            headers = asyncHttpRequest.WebResponse.Headers;
                        }
                        else
                        {
                            statusDescription = asyncHttpRequest.Exception.Message;
                        }

                        break;
                    default:
                        statusCode = -1;
                        statusDescription = String.Empty;
                        break;
                }
            }
            catch (Exception ex)
            {
                // we should never get her
                statusDescription = ex.Message;

                log.Warn("Exception during http response creation", ex);
            }

            var webStatus = asyncHttpRequest == null ? -1 : (int)asyncHttpRequest.WebStatus;
            if (asyncHttpRequest != null)
            {
                asyncHttpRequest.Dispose();
            }

            return new HttpResponseImpl(request, info, responseData, result, statusCode, statusDescription, webStatus, headers);
        }

        #endregion
    }

}
