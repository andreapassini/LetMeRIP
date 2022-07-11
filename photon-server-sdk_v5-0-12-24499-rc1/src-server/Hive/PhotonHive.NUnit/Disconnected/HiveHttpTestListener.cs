using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using ExitGames.Logging;
using Newtonsoft.Json;

namespace Photon.Hive.Tests.Disconnected
{
    public class HiveHttpTestListener : IDisposable
    {
        private readonly HttpListener listener;

        private static readonly ILogger log = LogManager.GetCurrentClassLogger();


        private static readonly byte[] defaultResponseData = System.Text.Encoding.UTF8.GetBytes("Hello");

        public bool IsRunning { get; private set; }

        public bool IsDisposed { get; private set; }

        public Uri Url { get; }

        public HiveHttpTestListener(bool start = true)
        {

            int port = 55557;

            var uri = $"http://localhost:{port}";
            this.Url = new Uri(uri);

            this.listener = new HttpListener();
            this.listener.Prefixes.Add(this.Url.ToString());

            if (start)
            {
                this.Start();
            }
        }

        public HiveHttpTestListener(Uri uriPrefix)
        {
            this.Url = uriPrefix;
            this.listener = new HttpListener();
            this.listener.Prefixes.Add(uriPrefix.ToString());
        }

        public void Start()
        {
            lock (this.listener)
            {
                if (this.IsDisposed)
                {
                    throw new ObjectDisposedException(nameof(HiveHttpTestListener));
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
                if (this.IsDisposed)
                {
                    throw new ObjectDisposedException(nameof(HiveHttpTestListener));
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
                await this.HandleRequest(contextTask.Result);
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

                if (request.RawUrl.Contains("webrpc") && !request.RawUrl.Contains("nonExisting"))
                {
                    var data = Encoding.UTF8.GetBytes(
                        "{\"ResultCode\":123, \"Message\":\"Hello World\", \"Data\": {\"str1\":\"value1\",\"str2\": 2,\"str3\": [1,2,3]}}");
                    context.Response.OutputStream.Write(data, 0, data.Length);
                }
                else
                {

                    context.Response.StatusCode = (int) HttpStatusCode.NotFound;

                    var responseData = GetResponseData(request);
                    if (log.IsDebugEnabled)
                    {
                        log.Debug("sending...");
                    }

                    context.Response.OutputStream.Write(responseData, 0, responseData.Length);
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