using System.Collections;
using Photon.Hive.Plugin;

namespace TestPlugins
{
    class CacheOpPlugin : TestPluginBase
    {
        private IPluginLogger log;
        public override void OnRaiseEvent(IRaiseEventCallInfo info)
        {
            if (this.log == null)
            {
                this.log = this.PluginHost.CreateLogger(this.Name);
            }

            this.log.DebugFormat("Raise event code {0}", info.Request.EvCode);

            string errorMsg;
            if (info.Request.EvCode == 1)
            {
                var cacheOp = new CacheOp(CacheOperations.AddToRoomCache, 0, 255, new Hashtable { {"x", "y"}});
                if (!this.PluginHost.ExecuteCacheOperation(cacheOp, out errorMsg))
                {
                    this.PluginHost.BroadcastErrorInfoEvent(errorMsg, info, new SendParameters());
                }
            }
            else if (info.Request.EvCode == 2)
            {
                // add event from this actor
                var cacheOp = new CacheOp(CacheOperations.AddToRoomCache, 0, 252, new Hashtable { { "a", "v" } })
                {
                    ActorNr = info.ActorNr
                };
                if (!this.PluginHost.ExecuteCacheOperation(cacheOp, out errorMsg))
                {
                    this.PluginHost.BroadcastErrorInfoEvent(errorMsg, info, new SendParameters());
                }
            }
            else if (info.Request.EvCode == 3)
            {
                this.log.DebugFormat("if for code {0} performed", info.Request.EvCode);
                // remove all for actors who left game
                var cacheOp = new CacheOp(CacheOperations.RemoveFromCacheForActorsLeft);
                if (!this.PluginHost.ExecuteCacheOperation(cacheOp, out errorMsg))
                {
                    this.PluginHost.BroadcastErrorInfoEvent(errorMsg, info, new SendParameters());
                }
            }
            else if (info.Request.EvCode == 4)
            {
                // remove all from actor 1
                var cacheOp = new CacheOp(CacheOperations.RemoveFromRoomCache, new int[] {1}, 0, null);
                if (!this.PluginHost.ExecuteCacheOperation(cacheOp, out errorMsg))
                {
                    this.PluginHost.BroadcastErrorInfoEvent(errorMsg, info, new SendParameters());
                }
            }
            else if (info.Request.EvCode == 5)
            {
                // remove all
                var cacheOp = new CacheOp(CacheOperations.RemoveFromRoomCache, null, 0, null);
                if (!this.PluginHost.ExecuteCacheOperation(cacheOp, out errorMsg))
                {
                    this.PluginHost.BroadcastErrorInfoEvent(errorMsg, info, new SendParameters());
                }
            }


            base.OnRaiseEvent(info);
        }
    }
}
