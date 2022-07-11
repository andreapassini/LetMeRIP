// --------------------------------------------------------------------------------------------------------------------
// <copyright file="HiveHostGame.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Collections.Specialized;
using System.Diagnostics;
using Photon.Common;
using Photon.Common.Plugins;
using Photon.Hive.Common;
using Photon.Plugins.Common;

namespace Photon.Hive
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;

    using ExitGames.Concurrency.Fibers;
    using ExitGames.Logging;

    using Photon.Hive.Diagnostics.OperationLogging;
    using Photon.Hive.Events;
    using Photon.Hive.Operations;
    using Photon.Hive.Plugin;
    using Photon.SocketServer;
    using Photon.SocketServer.Net;
    using Photon.SocketServer.Rpc.Protocols;
    using Photon.SocketServer.Diagnostics;
    using SendParameters = Photon.SocketServer.SendParameters;
    using DeliveryMode = Photon.SocketServer.DeliveryMode;


    public enum HiveHostActorState
    {
        ActorNr,
        Binary,
        UserId,
        Nickname,
    }


    public class HiveHostGame : HiveGame, IPluginHost, IHttpRequestQueueCounters, IUnknownTypeMapper, IPluginHttpUtilClient
    {
        #region Constants and Fields
        private static readonly ILogger Log = LogManager.GetCurrentClassLogger();

        private static readonly ILogger LogPlugin = LogManager.GetLogger("Photon.Hive.HiveGame.HiveHostGame.Plugin");

        private static readonly IHttpQueueCountersInstance _Total = HttpQueuePerformanceCounters.GetInstance("_Total");

        private static readonly IHttpQueueCountersInstance CountersInstance =
            HttpQueuePerformanceCounters.GetInstance(ApplicationBase.Instance.PhotonInstanceName + "_game");

        private static readonly LogCountGuard raiseEventExceptionLogGuard = new LogCountGuard(new TimeSpan(0, 0, 10));
        private static readonly LogCountGuard createGameCountGuard = new LogCountGuard(new TimeSpan(0, 0, 10));
        private static readonly LogCountGuard hhgDisconnectLogGuard = new LogCountGuard(new TimeSpan(0, 0, 10));

        private readonly LogCountGuard customHeaderExceptionLogGuard = new LogCountGuard(new TimeSpan(0, 1, 0));

        private readonly AutoResetEvent onHttpResponseEvent = new AutoResetEvent(false);

        private readonly CallCounter PendingPluginContinue = new CallCounter();

        private readonly CustomTypeCache customTypeCache = new CustomTypeCache();

        private readonly HttpRequestQueue httpRequestQueue = new HttpRequestQueue();

        private readonly int httpQueueRequestTimeout;

        protected CallEnv callEnv;
        private bool allowSetGameState = true;
        private PluginWrapper pluginWrapper;
        private readonly IPluginManager pluginManager;
        private readonly TimeIntervalCounterLite httpForwardedRequests = new TimeIntervalCounterLite(new TimeSpan(0, 0, 1));
        private readonly PluginFiber roomsPluginFiber;

        private readonly IPluginLogMessagesCounter logMessagesCounter;

        #endregion

        #region Constructors and Destructors

        private static string GetHwId()
        {
            if (!string.IsNullOrEmpty(ApplicationBase.Instance.HwId)) return ApplicationBase.Instance.HwId;

            string password = System.Environment.MachineName + System.Environment.OSVersion + System.Environment.UserName;
            var crypt = new SHA256Managed();
            string hash = string.Empty;
            byte[] crypto = crypt.ComputeHash(Encoding.UTF8.GetBytes(password), 0, Encoding.UTF8.GetByteCount(password));
            foreach (byte bit in crypto)
            {
                hash += bit.ToString("x2");
            }
            return hash;
        }

        public HiveHostGame(GameCreateOptions gameCreateOptions) 
            : base(gameCreateOptions.GameName, gameCreateOptions.RoomCache, gameCreateOptions.GameStateFactory, 
                gameCreateOptions.MaxEmptyRoomTTL, gameCreateOptions.ExecutionFiber)
        {
            this.pluginManager = gameCreateOptions.PluginManager;
            this.logMessagesCounter = gameCreateOptions.LogMessagesCounter;

            var httpRequestQueueOptions = gameCreateOptions.HttpRequestQueueOptions;

            this.httpRequestQueue.MaxErrorRequests = httpRequestQueueOptions.MaxErrorRequests;
            this.httpRequestQueue.MaxTimedOutRequests = httpRequestQueueOptions.MaxTimedOutRequests;
            this.httpRequestQueue.ReconnectInterval = TimeSpan.FromMilliseconds(httpRequestQueueOptions.ReconnectInterval);
            this.httpRequestQueue.QueueTimeout = TimeSpan.FromMilliseconds(httpRequestQueueOptions.QueueTimeout);
            this.httpRequestQueue.MaxQueuedRequests = httpRequestQueueOptions.MaxQueuedRequests;
            this.httpRequestQueue.MaxBackoffInMilliseconds = httpRequestQueueOptions.MaxBackoffTime;
            this.httpRequestQueue.MaxConcurrentRequests = httpRequestQueueOptions.MaxConcurrentRequests;
            this.httpRequestQueue.ResponseMaxSizeLimit = httpRequestQueueOptions.LimitHttpResponseMaxSize;

            this.httpQueueRequestTimeout = httpRequestQueueOptions.HttpRequestTimeout;

            this.httpRequestQueue.SetCounters(this);

            this.Environment = gameCreateOptions.Environment ?? new Dictionary<string, object>
            {
                {"AppId", GetHwId()},
                {"AppVersion", ""},
                {"Region", ""},
                {"Cloud", ""},
            };

            this.customTypeCache.TypeMapper = this;
            this.roomsPluginFiber = new PluginFiber(this.ExecutionFiber);
        }

        #endregion

        #region Properties

        public IGamePlugin Plugin => this.pluginWrapper;

        public IPluginInstance PluginInstance => this.pluginWrapper.PluginInstance;

        public Dictionary<string, object> Environment { get; set; }

        public int HttpForwardedOperationsLimit { get; protected set; }

        IList<IActor> IPluginHost.GameActors
        {
            get
            {
                return this.ActorsManager.AllActors.Select(a => (IActor)a).ToList();
            }
        }

        IList<IActor> IPluginHost.GameActorsActive
        {
            get
            {
                return this.ActorsManager.ActiveActors.Select(a => (IActor)a).ToList();
            }
        }

        IList<IActor> IPluginHost.GameActorsInactive
        {
            get
            {
                return this.ActorsManager.InactiveActors.Select(a => (IActor)a).ToList();
            }
        }

        string IPluginHost.GameId => this.Name;

        Hashtable IPluginHost.GameProperties => this.Properties.GetProperties();

        Dictionary<string, object> IPluginHost.CustomGameProperties
        {
            get
            {
                if (this.LobbyProperties != null)
                {
                    var customProperties =
                        this.Properties.AsDictionary()
                            .Where(prop => this.LobbyProperties.Contains(prop.Key))
                            .ToDictionary(prop => (string)prop.Key, prop => prop.Value.Value);
                    return customProperties;
                }
                return new Dictionary<string, object>();
            }
        }

        public override bool IsPersistent => this.Plugin.IsPersistent;

        protected bool FailedOnCreate { get; private set; }

        public bool IsSuspended => this.ExecutionFiber.IsPaused;

        public bool IsSyspended => this.IsSuspended;

        #endregion

        #region Indexers

        public object this[object key]
        {
            get => this.Properties.GetProperty(key).Value;

            set => this.Properties.Set(key, value);
        }

        #endregion

        #region Public Methods

        public override bool BeforeRemoveFromCache()
        {
            this.RemoveRoomPath = RemoveState.BeforeRemoveFromCacheCalled;
            // we call the plugin and give it a chance to change the TTL - not sure it is a good idea ... but anyway.
            // we return false to suppress getting evicted from the cache.
            // and we enqueue the plugin call because we are holding a cache lock !!
            this.removalStartTimestamp = DateTime.Now;

            this.ExecutionFiber.Enqueue(
                () =>
                {
                    this.RemoveRoomPath = RemoveState.BeforeRemoveFromCacheActionCalled;
                    var request = new CloseRequest { EmptyRoomTTL = this.EmptyRoomLiveTime };
                    RequestHandler handler = () =>
                    {
                        try
                        {
                            return this.ProcessBeforeCloseGame(request);
                        }
                        catch (Exception e)
                        {
                            Log.Error(e);
                            // here we can not rethrow because we are in fiber action
                            this.Plugin.ReportError(Photon.Hive.Plugin.ErrorCodes.UnhandledException, e);
                            this.TriggerPluginOnClose();
                        }
                        return false;
                    };

                    var info = new BeforeCloseGameCallInfo(this.PendingPluginContinue, this.callEnv)
                                   {
                                       Request = request,
                                       Handler = handler,
                                       SendParams = new SendParameters(),
                                       FailedOnCreate = this.FailedOnCreate,
                                   };

                    this.Plugin.BeforeCloseGame(info);
                });

            return false;
        }

        protected override void Dispose(bool dispose)
        {
            base.Dispose(dispose);
            if (dispose)
            {
                this.httpRequestQueue.Dispose();
            }
        }

        #endregion

        #region Implemented Interfaces

        #region IPluginHost

        void IPluginHost.BroadcastEvent(IList<int> receiverActors, int senderActor, byte evCode, Dictionary<byte, object> data, byte cacheOp,
            Photon.Hive.Plugin.SendParameters sendParameters)
        {
            var targets = receiverActors == null ? this.ActiveActors : this.ActorsManager.ActorsGetActorsByNumbers(receiverActors.ToArray());

            this.BroadcastEventInternal(evCode, data, cacheOp, true, senderActor, targets, sendParameters);
        }

        void IPluginHost.BroadcastEvent(byte target, int senderActor, byte targetGroup, byte evCode, Dictionary<byte, object> data, byte cacheOp,
            Photon.Hive.Plugin.SendParameters sendParameters)
        {
            var updateEventCache = target != ReciverGroup.Group;
            var actors = this.GetActorsFromTarget(target, senderActor, targetGroup, out var errorMsg);
            if (actors == null)
            {
                throw new ArgumentException(errorMsg);

            }

            this.BroadcastEventInternal(evCode, data, cacheOp, updateEventCache, senderActor, actors, sendParameters);
        }

        private void BroadcastEventInternal(byte evCode, Dictionary<byte, object> data,
            byte cacheOp, bool updateEventCache, int senderActor, IEnumerable<Actor> actors, Plugin.SendParameters sendParameters)
        {
            if (updateEventCache && cacheOp != (byte)CacheOperation.DoNotCache)
            {
                string msg;
                if (cacheOp > (byte)CacheOperation.AddToRoomCacheGlobal)
                {
                    msg = $"Unsupported value {cacheOp} for cacheOp. Use method ExecuteCacheOperation";
                    Log.Error(msg);
                    throw new ArgumentException(msg);
                }

                if (cacheOp != (byte)CacheOperation.AddToRoomCacheGlobal && senderActor <= 0)
                {
                    msg = $"Cache operation={cacheOp} requires existing sender number";
                    Log.Error(msg);
                    throw new ArgumentException(msg);
                }

                if (senderActor > 0)
                {
                    var actor = this.ActorsManager.ActorsGetActorByNumber(senderActor);
                    if (actor == null)
                    {
                        msg = $"Invalid senderActor={senderActor} specified. Number may be 0 or existing";
                        Log.Error(msg);
                        throw new ArgumentException(msg);
                    }
                }

                if (!this.UpdateEventCache(senderActor, evCode, data, cacheOp, out msg))
                {
                    Log.Error(msg);
                }
            }

            var ed = new EventData(evCode, data);
            this.PublishEvent(ed, actors, MakeSendParams(sendParameters));
        }

        public bool ExecuteCacheOperation(CacheOp operation, out string errorMsg)
        {
            if (operation.ActorNr > 0)
            {
                var actor = this.ActorsManager.ActorsGetActorByNumber(operation.ActorNr);
                if (actor == null)
                {
                    errorMsg = $"Invalid senderActor={operation.ActorNr} specified. Number may be 0 or existing";
                    Log.Error(errorMsg);
                    return false;
                }
            }

            errorMsg = string.Empty;
            var cacheOp = (CacheOperation) operation.CacheOperation;
            if (cacheOp >= CacheOperation.SliceIncreaseIndex && cacheOp <= CacheOperation.SlicePurgeUpToIndex)
            {
                return this.UpdateCacheSlice(cacheOp, operation.ActorNr, operation.SliceIndex, out errorMsg);
            }

            switch (cacheOp)
            {
                case CacheOperation.DoNotCache:
                    return true;
                case CacheOperation.MergeCache:
                case CacheOperation.ReplaceCache:
                case CacheOperation.RemoveCache:
                case CacheOperation.AddToRoomCache:
                case CacheOperation.AddToRoomCacheGlobal:
                {
                    return this.UpdateEventCache(operation.ActorNr, operation.EventCode, operation.Data, operation.CacheOperation, out errorMsg);
                }
                case CacheOperation.RemoveFromRoomCache:
                {
                    if (operation.Actors == null)
                    {
                        if (operation.Target != 255)
                        {
                            var actors = this.GetActorsFromTarget(operation.Target, operation.ActorNr, 255, out errorMsg);
                            if (actors == null)
                            {
                                return false;
                            }
                            operation.Actors = actors.Select(x => x.ActorNr).ToArray();
                        }
                    }
                    this.EventCache.RemoveEventsFromCache(operation.EventCode, operation.Actors, operation.Data);
                    return true;
                }
                case CacheOperation.RemoveFromCacheForActorsLeft:
                {
                    this.EventCache.RemoveEventsForActorsNotInList(this.ActorsManager.ActorsGetActorNumbers());
                    return true;
                }
                default:
                    break;
            }

            errorMsg = $"Unknown cache operation={operation.CacheOperation}.";
            return false;
        }


        private static SendParameters MakeSendParams(Plugin.SendParameters sendParameters)
        {
            var deliveryMode = DeliveryMode.Reliable;

            //use DeliveryMode if set
            if (sendParameters.DeliveryMode.HasValue)
            {
                switch (sendParameters.DeliveryMode.Value)
                {
                    case PluginDeliveryMode.Unreliable:
                        deliveryMode = DeliveryMode.UnReliable;
                        break;

                    case PluginDeliveryMode.ReliableUnsequenced:
                        deliveryMode = DeliveryMode.ReliableUnsequenced;
                        break;

                    case PluginDeliveryMode.UnreliableUnsequenced:
                        deliveryMode = DeliveryMode.UnSequenced;
                        break;

                    default:
                        //keep DeliveryMode.Reliable
                        break;
                }
            }
            else if (sendParameters.Unreliable)
            {
                deliveryMode = DeliveryMode.UnReliable;
            }

            return new SendParameters
            {
                ChannelId = sendParameters.ChannelId,
                Encrypted = sendParameters.Encrypted,
                Flush = sendParameters.Flush,
                Unreliable = sendParameters.Unreliable,
                DeliveryMode = deliveryMode
            };
        }
        void IPluginHost.BroadcastErrorInfoEvent(string message, Plugin.SendParameters sendParameters)
        {
            this.PublishEvent(new ErrorInfoEvent(message), this.ActiveActors, MakeSendParams(sendParameters));
        }

        void IPluginHost.BroadcastErrorInfoEvent(string message, ICallInfo info, Plugin.SendParameters sendParameters)
        {
            this.PublishEvent(new ErrorInfoEvent(message), this.ActiveActors, MakeSendParams(sendParameters));
        }

        private Action GetTimerAction(Action callback)
        {
            return () =>
            {
                try
                {
                    callback();
                }
                catch (Exception e)
                {
                    Log.Error("Exception in timer callback", e);
                    if (this.Plugin != null)
                    {
                        try
                        {
                            this.Plugin.ReportError(Hive.Plugin.ErrorCodes.UnhandledException, e);
                        }
                        catch (Exception exception)
                        {
                            Log.Error("Exception during Plugin.ReportError call.", exception);
                        }
                    }
                    else
                    {
                        Log.WarnFormat("Timer is active. Plugin is null. Game:{0}", this.Name);
                    }
                }
            };
        }

        object IPluginHost.CreateOneTimeTimer(Action callback, int dueTimeMs)
        {
            var action = this.GetTimerAction(callback);
            return this.ExecutionFiber.Schedule(action, dueTimeMs);
        }

        object IPluginHost.CreateOneTimeTimer(ICallInfo info, Action callback, int dueTimeMs)
        {
            var callInfo = (CallInfo)info;
            callInfo?.InternalDefer();

            return this.ExecutionFiber.Schedule(() => this.SafeOneTimeTimerCallback(callInfo, callback), dueTimeMs);
        }

        object IPluginHost.CreateTimer(Action callback, int dueTimeMs, int intervalMs)
        {
            return this.ExecutionFiber.ScheduleOnInterval(this.GetTimerAction(callback), dueTimeMs, intervalMs);
        }

        bool IPluginHost.RemoveActor(int actorNr, string reasonDetail)
        {
            return ((IPluginHost)this).RemoveActor(actorNr, 0, reasonDetail);
        }

        bool IPluginHost.RemoveActor(int actorNr, byte reason, string reasonDetail)
        {
            var actor = this.RemoveActor(actorNr, reason, reasonDetail);
            if (actor == null)
            {
                return false;
            }

            switch (reason)
            {
                case RemoveActorReason.Banned:
                    this.ApplyBanning(actor);
                    break;
                case RemoveActorReason.GlobalBanned:
                    this.ApplyGlobalBanning(actor);
                    break;
            }
            return true;
        }

        [ObsoleteAttribute("This method is obsolete. Call GetGameState() instead.", false)]
        public Dictionary<byte, byte[]> GetGameStateAsByteArray()
        {
            return null;
        }

        private class ResponseDataCarrier
        {
            public HttpRequestQueueResultCode Result { get; set; }
            public AsyncHttpRequest HttpRequest { get; set; }
            public object State { get; set; }
        }

        void IPluginHost.HttpRequest(HttpRequest request)
        {
            if (request.Callback == null)
            {
                var url = request.Url;
                Log.Debug("HttpRequest Callback is not set. Using default to log in case of error. " + url);

                request.Callback = (response, state) =>
                    {
                        if (response.Status != HttpRequestQueueResult.Success)
                        {
                            Log.Warn($"Request to '{url}' failed. reason={response.Reason}, httpcode={response.HttpCode} webstatus={response.WebStatus}, HttpQueueResult={response.Status}.");
                        }
                    };
            }
            const int RequestRetryCount = 3;
            var stateCarrier = new ResponseDataCarrier();
            try
            {
                var webRequest = (HttpWebRequest)WebRequest.Create(request.Url);
                webRequest.Proxy = null;
                webRequest.Method = request.Method ?? "GET";
                webRequest.ContentType = request.ContentType;
                webRequest.Accept = request.Accept;
                webRequest.Timeout = this.httpQueueRequestTimeout;

                if (request.CustomHeaders != null)
                {
                    this.AddCustomHttpHeaders(request, webRequest);
                }

                if (request.Headers != null)
                {
                    this.AddPredefinedHttpHeaders(request, webRequest);
                }

                HttpRequestQueueCallback callback = (result, httpRequest, state) =>
                    {
                        if (Log.IsDebugEnabled)
                        {
                            Log.DebugFormat("callback for request is called.url:{0}.IsAsync:{1}",
                                httpRequest.WebRequest.RequestUri, request.Async);
                        }

                        if (request.Async)
                        {
                            this.ExecutionFiber.Enqueue(() => this.HttpRequestHttpCallback(request, null, result, httpRequest, state));
                        }
                        else
                        {
                            stateCarrier.HttpRequest = httpRequest;
                            stateCarrier.Result = result;
                            stateCarrier.State = state;

                            this.onHttpResponseEvent.Set();
                        }
                    };

                this.EnqueueWebRequest(request, RequestRetryCount, webRequest, callback);
            }
            catch (WebException e)
            {
                Log.Error($"Exception calling Url:{request.Url}", e);
                var response = new HttpResponseImpl(request, null, HttpRequestQueueResultCode.Error, e.Message, (int)e.Status);
                request.Callback(response, request.UserState);
                return;
            }
            catch (Exception ex)
            {
                Log.Error($"Exception calling Url:{request.Url}, Exception Msg:{ex.Message}", ex);
                var response = new HttpResponseImpl(
                    request,
                    null,
                    HttpRequestQueueResultCode.Error,
                    ex.Message,
                    (int)WebExceptionStatus.UnknownError);
                request.Callback(response, request.UserState);
                return;
            }

            if (request.Async)
            {
                Log.Debug("HttpRequest() - NOT Waiting for HttpResponse.");
                // we return immediately without waiting for response
                return;
            }

            var timeout = (RequestRetryCount + 1) * this.httpQueueRequestTimeout
                + (int) this.httpRequestQueue.QueueTimeout.TotalMilliseconds + 1000;
            // waiting for our callback to release us
            Log.Debug("HttpRequest() - Waiting for HttpResponse.");
            if (!this.onHttpResponseEvent.WaitOne(timeout))
            {
                Log.WarnFormat("Plugin's sync http call timedout. url:{0}, Method:{1}, timeout:{2}", request.Url, request.Method, timeout);
                this.HttpRequestHttpCallback(request, null, HttpRequestQueueResultCode.Error, null, request.UserState);
                return;
            }
            Log.Debug("HttpRequest() - Done.");

            this.HttpRequestHttpCallback(request, null, stateCarrier.Result, stateCarrier.HttpRequest, stateCarrier.State);
        }

        void IPluginHost.HttpRequest(HttpRequest request, ICallInfo info)
        {
            if (request.Callback == null)
            {
                var url = request.Url;
                Log.Debug("HttpRequest Callback is not set. Using default to log in case of error. " + url);

                request.Callback = (response, state) =>
                {
                    if (response.Status != HttpRequestQueueResult.Success)
                    {
                        Log.Warn(
                            $"Request to '{url}' failed. reason={response.Reason}, httpcode={response.HttpCode} webstatus={response.WebStatus}, HttpQueueResult={response.Status}.");
                    }
                };
            }


            const int RequestRetryCount = 3;
            if (request.Async)
            {
                if (info != null)
                {
                    ((CallInfo)info).InternalDefer();
                }
            }
            else
            {
                if (info == null)
                {
                    throw new ArgumentNullException(nameof(info));
                }

                var timeout = (int)this.httpRequestQueue.QueueTimeout.TotalMilliseconds +
                    (RequestRetryCount + 1) * this.httpQueueRequestTimeout + 5000;

                Action timeoutAction = () =>
                {
                    Log.ErrorFormat("Game did not resumed after {0} ms. http call to {1}. Headers: {2}, CustomHeaders:{3}, " +
                                    "Accept:{4}, ContentType:{5}, Method:{6}",
                        timeout, request.Url,
                        request.Headers != null ? Newtonsoft.Json.JsonConvert.SerializeObject(request.Headers) : "<null>",
                        request.CustomHeaders != null ? Newtonsoft.Json.JsonConvert.SerializeObject(request.CustomHeaders) : "<null>",
                        request.Accept, request.ContentType, request.Method);

                    var response = new HttpResponseImpl(
                        request,
                        info,
                        null,
                        HttpRequestQueueResultCode.RequestTimeout,
                        0,
                        string.Empty,
                        (int)WebExceptionStatus.Timeout, null);
                    request.Callback(response, request.UserState);
                };

                this.SuspendGame(timeout, timeoutAction);

                ((CallInfo)info).Pause();
            }

            try
            {
                var webRequest = (HttpWebRequest)WebRequest.Create(request.Url);
                webRequest.Proxy = null;
                webRequest.Method = request.Method ?? "GET";
                webRequest.ContentType = request.ContentType;
                webRequest.Accept = request.Accept;
                webRequest.Timeout = this.httpQueueRequestTimeout;

                if (request.CustomHeaders != null)
                {
                    this.AddCustomHttpHeaders(request, webRequest);
                }

                if (request.Headers != null)
                {
                    this.AddPredefinedHttpHeaders(request, webRequest);
                }

                HttpRequestQueueCallback callback = (result, httpRequest, state) =>
                {
                    if (Log.IsDebugEnabled)
                    {
                        Log.DebugFormat("callback for request is called.url:{0}.IsAsync:{1}",
                            httpRequest.WebRequest.RequestUri, request.Async);
                    }

                    if (request.Async)
                    {
                        this.ExecutionFiber.Enqueue(() => this.HttpRequestCallback(request, result, httpRequest, state, info));
                    }
                    else
                    {
                        this.ResumeGame(() => this.HttpRequestCallback(request, result, httpRequest, state, info));
                    }
                };

                this.EnqueueWebRequest(request, RequestRetryCount, webRequest, callback);
            }
            catch (WebException e)
            {
                Log.Error($"Exception calling Url:{request.Url}", e);
                var response = new HttpResponseImpl(request, info, null, 
                    HttpRequestQueueResultCode.Error, 0, e.Message, (int)e.Status, null);
                request.Callback(response, request.UserState);
            }
            catch (Exception ex)
            {
                this.ExecutionFiber.Resume();

                Log.Error($"Exception calling Url:{request.Url}", ex);
                var response = new HttpResponseImpl(
                    request,
                    info,
                    null,
                    HttpRequestQueueResultCode.Error,
                    0,
                    ex.Message,
                    (int)WebExceptionStatus.UnknownError, null);
                request.Callback(response, request.UserState);
            }
        }

        private void EnqueueWebRequest(HttpRequest request, int RequestRetryCount, HttpWebRequest webRequest, HttpRequestQueueCallback callback)
        {
            switch(webRequest.Method)
            {
                case "GET":
                case "TRACE":
                case "HEAD":
                    this.httpRequestQueue.Enqueue(webRequest, callback, request.UserState, RequestRetryCount);
                    return;
            }
            if (request.DataStream != null)
            {
                this.httpRequestQueue.Enqueue(webRequest, request.DataStream.ToArray(), callback, request.UserState, RequestRetryCount);
            }
            else
            {
                this.httpRequestQueue.Enqueue(webRequest, callback, request.UserState, RequestRetryCount);
            }
        }

        void IPluginHost.LogDebug(object message)
        {
            try
            {
                LogPlugin.Debug(message);
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                throw;
            }
        }

        void IPluginHost.LogError(object message)
        {
            try
            {
                this.logMessagesCounter.IncrementErrorsCount();

                LogPlugin.Error(message);
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                throw;
            }
        }

        void IPluginHost.LogFatal(object message)
        {
            try
            {
                this.logMessagesCounter.IncrementFatalsCount();

                LogPlugin.Fatal(message);
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                throw;
            }
        }

        void IPluginHost.LogInfo(object message)
        {
            try
            {
                LogPlugin.Info(message);
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                throw;
            }
        }

        void IPluginHost.LogWarning(object message)
        {
            try
            {
                this.logMessagesCounter.IncrementWarnsCount();

                LogPlugin.Warn(message);
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                throw;
            }
        }

        bool IPluginHost.SetProperties(int actorNr, Hashtable properties, Hashtable expected, bool broadcast)
        {
            if (this.allowSetGameState || this.ActorsManager.Count == 0)
            {
                Log.Warn(HiveErrorMessages.UsageOfSetPropertiesNotAllowedBeforeContinue);
            }

            return this.SetProperties(actorNr, properties, expected, broadcast);
        }

        void IPluginHost.StopTimer(object timer)
        {
            if (timer is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        bool IPluginHost.TryRegisterType(Type type, byte typeCode, Func<object, byte[]> serializeFunction, Func<byte[], object> deserializeFunction)
        {
            return this.customTypeCache.TryRegisterType(type, typeCode, serializeFunction, deserializeFunction);
        }

        bool IPluginHost.SetGameState(SerializableGameState state)
        {
            if (!this.allowSetGameState)
            {
                Log.ErrorFormat("Plugin {0} tries to set game state after call to 'Continue'. Game '{1}', stack\n{2}",
                    this.Plugin.Name, this.Name, GetCallStack());
                return false;
            }
            return this.RoomState.SetState(state);
        }

        EnvironmentVersion IPluginHost.GetEnvironmentVersion()
        {
            return this.PluginInstance.Version;
        }

        IPluginLogger IPluginHost.CreateLogger(string name)
        {
            return new PluginLogger(name, this.logMessagesCounter);
        }

        void IPluginHost.Enqueue(Action action)
        {
            this.ExecutionFiber.Enqueue(action);
        }

        IPluginFiber IPluginHost.GetRoomFiber()
        {
            return this.roomsPluginFiber;
        }
        #endregion

        #region IHttpRequestQueueCounters

        public void HttpQueueRequestsIncrement()
        {
            _Total.IncrementQueueRequests();
            CountersInstance.IncrementQueueRequests();
        }

        public void HttpQueueResponsesIncrement()
        {
            _Total.IncrementQueueResponses();
            CountersInstance.IncrementQueueResponses();
        }

        public void HttpQueueSuccessIncrement()
        {
            _Total.IncrementQueueSuccesses();
            CountersInstance.IncrementQueueSuccesses();
            this.gameAppCounters.WebHooksQueueSuccessIncrement();
        }

        public void HttpQueueTimeoutIncrement()
        {
            _Total.IncrementQueueQueueTimeouts();
            CountersInstance.IncrementQueueQueueTimeouts();
        }

        public void HttpQueueErrorsIncrement()
        {
            _Total.IncrementQueueErrors();
            CountersInstance.IncrementQueueErrors();
            this.gameAppCounters.WebHooksQueueErrorIncrement();
        }

        public void HttpQueueOfflineResponsesIncrement()
        {
            _Total.IncrementQueueOfflineResponses();
            CountersInstance.IncrementQueueOfflineResponses();
        }

        public void HttpQueueConcurrentRequestsIncrement()
        {
            _Total.IncrementQueueConcurrentRequests();
            CountersInstance.IncrementQueueConcurrentRequests();
        }

        public void HttpQueueConcurrentRequestsDecrement()
        {
            _Total.DecrementQueueConcurrentRequests();
            CountersInstance.DecrementQueueConcurrentRequests();
        }

        public void HttpQueueQueuedRequestsIncrement()
        {
            _Total.IncrementQueueQueuedRequests();
            CountersInstance.IncrementQueueQueuedRequests();
        }

        public void HttpQueueQueuedRequestsDecrement()
        {
            _Total.DecrementQueueQueuedRequests();
            CountersInstance.DecrementQueueQueuedRequests();
        }

        public void HttpRequestExecuteTimeIncrement(long ticks)
        {
            _Total.IncrementHttpRequestExecutionTime(ticks);
            CountersInstance.IncrementHttpRequestExecutionTime(ticks);
            this.gameAppCounters.WebHooksHttpExecTimeIncrement(ticks);
        }

        public void HttpQueueOnlineQueueCounterIncrement()
        {
            _Total.IncrementQueueOnlineQueue();
            CountersInstance.IncrementQueueOnlineQueue();
        }

        public void HttpQueueOnlineQueueCounterDecrement()
        {
            _Total.DecrementQueueOnlineQueue();
            CountersInstance.DecrementQueueOnlineQueue();
        }

        public void HttpQueueBackedoffRequestsIncrement()
        {
            _Total.IncrementBackedOffRequests();
            CountersInstance.IncrementBackedOffRequests();
        }

        public void HttpQueueBackedoffRequestsDecrement()
        {
            _Total.DecrementBackedOffRequests();
            CountersInstance.DecrementBackedOffRequests();
        }

        public void HttpRequestIncrement()
        {
            _Total.IncrementHttpRequests();
            CountersInstance.IncrementHttpRequests();
        }

        public void HttpSuccessIncrement()
        {
            _Total.IncrementHttpSuccesses();
            CountersInstance.IncrementHttpSuccesses();
            this.gameAppCounters.WebHooksHttpSuccessIncrement();
        }

        public void HttpTimeoutIncrement()
        {
            _Total.IncrementHttpRequestTimeouts();
            CountersInstance.IncrementHttpRequestTimeouts();
            this.gameAppCounters.WebHooksHttpTimeoutIncrement();
        }

        public void HttpErrorsIncrement()
        {
            _Total.IncrementHttpErrors();
            CountersInstance.IncrementHttpErrors();
            this.gameAppCounters.WebHooksHttpErrorIncrement();
        }

        public void HttpResponseIncrement()
        {
            _Total.IncrementHttpResponses();
            CountersInstance.IncrementHttpResponses();
        }
        #endregion

        #region IUnknownTypeMapper

        public bool OnUnknownType(Type type, ref object obj)
        {
            return this.Plugin.OnUnknownType(type, ref obj);
        }

        #endregion

        #region IPluginHttpClient
        HttpRequestQueue IPluginHttpUtilClient.HttpRequestQueue => this.httpRequestQueue;

        int IPluginHttpUtilClient.HttpQueueRequestTimeout => this.httpQueueRequestTimeout;

        IExtendedFiber IPluginHttpUtilClient.ExecutionFiber => this.ExecutionFiber;
        ILogger IPluginHttpUtilClient.Log => Log;

        void IPluginHttpUtilClient.ResumeClient(Action resumeAction)
        {
            this.ResumeGame(resumeAction);
        }

        void IPluginHttpUtilClient.SuspendClient(int timeout, Action timeoutAction)
        {
            this.SuspendGame(timeout, timeoutAction);
        }

        void IPluginHttpUtilClient.PluginReportError(short errorCode, Exception ex, object state)
        {
            this.Plugin.ReportError(errorCode, ex, state);
        }
        #endregion
        #endregion

        #region Methods

        protected virtual void ResumeGame(Action resumeAction)
        {
            Log.DebugFormat("Resuming game {0}", this.Name);
            this.ExecutionFiber.Resume(resumeAction);
        }

        protected virtual void SuspendGame(int timeout, Action timeoutAction)
        {
            Log.DebugFormat("Suspending game {0}", this.Name);
            if (this.ExecutionFiber.IsPaused)
            {
                throw new Exception("Game is already paused");
            }
            this.ExecutionFiber.Pause(timeout, timeoutAction);
        }

        private void SafeOneTimeTimerCallback(CallInfo info, Action callback)
        {
            var needCheck = info != null && info.IsDeferred;
            try
            {
                if (needCheck)
                {
                    info.Reset();
                }

                callback();

                if (needCheck && !((ICallInfo)info).StrictModeCheck(out string errorMsg))
                {
                    var infoTypeName = info.GetType().ToString();
                    this.Plugin.ReportError(Photon.Hive.Plugin.ErrorCodes.MissingCallProcessing, null, infoTypeName);
                    info.Fail($"SafeOneTimeTimerCallback: {errorMsg}");
                }
            }
            catch (Exception e)
            {
                this.Plugin.ReportError(Hive.Plugin.ErrorCodes.UnhandledException, e);
                Log.WarnFormat("Exception during SafeOneTimeTimerCallback call", e);
                if (info != null && !info.IsProcessed)
                {
                    info.Fail(e.ToString());
                }
            }
        }

        private void AddPredefinedHttpHeaders(HttpRequest request, HttpWebRequest webRequest)
        {
            foreach (var kv in request.Headers)
            {
                try
                {
                    if (!this.ApplyRestrictedHeader(kv.Key, kv.Value, webRequest))
                    {
                        webRequest.Headers.Add(kv.Key, kv.Value);
                    }
                }
                catch (Exception e)
                {
                    Log.Warn(
                        $"Header add exception:'{kv.Key}' with value '{kv.Value}'. Game:{this}. Exception Msg:{e.Message}", e);
                }
            }
        }

        private bool ApplyRestrictedHeader(HttpRequestHeader key, string value, HttpWebRequest webRequest)
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
                    Log.WarnFormat("Usage of restricted http header :'{0}' with value '{1}'. Game:{2}. ", key, value, this);
                    LogPlugin.WarnFormat("Usage of restricted http header :'{0}' with value '{1}'. Game:{2}. ", key, value, this);
                    return true;
                default:
                    return false;
            }
            return true;
        }

        private bool ApplyRestrictedHeader(string key, string value, HttpWebRequest webRequest)
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
                        Log.WarnFormat("Usage of restricted http header :'{0}' with value '{1}'. Game:{2}. ", key, value, this);
                        LogPlugin.WarnFormat("Usage of restricted http header :'{0}' with value '{1}'. Game:{2}. ", key, value, this);
                        break;
                    }
                    return false;
            }
            return true;
        }

        private void AddCustomHttpHeaders(HttpRequest request, HttpWebRequest webRequest)
        {
            foreach (var kv in request.CustomHeaders)
            {
                try
                {
                    if (!ApplyRestrictedHeader(kv.Key, kv.Value, webRequest))
                    {
                        webRequest.Headers.Add(kv.Key, kv.Value);
                    }
                }
                catch (Exception e)
                {
                    Log.Warn(this.customHeaderExceptionLogGuard,
                        $"Custom header add exception:'{kv.Key}' with value '{kv.Value}'. Game:{this}. Exception Msg:{e.Message}", e);
                }
            }
        }

        private void HttpRequestHttpCallback(HttpRequest request, ICallInfo info,
            HttpRequestQueueResultCode result, AsyncHttpRequest asyncHttpRequest, object state)
        {

            var response = HttpResponseImpl.CreateHttpResponse(request, info, result, asyncHttpRequest);

            Log.Debug("Got HttpResponse - executing callback.");


            // Sync request triggers an event to release the waiting Request thread to continue
            try
            {
                request.Callback(response, state);
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                if (request.Async)
                {
                    Log.Error(ex);
                    this.Plugin.ReportError(Hive.Plugin.ErrorCodes.AsyncCallbackException, ex, state);
                }
                else
                {
                    throw;
                }
            }
        }

        private void HttpRequestCallback(HttpRequest request, HttpRequestQueueResultCode result, AsyncHttpRequest httpRequest, object state, ICallInfo info)
        {
            bool doCheck = false;
            if (info != null)
            {
                doCheck = !info.IsProcessed;
                if (doCheck)
                {
                    ((CallInfo)info).Reset();
                }
            }

            var response = HttpResponseImpl.CreateHttpResponse(request, info, result, httpRequest);

            Log.Debug("Got HttpResponse - executing callback.");

            try
            {
                request.Callback(response, state);

                // and check that one of methods was called
                if (doCheck && !info.StrictModeCheck(out var errorMsg))
                {
                    var infoTypeName = info.GetType().ToString();
                    this.Plugin.ReportError(Hive.Plugin.ErrorCodes.MissingCallProcessing, null, infoTypeName);
                    info.Fail($"HttpRequestCallback: {errorMsg}");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                this.Plugin.ReportError(Hive.Plugin.ErrorCodes.AsyncCallbackException, ex, state);
                if (info != null && !info.IsProcessed)
                {
                    info.Fail(ex.ToString());
                }
            }
        }


        private IEnumerable<Actor> GetActorsFromTarget(byte target, int senderActor, byte targetGroup, out string errorMsg)
        {
            IEnumerable<Actor> actors;
            switch (target)
            {
                case ReciverGroup.All:
                    actors = this.ActiveActors;
                    break;
                case ReciverGroup.Others:
                    actors = this.ActorsManager.ActorsGetExcludedList(senderActor);
                    break;
                case ReciverGroup.Group:
                    actors = this.GroupManager.GetActorGroup(targetGroup);
                    break;
                default:
                    errorMsg = $"Unknown target {target} specified in BroadcastEvent";
                    Log.Error(errorMsg);
                    return null;
            }
            errorMsg = string.Empty;
            return actors;
        }

        private void ApplyBanning(Actor actor)
        {
            this.ActorsManager.AddToExcludeList(actor.UserId, RemoveActorReason.Banned);
            this.OnActorBanned(actor);
        }

        private void ApplyGlobalBanning(Actor actor)
        {
            this.ActorsManager.AddToExcludeList(actor.UserId, RemoveActorReason.GlobalBanned);
            this.OnActorGlobalBanned(actor);
        }

        protected virtual void OnActorBanned(Actor actor)
        { }

        protected virtual void OnActorGlobalBanned(Actor actor)
        { }

        private Actor RemoveActor(int actorNr, int reason, string reasonDetail)
        {
            var actor = this.ActorsManager.ActorsGetActorByNumber(actorNr);
            if (actor != null)
            {
                actor.Peer.RemovePeerFromCurrentRoom(LeaveReason.PluginRequest, reasonDetail); //kicking player
                // during removing peer from Room it will be disconnected
            }
            else
            {
                actor = this.ActorsManager.InactiveActorsGetActorByNumber(actorNr);
                if (actor != null)
                {
                    base.RemoveInactiveActor(actor);
                }
            }
            return actor;
        }

        public override void RemoveInactiveActor(Actor actor)
        {
            RequestHandler handler = () =>
            {
                try
                {
                    return this.ProcessRemoveInactiveActor(actor);
                }
                catch (Exception e)
                {
                    Log.Error(e);
                    throw;
                }
            };

            var leaveRequest = GetMockLeaveRequest(null, actor.ActorNr, false);

            var info = new LeaveGameCallInfo(this.PendingPluginContinue, this.callEnv)
            {
                ActorNr = actor.ActorNr,
                UserId = actor.UserId,
                Nickname = actor.Nickname,
                IsInactive = false,
                Reason = LeaveReason.PlayerTtlTimedOut,
                Request = leaveRequest,
                Handler = handler,
                Peer = null,
                SendParams = new SendParameters(),
            };

            this.Plugin.OnLeave(info);
        }

        protected override void HandleCreateGameOperation(HivePeer peer, SendParameters sendParameters, JoinGameRequest createGameRequest)
        {
            if (this.IsFinished)
            {
                if (!this.ReinitGame())
                {
                    this.SendErrorResponse(peer, createGameRequest.OperationCode,
                        ErrorCode.InternalServerError, HiveErrorMessages.ReinitGameFailed, sendParameters);

                    createGameRequest.OnJoinFailed(ErrorCode.InternalServerError, HiveErrorMessages.ReinitGameFailed);

                    this.JoinFailureHandler(LeaveReason.ManagedDisconnect, peer, createGameRequest);

                    if (Log.IsWarnEnabled)
                    {
                        Log.WarnFormat("Game '{0}' userId '{1}' failed to create game. msg:{2} -- peer:{3}", this.Name, peer.UserId, 
                            HiveErrorMessages.ReinitGameFailed, peer);
                    }

                    return;
                }
            }

            if (!this.CreateGamePlugin(peer, sendParameters, createGameRequest))
            {
                return;
            }

            this.callEnv = new CallEnv(this.Plugin, this.Name);

            if (!this.ValidatePlugin(createGameRequest, out var msg))
            {
                this.SendErrorResponse(peer, createGameRequest.OperationCode, ErrorCode.PluginMismatch, msg, sendParameters);
                createGameRequest.OnJoinFailed(ErrorCode.PluginMismatch, msg);
                this.JoinFailureHandler(LeaveReason.ManagedDisconnect, peer, createGameRequest);

                if (Log.IsWarnEnabled)
                {
                    Log.WarnFormat("Game '{0}' userId '{1}' failed to create game. msg:{2} -- peer:{3}", this.Name, peer.UserId, msg, peer);
                }
                return;
            }

            this.HandleCreateGameOperationInt(peer, sendParameters, createGameRequest);
        }

        private bool CreateGamePlugin(HivePeer peer, SendParameters sendParameters, JoinGameRequest joinGameRequest)
        {
            var pluginName = joinGameRequest.GetPluginName();

            this.InitPlugin(pluginName);

            if (this.Plugin == null)
            {
                var errorMsg = $"Failed to create plugin '{pluginName}'";

                this.SendErrorResponse(peer, joinGameRequest.OperationCode,
                    ErrorCode.InternalServerError, errorMsg, sendParameters);

                joinGameRequest.OnJoinFailed(ErrorCode.InternalServerError, errorMsg);

                this.JoinFailureHandler(LeaveReason.ManagedDisconnect, peer, joinGameRequest);

                if (Log.IsWarnEnabled)
                {
                    Log.WarnFormat("Game '{0}' userId '{1}' failed to create game. msg:{2} -- peer:{3}", this.Name, peer.UserId,
                        errorMsg, peer);
                }

                return false;
            }

            this.callEnv = new CallEnv(this.Plugin, this.Name);
            return true;
        }

        private void HandleCreateGameOperationInt(HivePeer peer, SendParameters sendParameters,
            JoinGameRequest createGameRequest, bool fromJoin = false)
        {
            var createOptions = createGameRequest.GetCreateGameSettings(this);

            peer.SetPrivateCustomTypeCache(this.customTypeCache);

            RequestHandler handler = () =>
            {
                var oldValue = this.allowSetGameState;

                try
                {
                    this.allowSetGameState = false;

                    // since we allow to make changes to the original request sent by the client in a plugin
                    // we should check op.IsValid() - if not report error
                    createGameRequest.SetupRequest(peer.UserId);
                    if (!createGameRequest.IsValid)
                    {
                        this.FailedOnCreate = true;

                        peer.ReleaseRoomReference();
                        this.SendErrorResponse(peer, createGameRequest.OperationCode,
                            ErrorCode.OperationInvalid, createGameRequest.GetErrorMessage(), sendParameters);

                        peer.ScheduleDisconnect((int)ErrorCode.OperationInvalid, PeerBase.DefaultDisconnectInterval); // this gives the client a chance to get the reason

                        if (Loggers.InvalidOpLogger.IsInfoEnabled)
                        {
                            Loggers.InvalidOpLogger.Info(createGameCountGuard, $"Invalid operation. CreateGame Request is invalid. msg:{createGameRequest.GetErrorMessage()}, p:{peer}");
                        }

                        if (Loggers.DisconnectLogger.IsInfoEnabled)
                        {
                            Loggers.DisconnectLogger.Info(hhgDisconnectLogGuard, $"Disconnect during game creation. Reason: ErrorCode.OperationInvalid, Msg:{createGameRequest.GetErrorMessage()}");
                        }


                        return false;
                    }
                    return this.ProcessCreateGame(peer, createGameRequest, sendParameters);
                }
                catch (Exception)
                {
                    this.allowSetGameState = oldValue;
                    throw;
                }
            };

            var info = new CreateGameCallInfo(this.PendingPluginContinue, this.callEnv)
            {
                Request = createGameRequest,
                UserId = peer.UserId,
                Nickname = createGameRequest.GetNickname(),
                CreateOptions = createOptions,
                IsJoin = fromJoin,
                Handler = handler,
                Peer = peer,
                SendParams = sendParameters,
                OnFail = (onFailMsg, errorData) =>
                {
                    this.allowSetGameState = false;
                    this.FailedOnCreate = true;

                    peer.RemovePeerFromCurrentRoom((int)ErrorCode.PluginReportedError, onFailMsg);
                    this.SendErrorResponse(peer, createGameRequest.OperationCode,
                        ErrorCode.PluginReportedError, onFailMsg, sendParameters, errorData);

                    peer.ScheduleDisconnect((int)ErrorCode.PluginReportedError, PeerBase.DefaultDisconnectInterval); // this gives the client a chance to get the reason
                }
            };

            this.Plugin.OnCreateGame(info);
        }

        private bool ValidatePlugin(JoinGameRequest operation, out string msg)
        {
            if (operation.Plugins != null)
            {
                if (operation.Plugins.Length > 0)
                {
                    if (operation.Plugins.Length > 1)
                    {
                        msg = "Currently only one plugin per game supported.";
                        return false;
                    }

                    if (this.Plugin.Name != operation.Plugins[0])
                    {
                        var errorPlugin = this.pluginWrapper.ErrorPlugin;
                        if (errorPlugin != null)
                        {
                            msg =
                                $"Plugin Mismatch requested='{operation.Plugins[0]}' got ErrorPlugin with message:'{errorPlugin.Message}'";
                        }
                        else
                        {
                            msg = $"Plugin Mismatch requested='{operation.Plugins[0]}' got='{this.Plugin.Name}'";
                        }
                        return false;
                    }
                }
                else
                {
                    if (this.Plugin.Name != "Default")
                    {
                        msg = $"Room is setup with unexpected plugin '{this.Plugin.Name}' - instead of default (none).";
                        return false;
                    }
                }
            }
            msg = string.Empty;
            return true;
        }

        protected override void HandleJoinGameOperation(HivePeer peer, SendParameters sendParameters, JoinGameRequest joinGameRequest)
        {
            if (this.IsFinished)
            {
                if (!this.CheckGameCanBeCreated(peer, joinGameRequest))
                {
                    return;
                }

                if (!this.ReinitGame())
                {
                    this.SendErrorResponse(peer, joinGameRequest.OperationCode,
                        ErrorCode.InternalServerError, HiveErrorMessages.ReinitGameFailed, sendParameters);

                    joinGameRequest.OnJoinFailed(ErrorCode.InternalServerError, HiveErrorMessages.ReinitGameFailed);

                    this.JoinFailureHandler(LeaveReason.ManagedDisconnect, peer, joinGameRequest);
                    if (Log.IsDebugEnabled)
                    {
                        Log.DebugFormat("Game '{0}' userId '{1}' failed to join. msg:{2}", this.Name, peer.UserId,
                            HiveErrorMessages.ReinitGameFailed);
                    }
                    return;
                }
            }

            if (this.ActorsManager.ActorNumberCounter == 0) // we were just being created
            {
                if (this.Plugin == null && !this.CreateGamePlugin(peer, sendParameters, joinGameRequest))
                {
                    return;
                }

            }

            if (!this.ValidatePlugin(joinGameRequest, out var msg))
            {
                this.SendErrorResponse(peer, joinGameRequest.OperationCode, ErrorCode.PluginMismatch, msg, sendParameters);

                joinGameRequest.OnJoinFailed(ErrorCode.InternalServerError, HiveErrorMessages.ReinitGameFailed);
                this.JoinFailureHandler(LeaveReason.ManagedDisconnect, peer, joinGameRequest);

                if (Log.IsDebugEnabled)
                {
                    Log.DebugFormat("HandleJoinGameOperation: Game '{0}' userId '{1}' failed to join. msg:{2} -- peer:{3}", this.Name, peer.UserId, msg, peer);
                }
                return;
            }


            if (this.ActorsManager.ActorNumberCounter == 0) // we were just being created
            {
                if (!this.CheckGameCanBeCreated(peer, joinGameRequest))
                {
                    return;
                }

                this.HandleCreateGameOperationInt(peer, sendParameters, joinGameRequest, true);
            }
            else
            {
                peer.SetPrivateCustomTypeCache(this.customTypeCache);

                RequestHandler handler = () =>
                {
                    try
                    {
                        return this.ProcessBeforeJoinGame(joinGameRequest, sendParameters, peer);
                    }
                    catch (Exception e)
                    {
                        Log.Error(e);
                        throw;
                    }
                };
                var info = new BeforeJoinGameCallInfo(this.PendingPluginContinue, this.callEnv)
                {
                    Request = joinGameRequest,
                    UserId = peer.UserId,
                    Nickname = joinGameRequest.GetNickname(),
                    Handler = handler,
                    Peer = peer,
                    SendParams = sendParameters,
                    OnFail = (onFailMsg, errorData) =>
                    {
                        this.allowSetGameState = false;
                        joinGameRequest.OnJoinFailed(ErrorCode.PluginReportedError, onFailMsg);
                        this.OnJoinFailHandler(LeaveReason.ManagedDisconnect, onFailMsg, errorData, peer, sendParameters, joinGameRequest);
                    }
                };

                this.Plugin.BeforeJoin(info);

                this.CheckTotalPropertiesSize(null);
            }
        }

        private bool CheckGameCanBeCreated(HivePeer peer, JoinGameRequest joinGameRequest)
        {
            if (joinGameRequest.OperationCode == (byte)OperationCode.Join 
                || joinGameRequest.JoinMode == JoinModes.CreateIfNotExists
                || (joinGameRequest.JoinMode == JoinModes.RejoinOrJoin
                        && this.Plugin.IsPersistent)// for backwards compatibility - it seams some games expect this behavior - ISSUE: Codemasters uses RejoinOrJoin and now expects this to return false!
                || this.Plugin.IsPersistent)
            {
                return true;
            }

            this.SendErrorResponse(peer, joinGameRequest.OperationCode,
                ErrorCode.GameIdNotExists, HiveErrorMessages.GameIdDoesNotExist, new SendParameters());

            if (Log.IsDebugEnabled)
            {
                Log.DebugFormat(
                    "CheckGameCanBeCreated: Game '{0}' userId '{1}' failed to join game. msg:'{2}' (JoinMode={3}) -- peer:{4}",
                    this.Name,
                    peer.UserId,
                    HiveErrorMessages.GameIdDoesNotExist,
                    joinGameRequest.JoinMode,
                    peer);
            }

            joinGameRequest.OnJoinFailed(ErrorCode.GameIdNotExists, HiveErrorMessages.GameIdDoesNotExist);

            this.JoinFailureHandler(LeaveReason.ManagedDisconnect, peer, joinGameRequest);
            return false;
        }

        protected void InitPlugin(string pluginName)
        {
            if (this.pluginManager != null)
            {
                var pluginInstance = this.pluginManager.GetGamePlugin(this, pluginName);
                this.pluginWrapper = new PluginWrapper(pluginInstance);
            }
        }

        private bool ReinitGame()
        {
            if (this.Plugin == null)
            {
                Log.ErrorFormat("Reinit failed for game '{0}'. No plugin is set", this.Name);
                return false;
            }

            var pluginName = this.Plugin.Name;
            this.InitPlugin(pluginName);

            if (this.Plugin != null)
            {
                this.roomState = this.gameStateFactory.Create();

                this.EventCache.SetGameAppCounters(gameAppCounters);

                this.IsFinished = false;
                this.allowSetGameState = true;
                this.FailedOnCreate = false;
                this.callEnv = new CallEnv(this.Plugin, this.Name);
                return true;
            }

            Log.ErrorFormat("Reinit failed for game '{0}'. Failed to recreate plugin:'{1}'", this.Name, pluginName);
            return false;
        }

        /// <summary>
        /// Handles the <see cref="LeaveRequest"/> and calls <see cref="HiveGame.RemovePeerFromGame"/>.
        /// </summary>
        /// <param name="peer">
        /// The peer.
        /// </param>
        /// <param name="sendParameters">
        /// The send Parameters.
        /// </param>
        /// <param name="leaveOperation">
        /// The leave Operation.
        /// </param>
        protected override void HandleLeaveOperation(HivePeer peer, SendParameters sendParameters, LeaveRequest leaveOperation)
        {
            var actor = this.GetActorByPeer(peer);
            if (actor != null)
            {
                RequestHandler handler = () =>
                {
                    try
                    {
                        return this.ProcessLeaveGame(actor.ActorNr, leaveOperation, sendParameters, peer);
                    }
                    catch (Exception e)
                    {
                        Log.Error(e);
                        throw;
                    }
                };

                var info = new LeaveGameCallInfo(this.PendingPluginContinue, this.callEnv)
                {
                    ActorNr = actor.ActorNr,
                    UserId = peer.UserId,
                    Nickname = actor.Nickname,
                    IsInactive = leaveOperation != null && leaveOperation.IsCommingBack && this.PlayerTTL != 0,
                    Reason = LeaveReason.LeaveRequest,
                    Request = leaveOperation,
                    Handler = handler,
                    Peer = peer,
                    SendParams = sendParameters,
                };
                this.Plugin.OnLeave(info);
            }
            else
            {
                this.LogQueue.Add(new LogEntry("HandleLeaveOperation",
                    $"Failed to find Actor for peer {peer.ConnectionId}"));
            }
        }

        /// <summary>
        ///   Handles the <see cref = "RaiseEventRequest" />: Sends a <see cref = "CustomEvent" /> to actors in the room.
        /// </summary>
        /// <param name = "peer">
        ///   The peer.
        /// </param>
        /// <param name = "raiseEventRequest">
        ///   The operation
        /// </param>
        /// <param name = "sendParameters">
        ///   The send Parameters.
        /// </param>
        protected override void HandleRaiseEventOperation(HivePeer peer, RaiseEventRequest raiseEventRequest, SendParameters sendParameters)
        {
            // get the actor who send the operation request
            var actor = this.GetActorByPeer(peer);
            if (actor == null)
            {
                return;
            }

            if (raiseEventRequest.HttpForward
                && this.httpForwardedRequests.Increment(1) > this.HttpForwardedOperationsLimit)
            {
                this.SendErrorResponse(peer, raiseEventRequest.OperationCode,
                    ErrorCode.HttpLimitReached, HiveErrorMessages.HttpForwardedOperationsLimitReached, sendParameters);

                if (Log.IsWarnEnabled)
                {
                    Log.WarnFormat("Game '{0}' userId '{1}' RaiseEvent denied. msg:{2} -- peer:{3}", this.Name, peer.UserId,
                        HiveErrorMessages.HttpForwardedOperationsLimitReached, peer);
                }
                return;
            }

            if (raiseEventRequest.SimulatesPhotonEvent)
            {
                var msg = string.Format(HiveErrorMessages.SimulatesPhotonEvent, (byte)EventCode.LastPhotonEvent);
                this.SendErrorResponse(peer, raiseEventRequest.OperationCode, ErrorCode.OperationInvalid, msg, sendParameters);
                return;
            }

            RequestHandler handler = () =>
            {
                try
                {
                    return this.ProcessRaiseEvent(peer, raiseEventRequest, sendParameters, actor);
                }
                catch (Exception e)
                {
                    Log.Error(raiseEventExceptionLogGuard, $"Exception in RaiseEventHandler:Request:{raiseEventRequest.DumpRequest()}, " +
                              $"Actors:{this.ActorsManager.DumpActors()}, MCId:{this.MasterClientId}, SuppressRoomEvents:{this.SuppressRoomEvents}", e);
                    this.Plugin.ReportError(Photon.Hive.Plugin.ErrorCodes.UnhandledException, e, raiseEventRequest);
                }
                return false;
            };

            if (Log.IsDebugEnabled)
            {
                Log.Debug($"Broadcasting: Got RaiseEvent from {actor.ActorNr}/'{peer.UserId}', targetActors={raiseEventRequest.Actors?.Length ?? -1 }, Group={raiseEventRequest.Group}, ReceiverGroup={raiseEventRequest.ReceiverGroup}");
            }
            var info = new RaiseEventCallInfo(this.PendingPluginContinue, this.callEnv)
                {
                    ActorNr = actor.ActorNr,
                    Request = raiseEventRequest,
                    UserId = peer.UserId,
                    Nickname = actor.Nickname,
                    Handler = handler,
                    Peer = peer,
                    SendParams = sendParameters,
                };
            this.Plugin.OnRaiseEvent(info);
        }

        protected override void HandleRemovePeerMessage(HivePeer peer, int reason, string details)
        {
            if ((reason == LeaveReason.PlayerTtlTimedOut)
                || (reason == LeaveReason.LeaveRequest))
            {
                throw new ArgumentException("PlayerTtlTimeout and LeaveRequests are handled in their own routines.");
            }

            var actor = peer.Actor;
            var actorNr = actor?.ActorNr ?? -1;

            var isInactive = reason != LeaveReason.PluginRequest && peer.JoinStage == HivePeer.JoinStages.Complete && this.PlayerTTL != 0;

            RequestHandler handler = () =>
            {
                try
                {
                    return this.ProcessHandleRemovePeerMessage(peer, isInactive);
                }
                catch (Exception e)
                {
                    Log.Error(e);
                    throw;
                }
            };

            var leaveRequest = GetMockLeaveRequest(peer, actorNr, isInactive);
            var info = new LeaveGameCallInfo(this.PendingPluginContinue, this.callEnv)
            {
                ActorNr = actorNr,
                UserId = peer.UserId,
                Nickname = actor != null ? actor.Nickname : string.Empty,
                IsInactive = isInactive,
                Reason = reason,
                Details = details,
                Handler = handler,
                Peer = peer,
                OperationRequest = leaveRequest,
                SendParams = new SendParameters()
            };
            this.Plugin.OnLeave(info);
        }

        private static LeaveRequest GetMockLeaveRequest(HivePeer peer, int actorNr, bool isInactive)
        {
            var mockRequest = new OperationRequest((byte) OperationCode.Leave)
            {
                Parameters = new Dictionary<byte, object>
                {
                    {(byte) ParameterKey.IsInactive, isInactive}
                }
            };
            return new LeaveRequest(peer != null ? peer.Protocol : Protocol.GpBinaryV162, mockRequest);
        }

        protected override void HandleSetPropertiesOperation(HivePeer peer, SetPropertiesRequest request, SendParameters sendParameters)
        {
            var actor = this.GetActorByPeer(peer);

            if (actor == null)
            {
                this.SendErrorResponse(peer, request.OperationCode,
                    ErrorCode.OperationInvalid, HiveErrorMessages.PeerNotJoinedToRoom, sendParameters);

                if (Log.IsWarnEnabled)
                {
                    Log.WarnFormat("Game '{0}' userId '{1}' SetProperties failed. msg:{2} -- peer:{3}", this.Name, peer.UserId,
                        HiveErrorMessages.PeerNotJoinedToRoom, peer);
                }
                return;
            }

            if (request.HttpForward
                && this.httpForwardedRequests.Increment(1) > this.HttpForwardedOperationsLimit)
            {
                this.SendErrorResponse(peer, request.OperationCode,
                    ErrorCode.HttpLimitReached, HiveErrorMessages.HttpForwardedOperationsLimitReached, sendParameters);

                if (Log.IsWarnEnabled)
                {
                    Log.WarnFormat("Game '{0}' userId '{1}' SetProperties failed. msg:{2} -- peer:{3}", this.Name, peer.UserId,
                        HiveErrorMessages.HttpForwardedOperationsLimitReached, peer);
                }
                return;
            }

            RequestHandler handler = () =>
            {
                try
                {
                    if (!this.ValidateAndFillSetPropertiesRequest(peer, request, out var errorMsg))
                    {
                        this.SendErrorResponse(peer, (byte)OperationCode.SetProperties, 
                            ErrorCode.OperationInvalid, errorMsg, sendParameters);
                        this.Plugin.ReportError(Photon.Hive.Plugin.ErrorCodes.SetPropertiesPreconditionsFail, null, errorMsg);
                        return false;
                    }
                    return this.ProcessBeforeSetProperties(peer, request, sendParameters);
                }
                catch (Exception e)
                {
                    this.SendErrorResponse(peer, (byte)OperationCode.SetProperties, ErrorCode.InternalServerError, e.ToString(), sendParameters);
                    this.Plugin.ReportError(Photon.Hive.Plugin.ErrorCodes.UnhandledException, e, request);
                    Log.Error(e);
                    return false;
                }
            };

            var info = new BeforeSetPropertiesCallInfo(this.PendingPluginContinue, this.callEnv)
                           {
                               Request = request,
                               UserId = peer.UserId,
                               Nickname = actor.Nickname,
                               Handler = handler,
                               Peer = peer,
                               SendParams = sendParameters,
                               ActorNr = actor.ActorNr,
                           };
            this.Plugin.BeforeSetProperties(info);

            this.CheckTotalPropertiesSize(request);
        }

        protected virtual bool ProcessBeforeJoinGame(JoinGameRequest joinRequest, SendParameters sendParameters, HivePeer peer)
        {
            if (!this.JoinApplyGameStateChanges(peer, joinRequest, sendParameters, out var actor))
            {
                this.JoinFailureHandler(LeaveReason.ManagedDisconnect, peer, joinRequest);
                return false;
            }

            peer.SetJoinStage(HivePeer.JoinStages.BeforeJoinComplete);

            var info = new JoinGameCallInfo(this.PendingPluginContinue, this.callEnv)
            {
                UserId = peer.UserId,
                Peer = peer,
                Nickname = actor.Nickname,
                ActorNr = actor.ActorNr,
                Request = joinRequest,
                JoinParams = new ProcessJoinParams(),
                OnFail = (reason, parameters) => this.OnJoinFailHandler(LeaveReason.PluginFailedJoin, reason, parameters, peer, sendParameters, joinRequest),
            };

            RequestHandler handler = () =>
            {
                try
                {
                    if (!this.ProcessJoin(actor, joinRequest, sendParameters, info.JoinParams, peer))
                    {
                        // here we suppose that error response is already sent
                        this.JoinFailureHandler(LeaveReason.ManagedDisconnect, peer, joinRequest);
                        return false;
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e);
                    throw;
                }
                return true;
            };

            info.Handler = handler;

            this.Plugin.OnJoin(info);
            return true;
        }

        protected virtual bool ProcessBeforeSetProperties(HivePeer peer, SetPropertiesRequest request, SendParameters sendParameters)
        {
            if (!this.SetNewPropertyValues(request, out var errorMsg))
            {
                this.SendErrorResponse(peer, (byte)OperationCode.SetProperties, ErrorCode.OperationInvalid, errorMsg, sendParameters);
                this.Plugin.ReportError(Photon.Hive.Plugin.ErrorCodes.SetPropertiesCASFail, null, errorMsg);
                return false;
            }

            RequestHandler handler = () =>
            {
                try
                {
                    return this.ProcessSetProperties(peer, true, string.Empty, request, sendParameters);
                }
                catch (Exception e)
                {
                    Log.Error(e);
                    this.SendErrorResponse(peer, (byte)OperationCode.SetProperties, ErrorCode.InternalServerError, e.ToString(), sendParameters);
                    this.Plugin.ReportError(Photon.Hive.Plugin.ErrorCodes.SetPropertiesException, e, request);
                    return false;
                }
            };

            // we checked that actor is not null in HandleSetPropertiesOperation, but 
            // it is still possible that actor will be null, because we do not know 
            // how plugin will handle OnBeforeSetProperties. if it will do http request, than client peer may disconnect
            // so, we still need to check that client is not null

            var actor = request.SenderActor;
            var info = new SetPropertiesCallInfo(this.PendingPluginContinue, this.callEnv)
            {
                Request = request,
                Handler = handler,
                Peer = peer,
                UserId = peer.UserId,
                Nickname = actor != null ? actor.Nickname : string.Empty,
                SendParams = sendParameters,
                ActorNr = actor?.ActorNr ?? -1,
                OnFail = (errorMessage, objects) => this.OnSetPropertiesFailHandler(errorMessage, objects, request, peer, sendParameters),
            };

            this.Plugin.OnSetProperties(info);
            return true;
        }

        private void OnSetPropertiesFailHandler(string errorMessage, Dictionary<byte, object> parameters, SetPropertiesRequest request, HivePeer peer, SendParameters sendParameters)
        {
            LogPlugin.Error(errorMessage);
            this.SendErrorResponse(peer, request.OperationCode,
                ErrorCode.PluginReportedError, errorMessage, sendParameters, parameters);
            if (Log.IsWarnEnabled)
            {
                Log.WarnFormat("Game '{0}' userId '{1}' SetProperties plugin error. msg:{2} -- peer:{3}", this.Name, peer.UserId, errorMessage, peer);
            }
        }

        protected virtual bool ProcessSetProperties(HivePeer peer, bool result, string errorMsg, SetPropertiesRequest request, SendParameters sendParameters)
        {
            this.PublishResultsAndSetGameProperties(result, errorMsg, request, peer, sendParameters);
            return true;
        }

        protected virtual bool ProcessBeforeCloseGame(CloseRequest request)
        {
            // we currently allow the plugin to set the TTL - it could be changed
            // through plugin host properties ... we could remove that feature.
            // plugin.OnClose() is responsible for saving
            this.EmptyRoomLiveTime = request.EmptyRoomTTL > this.MaxEmptyRoomTTL ? this.MaxEmptyRoomTTL : request.EmptyRoomTTL;

            this.RemoveRoomPath = RemoveState.ProcessBeforeCloseGameCalled;
            if (this.EmptyRoomLiveTime <= 0)
            {
                this.RemoveRoomPath = RemoveState.ProcessBeforeCloseGameCalledEmptyRoomLiveTimeLECalled;
                this.TriggerPluginOnClose();
                return true;
            }

            this.ExecutionFiber.Enqueue(() => this.ScheduleTriggerPluginOnClose(this.EmptyRoomLiveTime));
            return true;
        }

        private void ScheduleTriggerPluginOnClose(int roomLiveTime)
        {
            this.RemoveRoomPath = RemoveState.ScheduleTriggerPluginOnCloseCalled;
            if (this.RemoveTimer != null)
            {
                this.RemoveTimer.Dispose();
                this.RemoveTimer = null;
            }

            this.RemoveTimer = this.ExecutionFiber.Schedule(this.TriggerPluginOnClose, roomLiveTime);

            if (Log.IsDebugEnabled)
            {
                Log.DebugFormat("Scheduled TriggerPluginOnClose: roomName={0}, liveTime={1:N0}", this.Name, roomLiveTime);
            }
        }

        private void TriggerPluginOnClose()
        {
            this.RemoveRoomPath = RemoveState.TriggerPluginOnCloseCalled;
            if (this.ActorsManager.ActiveActorsCount > 0)
            {
                if (this.EmptyRoomLiveTime == 0 && Log.IsWarnEnabled)
                {
                    Log.Warn($"Room still has actors(Count={this.ActorsManager.Count}). We stop removing.room:{this.Name}, Actors Dump:{this.ActorsManager.DumpActors()}");
                }
                // game already has players. stop closing it
                this.RemoveRoomPath = RemoveState.AliveGotNewPlayer;
                this.removalStartTimestamp = DateTime.MinValue;
                return;
            }

            RequestHandler handler = () =>
            {
                try
                {
                    return this.ProcessCloseGame(null);
                }
                catch (Exception e)
                {
                    Log.Error(e);
                    this.Plugin.ReportError(Photon.Hive.Plugin.ErrorCodes.UnhandledException, e);
                    this.TryRemoveRoomFromCache();
                }
                return false;
            };

            var info = new CloseGameCallInfo(this.PendingPluginContinue, this.callEnv)
            {
                ActorCount = this.ActorsManager.InactiveActorsCount,
                Handler = handler,
                Peer = null,
                SendParams = new SendParameters(),
                FailedOnCreate = this.FailedOnCreate,
                Request = new CloseRequest(),
            };

            try
            {
                this.Plugin.OnCloseGame(info);
            }
            finally
            {
                this.IsFinished = true;
            }
        }

        private void OnJoinFailHandler(byte leaveReason, string reasonDetails, Dictionary<byte, object> parameters,
            HivePeer peer, SendParameters sendParameters, JoinGameRequest request)
        {
            this.SendErrorResponse(peer, request.OperationCode,
                ErrorCode.PluginReportedError, reasonDetails, sendParameters, parameters);

            if (Log.IsDebugEnabled)
            {
                Log.DebugFormat("OnJoinFailHandler: Game '{0}' userId '{1}' failed to join. msg:{2} -- peer:{3}", this.Name, peer.UserId, reasonDetails, peer);
            }

            this.JoinFailureHandler(leaveReason, peer, request);
        }

        protected override void JoinFailureHandler(byte leaveReason, HivePeer peer, JoinGameRequest request)
        {
            base.JoinFailureHandler(leaveReason, peer, request);

            if (Log.IsDebugEnabled)
            {
                Log.DebugFormat("JoinFailureHandler is called for peer with reason:{0}.room:{1},p:{2}", request.FailureReason, this.Name, peer);
            }
            this.CallPluginOnLeaveIfJoinFailed(leaveReason, peer, request);


            if (Loggers.DisconnectLogger.IsInfoEnabled)
            {
                Loggers.DisconnectLogger.Info(hhgDisconnectLogGuard, $"Disconnect join failure disconnect. Reason:{request.FailureReason}, Msg:{request.FailureMessage}, p:{peer}");
            }
            peer.ScheduleDisconnect((int)request.FailureReason, PeerBase.DefaultDisconnectInterval);
        }

        private void CallPluginOnLeaveIfJoinFailed(byte reason, HivePeer peer, JoinGameRequest request)
        {
            if (peer.JoinStage == HivePeer.JoinStages.Connected || peer.JoinStage == Hive.HivePeer.JoinStages.CreatingOrLoadingGame)
            {
                if (Log.IsDebugEnabled)
                {
                    Log.DebugFormat("Peer join stage is {0}. CallPluginOnLeaveIfJoinFailed will be skipped. p:{1}", peer.JoinStage, peer);
                }
                return;
            }

            if (Log.IsDebugEnabled)
            {
                Log.DebugFormat("Peer join stage is {0}. reason:{1}. CallPluginOnLeaveIfJoinFailed is called. p:{2}", peer.JoinStage, reason, peer);
            }

            var actor = peer.Actor;// Actor can be null here because peer was not added to game yet

            RequestHandler handler = () =>
            {
                try
                {
                    this.RemovePeerFromGame(peer, false);
                }
                catch (Exception e)
                {
                    Log.Error(e);
                    throw;
                }
                return true;
            };

            var leaveRequest = GetMockLeaveRequest(peer, actor?.ActorNr ?? -1, false);

            var info = new LeaveGameCallInfo(this.PendingPluginContinue, this.callEnv)
            {
                ActorNr = actor?.ActorNr ?? -1,
                UserId = actor != null ? actor.UserId : peer.UserId,
                Nickname = actor != null ? actor.Nickname : request.GetNickname(),
                IsInactive = false,
                Reason = reason,
                Request = leaveRequest,
                Handler = handler,
                Peer = null,
                SendParams = new SendParameters(),
            };

            this.Plugin.OnLeave(info);
        }

        protected virtual bool ProcessCloseGame(object state)
        {
            this.RemoveRoomPath = RemoveState.ProcessCloseGameCalled;
            // enqueuing the remove from cache ensures we perform tasks enqueued by the plugin 
            // before we trigger the room release.
            // TBD - we might need something to make sure we limit potential disruption (only relevant for developer plugins)
            this.ExecutionFiber.Enqueue(this.TryRemoveRoomFromCache);
            return true;
        }

        protected virtual bool ProcessCreateGame(HivePeer peer, JoinGameRequest joinRequest, SendParameters sendParameters)
        {
            var result = this.CreateGame(peer, joinRequest, sendParameters);

            this.ActorsManager.DeactivateActors(this);

            return result;
        }

        protected virtual bool ProcessJoin(Actor actor, JoinGameRequest joinRequest, SendParameters sendParameters, ProcessJoinParams prms, HivePeer peer)
        {
            return this.JoinSendResponseAndEvents(peer, joinRequest, sendParameters, actor, prms);
        }

        protected virtual bool ProcessLeaveGame(int actorNr, LeaveRequest request, SendParameters sendParameters, HivePeer peer)
        {
            this.LeaveOperationHandler(peer, sendParameters, request);
            return true;
        }

        protected virtual bool ProcessRaiseEvent(HivePeer peer, RaiseEventRequest raiseEventRequest, SendParameters sendParameters, Actor actor)
        {
            return this.RaiseEventOperationHandler(peer, raiseEventRequest, sendParameters, actor);
        }

        private bool ProcessRemoveInactiveActor(Actor actor)
        {
            base.RemoveInactiveActor(actor);
            return true;
        }

        private bool ProcessHandleRemovePeerMessage(HivePeer actorPeer, bool isComingBack)
        {
            this.RemovePeerFromGame(actorPeer, isComingBack);
            return true;
        }

        protected override OperationResponse GetUserJoinResponse(JoinGameRequest joinRequest, Actor actor, ProcessJoinParams prms)
        {
            var res = base.GetUserJoinResponse(joinRequest, actor, prms);
            if (this.Plugin.Name != "Default")
            {
                res.Parameters.Add((byte)ParameterKey.PluginName, this.Plugin.Name);
                res.Parameters.Add((byte)ParameterKey.PluginVersion, this.Plugin.Version);
            }
            return res;
        }

        public SerializableGameState GetSerializableGameState()
        {
            return this.RoomState.GetSerializableGameState();
        }

        public Dictionary<string, object> GetGameState()
        {
            return this.RoomState.GetState();
        }

        public bool SetGameState(Dictionary<string, object> state)
        {
            if (!this.allowSetGameState)
            {
                Log.ErrorFormat("Plugin {0} tries to set game state after call to 'Continue'. Game '{1}', stack\n{2}",
                    this.Plugin.Name, this.Name, GetCallStack());
                return false;
            }
            var res = this.RoomState.SetState(state);

            Log.DebugFormat("Loading Room: actorNumberCounter={0} DeleteCacheOnLeave={1} EmptyRoomLiveTime={2} IsOpen={3} IsVisible={4} LobbyId={5} LobbyType={6} MaxPlayers={7} PlayerTTL={8} SuppressRoomEvents={9}",
                this.ActorsManager.ActorNumberCounter,//0
                this.DeleteCacheOnLeave,//1
                this.EmptyRoomLiveTime,//2
                this.IsOpen,//3
                this.IsVisible,//4
                this.LobbyId,//5
                this.LobbyType,//6
                this.MaxPlayers,//7
                this.PlayerTTL,//8
                this.SuppressRoomEvents//9
                );

            return res;
        }

        protected static string GetCallStack()
        {
            var st = new StackTrace();
            var sb = new StringBuilder();
            sb.AppendLine("Stack:");
            var count = st.FrameCount;
            if (count > 10)
            {
                count -= 10;
            }

            for (var i = 1; i < count; ++i)
            {
                sb.AppendLine(st.GetFrame(i).GetMethod().ToString());
            }
            return sb.ToString();
        }

        #endregion

        #region Types

        public class HttpResponseImpl : IHttpResponse
        {
            #region Constants and Fields

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

            public int HttpCode => this.httpCode;

            public string Reason => this.reason;

            public byte[] ResponseData => this.responseData;

            public string ResponseText => this.responseText;

            public byte Status => this.status;

            public int WebStatus => this.webStatus;

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
                            if (asyncHttpRequest == null)
                            {
                                statusDescription = "Thread deadlock happened";
                                break;
                            }

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
                            statusDescription = string.Empty;
                            break;
                    }
                }
                catch (Exception ex)
                {
                    // we should never get her
                    statusDescription = ex.Message;

                    Log.Warn("Exception during http response creation", ex);
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

        #endregion
    }
}