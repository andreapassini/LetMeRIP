using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Hive.Plugin;

namespace TestPlugins
{
    /// <summary>
    /// dummy class. its code is not executed at all, and it is used to select overload
    /// </summary>
    class Formater : IFormatProvider
    {
        public object GetFormat(Type formatType)
        {
            throw new NotImplementedException();
        }
    }

    class TypesTestPlugin : TestPluginBase
    {
        #region fields and consts

        private const string PathHook = "PathHook";

        private string pathHook;

        #endregion

        #region Properties

        public override bool IsPersistent
        {
            get
            {
                return true;
            }
        }

        #endregion

        #region Methods

        public override bool SetupInstance(IPluginHost host, Dictionary<string, string> config, out string errorMsg)
        {
            pathHook = config[PathHook];

            return base.SetupInstance(host, config, out errorMsg);
        }

        public override void OnCreateGame(ICreateGameCallInfo info)
        {
            var env = this.PluginHost.GetEnvironmentVersion();// to check that we may load EnvironmetVersion type correctly
            var env1 = env.BuiltWithVersion;
            var env2 = env.HostVersion;

            if (WebFlags.ShouldSendAuthCookie(1))// to check that web flags can be used
            {

            }

#pragma warning disable CS0219 // Variable is assigned but its value is never used
            var callStatus = CallStatus.Cancelled;// to check that CallStatus is accessible
#pragma warning restore CS0219 // Variable is assigned but its value is never used

            var status = HttpRequestQueueResult.Error;

#pragma warning disable CS0219 // Variable is assigned but its value is never used
            var leaveReason = LeaveReason.LeaveRequest;
#pragma warning restore CS0219 // Variable is assigned but its value is never used

#pragma warning disable CS0219 // Variable is assigned but its value is never used
            var joinMode = JoinModeConstants.JoinOnly;
#pragma warning restore CS0219 // Variable is assigned but its value is never used

            var opCode = info.Request.OperationCode;

            var opRequest = info.OperationRequest;

            if (opCode == 3)// to not call, but use so, that we will get exceptions during type loading
            {
                #region ICallInfo

                var t = info.IsCancelled;
                t = info.IsCanceled;
                t = info.IsDeferred;
                t = info.IsFailed;
                t = info.IsNew;
                t = info.IsProcessed;
                t = info.IsSucceeded;

                var opr = info.OperationRequest;

                var opr1 = opr.OperationCode;
                var opr2 = opr.Parameters;
                var opr3 = opr.WebFlags;

                status = info.Status;

                info.Continue();
                info.Fail();

                #endregion

                #region ICreateGameCallInfo

                var y1 = info.Request.ActorNr;
                var y2 = info.Request.ActorProperties;
                var y3 = info.Request.BroadcastActorProperties;
                var y4 = info.Request.CreateIfNotExists;
                var y5 = info.Request.DeleteCacheOnLeave;
                var y6 = info.Request.EmptyRoomLiveTime;
                var y7 = info.Request.GameId;
                var y8 = info.Request.GameProperties;
                var y9 = info.Request.JoinMode;
                var y10 = info.Request.LobbyName;
                var y11 = info.Request.LobbyType;
                var y12 = info.Request.SuppressRoomEvents;




                var ac = info.AuthCookie;
                var x = info.Nickname;
#pragma warning disable 618
                var x1 = info.AuthResultsToken;
#pragma warning restore 618
                var x2 = info.CreateIfNotExists;
                var x3 = info.CreateOptions;
                var x4 = info.IsJoin;

                #endregion

                var callInfo = (ICallInfo)info;

                #region IBeforeCloseGameCallInfo

                var closeGameInfo = (IBeforeCloseGameCallInfo)callInfo;
                var bcgx1 = closeGameInfo.FailedOnCreate;
                var bcgy1 = closeGameInfo.Request.EmptyRoomTTL;

                #endregion

                #region ICloseGameCallInfo

                var closeGameCallInfo = (ICloseGameCallInfo)callInfo;
                var cgx1 = closeGameCallInfo.ActorCount;
                var cgx2 = closeGameCallInfo.FailedOnCreate;
                var cgy1 = closeGameCallInfo.Request.EmptyRoomTTL;

                #endregion

                #region IBeforeSetPropertiesCallInfo

                var beforeSetPropCallInfo = (IBeforeSetPropertiesCallInfo)callInfo;
                var bfspx1 = beforeSetPropCallInfo.ActorNr;
                var bfspy1 = beforeSetPropCallInfo.Request.ActorNumber;
                var bfspy2 = beforeSetPropCallInfo.Request.Broadcast;
                var bfspy3 = beforeSetPropCallInfo.Request.ExpectedValues;
                var bfspy4 = beforeSetPropCallInfo.Request.HttpForward;
                var bfspy5 = beforeSetPropCallInfo.Request.Properties;


                beforeSetPropCallInfo.Cancel();
                // we do not check obsolete methods
                //beforeSetPropCallInfo.Defer();

                #endregion

                #region ISetPropertiesCallInfo

                var setPropCallInfo = (ISetPropertiesCallInfo)callInfo;

                var spx1 = setPropCallInfo.ActorNr;
                var spy1 = setPropCallInfo.Request.Properties;
                var spy2 = setPropCallInfo.Request.ActorNumber;
                var spy3 = setPropCallInfo.Request.Broadcast;
                var spy4 = setPropCallInfo.Request.ExpectedValues;
                var spy5 = setPropCallInfo.Request.HttpForward;

                #endregion

                #region IBeforeJoinGameCallInfo

                var beforeJoinGameCallInfo = (IBeforeJoinGameCallInfo)callInfo;
                var bjgac = beforeJoinGameCallInfo.AuthCookie;
                var bjgx0 = beforeJoinGameCallInfo.Nickname;
                var bjgx2 = beforeJoinGameCallInfo.UserId;
                var bjgy1 = beforeJoinGameCallInfo.Request.ActorNr;
                var bjgy2 = beforeJoinGameCallInfo.Request.ActorProperties;
                var bjgy3 = beforeJoinGameCallInfo.Request.BroadcastActorProperties;
                var bjgy4 = beforeJoinGameCallInfo.Request.CreateIfNotExists;
                var bjgy5 = beforeJoinGameCallInfo.Request.DeleteCacheOnLeave;
                var bjgy6 = beforeJoinGameCallInfo.Request.EmptyRoomLiveTime;
                var bjgy7 = beforeJoinGameCallInfo.Request.GameId;
                var bjgy8 = beforeJoinGameCallInfo.Request.GameProperties;
                var bjgy9 = beforeJoinGameCallInfo.Request.JoinMode;
                var bjgy10 = beforeJoinGameCallInfo.Request.LobbyName;
                var bjgy11 = beforeJoinGameCallInfo.Request.LobbyType;
                var bjgy12 = beforeJoinGameCallInfo.Request.SuppressRoomEvents;


                #endregion

                #region IJoinGameCallInfo

                var joinCallInfo = (IJoinGameCallInfo)callInfo;

                var jcx1 = joinCallInfo.ActorNr;
                var jcx2 = joinCallInfo.JoinParams;
                var jcx21 = jcx2.PublishCache;
                var jcx22 = jcx2.PublishJoinEvents;
                var jcx23 = jcx2.ResponseExtraParameters;
                var jcy1 = joinCallInfo.Request.ActorNr;
                var jcy2 = joinCallInfo.Request.ActorProperties;
                var jcy3 = joinCallInfo.Request.BroadcastActorProperties;
                var jcy4 = joinCallInfo.Request.CreateIfNotExists;
                var jcy5 = joinCallInfo.Request.DeleteCacheOnLeave;
                var jcy6 = joinCallInfo.Request.EmptyRoomLiveTime;
                var jcy7 = joinCallInfo.Request.GameId;
                var jcy8 = joinCallInfo.Request.GameProperties;
                var jcy9 = joinCallInfo.Request.JoinMode;
                var jcy10 = joinCallInfo.Request.LobbyName;
                var jcy11 = joinCallInfo.Request.LobbyType;
                var jcy12 = joinCallInfo.Request.SuppressRoomEvents;
                var jcy13 = joinCallInfo.Request.RoomFlags;
                var jcy14 = joinCallInfo.Request.PlayerTTL;

                #endregion

                #region ILeaveGameCallInfo

                var leaveCallInfo = (ILeaveGameCallInfo)callInfo;

                var lx1 = leaveCallInfo.IsInactive;
                var lx2 = leaveCallInfo.ActorNr;
                var lx3 = leaveCallInfo.Details;
                var lx4 = leaveCallInfo.Reason;
                var ly1 = leaveCallInfo.Request.IsCommingBack;

                #endregion

                #region IRaiseEventCallInfo

                var raiseEventCallInfo = (IRaiseEventCallInfo)callInfo;

                var rex1 = raiseEventCallInfo.ActorNr;
                var rey1 = raiseEventCallInfo.Request.HttpForward;
                var rey2 = raiseEventCallInfo.Request.Actors;
                var rey3 = raiseEventCallInfo.Request.Cache;
                var rey4 = raiseEventCallInfo.Request.CacheSliceIndex;
                var rey5 = raiseEventCallInfo.Request.Data;
                var rey6 = raiseEventCallInfo.Request.EvCode;
                var rey7 = raiseEventCallInfo.Request.GameId;
                var rey8 = raiseEventCallInfo.Request.Group;
                var rey9 = raiseEventCallInfo.Request.ReceiverGroup;
                #endregion

                #region IPluginHost

                var ph1 = this.PluginHost.GameId;
                var ph2 = this.PluginHost.CustomGameProperties;
                var ph3 = this.PluginHost.Environment;
                var ph4 = this.PluginHost.GameActors;
                var ph5 = this.PluginHost.GameActorsActive;
                var ph6 = this.PluginHost.GameActorsInactive;
                var ph7 = this.PluginHost.GameProperties;
                var ph8 = this.PluginHost.MasterClientId;

                this.PluginHost.BroadcastErrorInfoEvent("xx", info);
                this.PluginHost.BroadcastErrorInfoEvent("xx");

                this.PluginHost.BroadcastEvent(null, 0, 0, null, 0);
                this.PluginHost.BroadcastEvent(0, 0, 0, 0, null, 0);

                this.PluginHost.CreateOneTimeTimer(info, null, 0);
                this.PluginHost.CreateTimer(null, 0, 0);

                var ver = this.PluginHost.GetEnvironmentVersion();
                var ver1 = ver.BuiltWithVersion;
                var ver2 = ver.HostVersion;

                var state = this.PluginHost.GetSerializableGameState();

                //var state00 = state.PublishUserId;
                var state01 = state.ExcludedActors;
                var state02 = state.DebugInfo;
                var state03 = state.Slice;
                //var state04 = state.SuppressRoomEvents;
                var state05 = state.PlayerTTL;
                var state06 = state.MaxPlayers;
                var state07 = state.LobbyProperties;
                var state08 = state.LobbyType;
                var state09 = state.LobbyId;
                var state10 = state.IsVisible;
                var state11 = state.IsOpen;
                var state12 = state.EmptyRoomTTL;
                //var state13 = state.DeleteCacheOnLeave;
                var state14 = state.CustomProperties;
                //var state15 = state.CheckUserOnJoin;
                var state16 = state.Binary;
                var state17 = state.ActorList;
                var state18 = state.ActorCounter;
                var state19 = state.ExpectedUsers;

                var excludedActor = state01[0];

                var ea1 = excludedActor.UserId;
                var ea2 = excludedActor.Reason;

                var serializableActor = state.ActorList[0];

                var sa0 = serializableActor.ActorNr;
                var sa1 = serializableActor.UserId;
                var sa2 = serializableActor.Nickname;
                var sa3 = serializableActor.IsActive;
                var sa4 = serializableActor.Binary;
                var sa5 = serializableActor.DeactivationTime;
                var sa6 = serializableActor.DEBUG_BINARY;

                var actor = this.PluginHost.GameActorsActive[0];

                var actor0 = actor.ActorNr;
                var actor1 = actor.Properties;
                var actor2 = actor.UserId;
                var actor3 = actor.Nickname;
                var actor4 = actor.IsActive;
                var actor5 = actor.Secure;

                this.PluginHost.HttpRequest(new HttpRequest(), info);
                this.PluginHost.LogDebug("");
                this.PluginHost.LogError("");
                this.PluginHost.LogFatal("");
                this.PluginHost.LogInfo("");
                this.PluginHost.LogWarning("");
                this.PluginHost.RemoveActor(0, null);
                this.PluginHost.RemoveActor(0, 0, null);
                this.PluginHost.SetGameState(state);
                this.PluginHost.SetProperties(0, null, null, false);
                this.PluginHost.StopTimer(null);
                this.PluginHost.TryRegisterType(null, 0, null, null);

                this.PluginHost.Enqueue(() => { });
                string errorStr;
                this.PluginHost.ExecuteCacheOperation(new CacheOp(0, 0), out errorStr);
                var logger = this.PluginHost.CreateLogger("xx");

                #region IPluginLogger

                var logger1 = logger.Name;
                var logger2 = logger.IsInfoEnabled;
                var logger3 = logger.IsFatalEnabled;
                var logger4 = logger.IsErrorEnabled;
                var logger5 = logger.IsDebugEnabled;
                var logger6 = logger.IsWarnEnabled;
                logger.Debug("");
                logger.Debug("", new Exception());
                logger.DebugFormat("{0}", 1);
                logger.DebugFormat(new Formater(), "{0}", 1);
                logger.Error("", new Exception());
                logger.Error("");
                logger.ErrorFormat(new Formater(), "{0}", 1);
                logger.ErrorFormat("{0}", 1);
                logger.Fatal("");
                logger.Fatal("", new Exception());
                logger.FatalFormat("{0}", 1);
                logger.FatalFormat(new Formater(), "{0}", 1);
                logger.Info("");
                logger.Info("", new Exception());
                logger.InfoFormat(new Formater(), "{0}", 1);
                logger.InfoFormat("{0}", 1);
                logger.Warn("", new Exception());
                logger.Warn("");
                logger.WarnFormat(new Formater(), "{0}", 1);
                logger.WarnFormat("{0}", 1);

                #endregion

                #region CacheOp

                var cacheOp = new CacheOp();

                var caop1 = cacheOp.ActorNr;
                var caop2 = cacheOp.Actors;
                var caop3 = cacheOp.Data;
                var caop4 = cacheOp.CacheOperation;
                var caop5 = cacheOp.EventCode;
                var caop6 = cacheOp.SliceIndex;
                var caop7 = cacheOp.Target;



                #endregion


                #endregion

                #region IHttpResponse

                var o = new object();
                var resp = (IHttpResponse)o;
                var resp0 = resp.HttpCode;
                var resp1 = resp.Reason;
                var resp2 = resp.Request;
                var resp3 = resp.ResponseData;
                var resp4 = resp.ResponseText;
                var resp5 = resp.Status;
                var resp6 = resp.WebStatus;
                var resp7 = resp.CallInfo;
                var resp8 = resp.Headers;

                #endregion

                #region HttpRequest

                var req = new HttpRequest();
                var req0 = req.Accept;
                var req1 = req.Callback;
                var req2 = req.ContentType;
                var req3 = req.DataStream;
                var req4 = req.Headers;
                var req5 = req.CustomHeaders;
                var req6 = req.Method;
                var req7 = req.Url;
                var req8 = req.UserState;
                var req9 = req.Async;

                #endregion

                #region Property

                var property = new Property<object>(1, 1);

                var prop1 = property.Key;
                var prop2 = property.Value;
                property.PropertyChanged += (sender, args) => { };

                #endregion

                #region PropertyBag

                var propertyBag = new PropertyBag<object>();
                var propBag1 = propertyBag.Count;
                var propBag2 = propertyBag.AsDictionary();
                var propBag3 = propertyBag.GetAll();
                var propBag4 = propertyBag.GetProperties();
                var propBag5 = propertyBag.GetProperties(new List<object>());
                var propBag6 = propertyBag.GetProperties((IEnumerable<object>)new List<object>());
                var propBag7 = propertyBag.GetProperties((IEnumerable)new List<object>());
                var propBag9 = propertyBag.GetProperty(1);
                propertyBag.Set(o, 1);
                propertyBag.SetProperties((IDictionary)new Dictionary<object, object>());
                propertyBag.SetProperties((IDictionary<object, object>)new Dictionary<object, object>());

                propertyBag.SetPropertiesCAS((IDictionary)new Dictionary<object, object>(), (IDictionary)new Dictionary<object, object>(), out errorStr);
                propertyBag.SetPropertiesCAS((IDictionary<object, object>)new Dictionary<object, object>(), (IDictionary<object, object>)new Dictionary<object, object>(), out errorStr);

                if (propertyBag.TryGetValue(1, out o))
                { }


                propertyBag.PropertyChanged += (sender, args) => { };

                propertyBag.DeleteNullProps = true;

                #endregion

            }

            var request = new HttpRequest
            {
                Async = true,
                Callback = (response, state) =>
                {
                    var x = response.Request.Async;
                    var y = response.Status == HttpRequestQueueResult.Success;

                },
                Url = this.pathHook + "?method=OnCreateGame",
            };

            this.PluginHost.HttpRequest(request, info);

            base.OnCreateGame(info);
        }

        #endregion
    }
}
