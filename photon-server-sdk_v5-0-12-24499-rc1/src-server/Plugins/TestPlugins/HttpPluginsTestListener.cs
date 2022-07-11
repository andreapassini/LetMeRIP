using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Photon.Hive.Plugin;
using Photon.SocketServer.NUnit.Utils.Http;
using Photon.Hive.Plugin.WebHooks;

namespace TestPlugins
{
    public class HttpPluginsTestListener : IDisposable
    {
        private readonly HttpListener listener;

        private readonly IPluginLogger log;

        private static readonly byte[] defaultResponseData = System.Text.Encoding.UTF8.GetBytes("Hello");

        private readonly Dictionary<string, SerializableGameState> games =
            new Dictionary<string, SerializableGameState>();

        public bool IsRunning { get; private set; }

        public bool IsDisposed { get; private set; }

        public Uri Url { get; }

        public HttpPluginsTestListener(IPluginLogger logger, bool start = true)
        {
            this.log = logger;

            int port = 55557;
            if (!PortManager.IsPortFree(port))
            {
                port = PortManager.TakePort();
            }

            var uri = $"http://localhost:{port}";
            this.Url = new Uri(uri);

            this.listener = new HttpListener();
            this.listener.Prefixes.Add(this.Url.ToString());

            if (start)
            {
                this.Start();
            }
        }

        public HttpPluginsTestListener(Uri uriPrefix)
        {
            this.Url = uriPrefix;
            this.listener = new HttpListener();
            listener.Prefixes.Add(uriPrefix.ToString());
        }

        public void Start()
        {
            lock (this.listener)
            {
                if (this.IsDisposed)
                {
                    throw new ObjectDisposedException(nameof(HttpPluginsTestListener));
                }

                if (this.IsRunning)
                {
                    return;
                }

                this.listener.Start();
                this.IsRunning = true;
                this.listener.GetContextAsync().ContinueWith(this.HandleRequest);
            }
        }

        public void Stop()
        {
            lock (this.listener)
            {
                if (IsDisposed)
                {
                    throw new ObjectDisposedException(nameof(HttpPluginsTestListener));
                }

                this.listener.Stop();
                this.IsRunning = false;
            }
        }

        public void Dispose()
        {
            lock (this.listener)
            {
                if (this.IsDisposed)
                {
                    return;
                }

                this.Stop();
                this.IsDisposed = true;

                this.listener.Abort();
            }
        }

        public string GetStatusRequest(HttpStatusCode statusCode)
        {
            return this.Url + $"?statusCode={statusCode}";
        }

        public string GetTimeoutRequest()
        {
            return this.Url + "?timeout=true";
        }

        public string GetTooBigResponse()
        {
            return this.Url + "?toobig=1000";
        }

        public string GetTooBigResponseChunked()
        {
            return this.Url + "?chunksending=1000";
        }

        public string GetWebHooks12Url()
        {
            return this.Url + "realtime-webhooks-1.2";
        }

        private async void HandleRequest(Task<HttpListenerContext> contextTask)
        {
            if (contextTask.IsCanceled)
            {
                if (log.IsDebugEnabled)
                {
                    log.Debug("Task canceled");
                }

                return;
            }

            if (contextTask.IsFaulted && contextTask.Exception?.InnerException is ObjectDisposedException)
            {
                log.Debug("Task faulted with ObjectDisposedException");
                return;
            }

            try
            {
                _ = this.listener.GetContextAsync()
                    .ContinueWith(this.HandleRequest, TaskContinuationOptions.OnlyOnRanToCompletion);
            }
            catch (ObjectDisposedException)
            {
            }

            if (!contextTask.IsFaulted)
            {
                await HandleRequest(contextTask.Result);
            }
        }

        private async Task HandleRequest(HttpListenerContext context)
        {
            try
            {
                if (log.IsDebugEnabled)
                {
                    log.Debug("Handling of the request");
                }

                var request = context.Request;

                if (request.RawUrl.Contains("realtime-webhooks-1.2"))
                {
                    if (request.RawUrl.Contains("WrongUriPath"))
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    }
                    else if (request.RawUrl.Contains("GetGameList"))
                    {
                        var data = Encoding.UTF8.GetBytes(
                            "{\"ResultCode\":0, \"Message\":\"\" }");
                        context.Response.OutputStream.Write(data, 0, data.Length);
                    }
                    else
                    {
                        this.HandleWebhooksRequest(context);
                    }
                }
                else
                {
                    if (((IList) request.QueryString.AllKeys).Contains("timeout"))
                    {
                        return;
                    }

                    var delayParam = request.QueryString["delay"];
                    if (delayParam != null && int.TryParse(delayParam, out int delay))
                    {
                        if (log.IsDebugEnabled)
                        {
                            log.Debug("delay before answer");
                        }

                        await Task.Delay(TimeSpan.FromMilliseconds(delay));
                    }

                    context.Response.StatusCode = (int) GetStatusCode(request);

                    var responseData = GetResponseData(request);
                    if (request.QueryString.AllKeys.Contains(@"chunksending"))
                    {
                        if (log.IsDebugEnabled)
                        {
                            log.Debug("chunk sending");
                        }

                        responseData = new byte[2048];

                        context.Response.SendChunked = true;
                        context.Response.OutputStream.Write(responseData, 0, responseData.Length / 2);
                        context.Response.OutputStream.Flush();

                        await Task.Delay(500);

                        context.Response.OutputStream.Write(responseData, responseData.Length / 2,
                            responseData.Length - responseData.Length / 2);
                        context.Response.OutputStream.Flush();
                    }
                    else if (request.QueryString.AllKeys.Contains(@"toobig"))
                    {
                        responseData = new byte[10000];
                        context.Response.ContentLength64 = responseData.Length;
                        context.Response.OutputStream.Write(responseData, 0, responseData.Length);
                        context.Response.OutputStream.Flush();
                    }
                    else
                    {
                        if (log.IsDebugEnabled)
                        {
                            log.Debug("sending...");
                        }

                        context.Response.OutputStream.Write(responseData, 0, responseData.Length);
                    }
                }

                context.Response.Close();
            }
            catch (Exception e)
            {
                log.Error(e);

                context.Response.StatusCode = (int) HttpStatusCode.InternalServerError;
                context.Response.StatusDescription = e.Message;
                context.Response.Close();
            }
        }

        private void HandleWebhooksRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var dataLen = (int)request.ContentLength64;
            var data = new byte[dataLen];
            request.InputStream.Read(data, 0, dataLen);

            var strData = Encoding.UTF8.GetString(data);

            var webHooksRequest = JsonConvert.DeserializeObject<WebhooksRequest>(strData);

            if (this.log.IsDebugEnabled)
            {
                this.log.Debug($"Got new webhooks request with type_{webHooksRequest.Type}, GameId:{webHooksRequest.GameId}, actorId:{webHooksRequest.UserId}");
            }

            switch (webHooksRequest.Type)
            {
                case "Create":
                case "Save":
                    this.HandleCreateRequest(webHooksRequest, context);
                    break;
                case "Load":
                    this.HandleLoadRequest(webHooksRequest, context);
                    break;
                case "Join":
                    this.HandleGameJoinRequest(webHooksRequest, context);
                    break;
            }
        }

        private void HandleGameJoinRequest(WebhooksRequest webHooksRequest, HttpListenerContext context)
        {
            var response = new WebhooksResponse()
            {
                ResultCode = 0
            };
            var data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response));

            context.Response.OutputStream.Write(data, 0, data.Length);
            context.Response.OutputStream.Flush();

        }

        private void HandleLoadRequest(WebhooksRequest webHooksRequest, HttpListenerContext context)
        {
            var response = new WebhooksResponse()
            {
                ResultCode = 0
            };

            if (this.games.TryGetValue(webHooksRequest.GameId, out var gameState))
            {
                response.State = gameState;
            }
            else
            {
                response.ResultCode = 1;
                response.Message = $"Game '{webHooksRequest.GameId}' not found";
            }
            var data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response));

            context.Response.OutputStream.Write(data, 0, data.Length);
            context.Response.OutputStream.Flush();
        }

        private void HandleCreateRequest(WebhooksRequest webHooksRequest, HttpListenerContext context)
        {
            this.games[webHooksRequest.GameId] = webHooksRequest.State;

            var response = new WebhooksResponse()
            {
                ResultCode = 0
            };
            var data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response));

            context.Response.OutputStream.Write(data, 0, data.Length);
            context.Response.OutputStream.Flush();
        }

        private static HttpStatusCode GetStatusCode(HttpListenerRequest request)
        {
            string codeString = request.QueryString["statusCode"];
            if (string.IsNullOrEmpty(codeString))
                return HttpStatusCode.OK;

            if (int.TryParse(codeString, out var code))
                return (HttpStatusCode) code;

            if (Enum.TryParse<HttpStatusCode>(codeString, true, out var statusCode))
                return statusCode;

            return HttpStatusCode.OK;
        }

        private static byte[] GetResponseData(HttpListenerRequest request)
        {
            if (!request.HasEntityBody)
            {
                return defaultResponseData;
            }

            var result = new byte[request.ContentLength64];
            request.InputStream.Read(result, 0, result.Length);
            return result;
        }

    }
}