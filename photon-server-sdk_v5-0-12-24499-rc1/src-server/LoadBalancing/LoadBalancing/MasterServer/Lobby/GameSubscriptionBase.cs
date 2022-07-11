using System;
using System.Collections;

namespace Photon.LoadBalancing.MasterServer.Lobby
{
    public abstract class GameSubscriptionBase : IGameListSubscription
    {
        protected readonly MasterClientPeer peer;
        protected bool disposed;
        protected readonly int maxGamesCount;

        protected GameSubscriptionBase(MasterClientPeer peer, int maxGamesCount)
        {
            this.peer = peer;
            this.maxGamesCount = maxGamesCount;
        }

        ~GameSubscriptionBase()
        {
            this.Dispose(false);
            this.disposed = true;
        }

        protected abstract void Dispose(bool disposing);
        public abstract Hashtable GetGameList();

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            this.Dispose(true);
            this.disposed = true;
        }
    }
}