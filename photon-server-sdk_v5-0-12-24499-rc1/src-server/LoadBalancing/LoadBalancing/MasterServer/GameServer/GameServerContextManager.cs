using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ExitGames.Concurrency.Fibers;
using ExitGames.Logging;

using Photon.LoadBalancing.ServerToServer.Operations;

namespace Photon.LoadBalancing.MasterServer.GameServer
{
    public class GameServerContextManager
    {
        #region Sub Classes

        protected class ContextKeeper
        {
            public GameServerContext Context { get; set; }
            public IDisposable DisposeTimer { get; set; }

            public void KillDisposeTimer()
            {
                var timer = this.DisposeTimer;
                if (timer != null)
                {
                    if (log.IsDebugEnabled)
                    {
                        log.Debug("Disposing destroy timer");
                    }
                    timer.Dispose();
                    this.DisposeTimer = null;
                }
            }
        }

        #endregion

        #region Fields and Constants

        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        protected readonly Dictionary<string, ContextKeeper> gameServerContexts = new Dictionary<string, ContextKeeper>();

        private readonly PoolFiber fiber = new PoolFiber();
        private readonly MasterApplication application;
        private readonly int contextTTL;

        #endregion

        #region .ctr

        public GameServerContextManager(MasterApplication application, int contextTTL)
        {
            this.application = application;
            this.contextTTL = contextTTL;
            this.fiber.Start();
        }

        public int Count
        {
            get
            {
                lock (this.gameServerContexts)
                {
                    return this.gameServerContexts.Count;
                }
            }
        }

        #endregion

        #region Publics

        public void RegisterGameServer(IRegisterGameServer request, IGameServerPeer peer)
        {
            this.RegisterGameServer(request, peer, registerByRequest: true);
        }

        public void RegisterGameServerOnInit(IRegisterGameServer request, IGameServerPeer peer)
        {
            this.RegisterGameServer(request, peer, registerByRequest: false);
        }

        public void OnGameServerDisconnect(GameServerContext context, IGameServerPeer peer)
        {
            lock (this.gameServerContexts)
            {
                if (context.DetachPeer(peer))
                {
                    this.ScheduleContextRemoval(context, peer);
                }
            }
        }

        public void OnGameServerLeft(GameServerContext context, IGameServerPeer peer)
        {
            lock (this.gameServerContexts)
            {
                context.DetachPeerAndClose();

                this.RemoveContext(context, peer);
            }
        }

        public IGameServerPeer[] GameServerPeersToArray()
        {
            lock (this.gameServerContexts)
            {
                return this.gameServerContexts.Values.Select(x => x.Context.Peer).ToArray();
            }
        }

        #endregion

        #region Privates

        private void RegisterGameServer(IRegisterGameServer request, IGameServerPeer peer, bool registerByRequest)
        {
            var key = GameServerContext.GetKey(request);

            if (log.IsInfoEnabled)
            {
                if (registerByRequest)
                {
                    log.Info($"Registering GS by request. key:'{key}', id:'{request.ServerId}', state:{request.ServerState}, loadIndex:{request.LoadIndex}, p:{peer}");
                }
                else
                {
                    log.Info($"Registering GS by InitRequest. key:'{key}', id:'{request.ServerId}', state:{request.ServerState}, loadIndex:{request.LoadIndex}, p:{peer}");
                }
            }

            lock (this.gameServerContexts)
            {
                if (this.gameServerContexts.TryGetValue(key, out var keeper))
                {
                    keeper.KillDisposeTimer();
                    if (keeper.Context.ServerId == request.ServerId)
                    {
                        if (log.IsInfoEnabled)
                        {
                            log.InfoFormat("Context for GS found and reused. key:'{0}', id:'{1}',p:{2}", key, request.ServerId, peer);
                        }

                        keeper.Context.AttachPeerAndHandleRegisterRequest(peer, request, true);
                        return;
                    }

                    if (log.IsDebugEnabled)
                    {
                        log.DebugFormat("Context for GS found but belongs to other server." +
                                        " key:'{0}', old_id:'{1}',new_id:{2}", key, keeper.Context.ServerId, request.ServerId);
                    }

                    this.RemoveContext(keeper.Context);
                    keeper.Context.DetachPeerAndClose();
                }

                keeper = new ContextKeeper
                {
                    Context = this.CreateContext(request),
                };

                keeper.Context.AttachPeerAndHandleRegisterRequest(peer, request, false);

                this.gameServerContexts.Add(key, keeper);

                if (log.IsInfoEnabled)
                {
                    log.InfoFormat("GS is added. key:'{0}', id:'{1}',p:{2}", key, request.ServerId, peer);
                }

                this.LogoutServersList();
            }
        }

        private void LogoutServersList()
        {
            var sb = new StringBuilder();
            sb.AppendLine("GameServers List:");
            foreach (var context in this.gameServerContexts)
            {
                var srvCtx = context.Value?.Context;
                if (srvCtx != null)
                {
                    sb.AppendLine($"host_{srvCtx.AddressInfo.Hostname}, state_{srvCtx.State}, load_{srvCtx.LoadLevel}, " +
                                  $"lbPriority_{srvCtx.LoadBalancerPriority}, serverId_{srvCtx.ServerId}, key_{srvCtx.Key}, p_{(srvCtx.Peer == null ? "no peer" : "has peer")}");
                }
            }

            if (log.IsInfoEnabled)
            {
                log.Info(sb.ToString());
            }
        }

        private void ScheduleContextRemoval(GameServerContext context, IGameServerPeer peer)
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Scheduling context for context removal. context={0},p:{1}", context, peer);
            }

            if (!this.gameServerContexts.TryGetValue(context.Key, out ContextKeeper keeper))
            {
                log.WarnFormat("Scheduling removal failed to find context. key:'{0}', id:'{1}',p:{2}", context.Key, context.ServerId, peer);
                return;
            }

            if (keeper.Context == context)
            {
                keeper.DisposeTimer = this.fiber.Schedule(() => ScheduledDestroyContext(keeper), this.contextTTL);
                if (log.IsInfoEnabled)
                {
                    log.InfoFormat("Context was scheduled for removal. context={0}, p:{1}", context, peer);
                }
                return;
            }

            if (log.IsInfoEnabled)
            {
                log.InfoFormat("Removal scheduling skipped. context found but it belongs " +
                               "to different server. context4remove={0}, context in index:{1}", context, keeper.Context);
            }
        }

        private void ScheduledDestroyContext(ContextKeeper contextKeeper)
        {
            lock (this.gameServerContexts)
            {
                if (contextKeeper.DisposeTimer != null)
                {
                    this.RemoveContext(contextKeeper.Context);

                    contextKeeper.Context.CloseContext();

                    contextKeeper.KillDisposeTimer();
                }
            }
        }

        private void RemoveContext(GameServerContext context, IGameServerPeer peer = null)
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Removing context. context={0},p:{1}", context, peer);
            }

            if (this.gameServerContexts.TryGetValue(context.Key, out ContextKeeper keeper))
            {
                if (keeper.Context == context)
                {
                    this.gameServerContexts.Remove(context.Key);

                    if (log.IsInfoEnabled)
                    {
                        log.InfoFormat("Context was removed. context={0}, p:{1}", context, peer);
                    }
                    return;
                }

                if (log.IsInfoEnabled)
                {
                    log.InfoFormat("Context removal skipped. context found but it belongs " +
                                   "to different server. context4remove={0}, context in index:{1}", context, keeper.Context);
                }
            }
            else
            {
                log.WarnFormat("Removing of context failed. context={0},p:{1}", context, peer);
            }
        }

        protected virtual GameServerContext CreateContext(IRegisterGameServer request)
        {
            return new GameServerContext(this, this.application, request);
        }

        #endregion
    }
}
