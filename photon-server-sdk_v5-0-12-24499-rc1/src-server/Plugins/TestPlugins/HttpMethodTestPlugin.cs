using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using Photon.Hive.Plugin;

namespace TestPlugins
{
    class HttpMethodTestPlugin : TestPluginBase
    {
        private const string ServerUrl = "http://localhost:53001";
        HttpListener httpListener;

        private byte[] postData = Encoding.UTF8.GetBytes("post data");
        private byte[] putData = Encoding.UTF8.GetBytes("put data");
        private byte[] optionsData = Encoding.UTF8.GetBytes("options data");
        private byte[] deleteData = Encoding.UTF8.GetBytes("delete data");

        public HttpMethodTestPlugin()
            : base()
        {
            this.httpListener = new HttpListener();

            this.httpListener.Prefixes.Add(ServerUrl + "/");
            this.httpListener.Start();

            this.httpListener.BeginGetContext(this.HttpListerCallback, null);
        }

        public override void OnCloseGame(ICloseGameCallInfo info)
        {
            this.httpListener.Stop();

            base.OnCloseGame(info);
        }
        public override void OnRaiseEvent(IRaiseEventCallInfo info)
        {
            switch (info.Request.EvCode)
            {
                case 1:
                    this.UseNewHttpMode(info);
                    break;
                case 2:
                    this.UseOldHttpMode(info);
                    break;
            }
            base.OnRaiseEvent(info);
        }

        private void UseOldHttpMode(IRaiseEventCallInfo info)
        {
            var request = (Dictionary<string, string>)info.Request.Data;

            this.PluginHost.HttpRequest(GetHttpRequest(request));
        }

        private void UseNewHttpMode(IRaiseEventCallInfo info)
        {
            var request = (Dictionary<string, string>)info.Request.Data;

            this.PluginHost.HttpRequest(GetHttpRequest(request), info);
        }

        private HttpRequest GetHttpRequest(Dictionary<string, string> request)
        {
            HttpRequest httpRequest = new HttpRequest
            {
                Method = request["method"],
                Url = ServerUrl,
                Async = true,
                Callback = HttpCllback
            };

            switch (request["method"])
            {
                case "POST":
                    httpRequest.DataStream = new MemoryStream(this.postData);
                    break;
                case "POST_NO_DATA":
                    httpRequest.Method = "POST";
                    httpRequest.DataStream = new MemoryStream(new byte[0]);
                    break;
                case "PUT":
                    httpRequest.DataStream = new MemoryStream(this.putData);
                    break;
                case "PUT_NO_DATA":
                    httpRequest.Method = "PUT";
                    httpRequest.DataStream = new MemoryStream(new byte[0]);
                    break;
                case "DELETE":
                    break;
                case "DELETE_WITH_DATA":
                    httpRequest.Method = "DELETE";
                    httpRequest.DataStream = new MemoryStream(this.deleteData);
                    break;
                case "OPTIONS":
                    break;
                case "OPTIONS_WITH_DATA":
                    httpRequest.Method = "OPTIONS";
                    httpRequest.DataStream = new MemoryStream(this.optionsData);
                    break;
                case "HEAD":
                case "GET":
                case "TRACE":
                    break;
            }

            return httpRequest;
        }

        private void HttpCllback(IHttpResponse response, object userState)
        {
            if (response.Status != HttpRequestQueueResult.Success)
            {
                this.BroadcastEvent(123, new Dictionary<byte, object>
                {
                    {
                        0,
                        string.Format("HttpRequest failure for method '{0}'. Status:{1}, Reason:{2}", 
                                response.Request.Method, response.Status, response.Reason)
                    }
                });

                return;
            }
            if (response.Request.Method == "HEAD")
            {
                this.BroadcastEvent(123, new Dictionary<byte, object> { { 0, "OK" } });
            }
            else
            {
                this.BroadcastEvent(123, new Dictionary<byte, object> { { 0, response.ResponseText } });
            }
        }

        #region Http Server Methods
        private void HttpListerCallback(IAsyncResult ar)
        {
            if (this.httpListener.IsListening == false)
            {
                return;
            }

            try
            {
                HttpListenerContext context = this.httpListener.EndGetContext(ar);
                this.httpListener.BeginGetContext(this.HttpListerCallback, null);

                switch (context.Request.HttpMethod)
                {
                    case "POST":
                        this.HandleAndRespondPOST(context);
                        break;
                    case "PUT":
                        this.HandleAndRespondPUT(context);
                        break;
                    case "OPTIONS":
                        this.HandleAndRespondOPTIONS(context);
                        break;
                    case "DELETE":
                        this.HandleAndRespondDELETE(context);
                        break;
                    default:
                        this.HandleAndRespondNODATA(context);
                        break;
                }
            }
            catch (Exception e)
            {
                this.PluginHost.LogError($"Exception in HttpListerCallback. Exception={e}");
            }
        }

        private void SendOk(HttpListenerContext context)
        {
            SendResponse(context, "OK");
        }

        private void SendResponse(HttpListenerContext context, string response)
        {
            try
            {
                var responseBuffer = Encoding.UTF8.GetBytes(response);

                context.Response.StatusCode = (int)HttpStatusCode.OK;
                if (context.Request.HttpMethod != "HEAD")
                {
                    context.Response.ContentLength64 = responseBuffer.Length;
                    context.Response.OutputStream.Write(responseBuffer, 0, responseBuffer.Length);
                }
                context.Response.OutputStream.Flush();
                context.Response.OutputStream.Close();
            }
            catch (Exception e)
            {
                this.PluginHost.LogError(string.Format("Exception during sending response for method '{0}'. Exception:{1}",
                    context.Request.HttpMethod, e));
            }
        }

        private void HandleAndRespondBinary(HttpListenerContext context, byte[] expectedData)
        {
            HttpListenerRequest request = context.Request;

            byte[] requestBuffer = new byte[request.ContentLength64];
            request.InputStream.Read(requestBuffer, 0, requestBuffer.Length);

            if (requestBuffer.SequenceEqual(expectedData))
            {
                this.SendOk(context);
            }
            else
            {
                this.SendResponse(context, "Server got wrong data for method " + context.Request.HttpMethod);
            }
        }

        private void HandleAndRespondPOST(HttpListenerContext context)
        {
            this.HandleAndRespondRequestWithOptionlData(context, this.postData);
        }

        private void HandleAndRespondPUT(HttpListenerContext context)
        {
            this.HandleAndRespondRequestWithOptionlData(context, this.putData);
        }

        private void HandleAndRespondOPTIONS(HttpListenerContext context)
        {
            HandleAndRespondRequestWithOptionlData(context, this.optionsData);
        }

        private void HandleAndRespondDELETE(HttpListenerContext context)
        {
            HandleAndRespondRequestWithOptionlData(context, this.deleteData);
        }

        private void HandleAndRespondRequestWithOptionlData(HttpListenerContext context, byte[] expectedData)
        {
            if (context.Request.ContentLength64 != 0)
            {
                this.HandleAndRespondBinary(context, expectedData);
            }
            else
            {
                this.HandleAndRespondNODATA(context);
            }
        }

        private void HandleAndRespondNODATA(HttpListenerContext context)
        {
            this.SendOk(context);
        }

        #endregion
    }
}
