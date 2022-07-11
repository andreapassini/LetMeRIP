using System;
using System.Collections.Generic;
using System.Linq;
using ExitGames.Logging;
using Photon.Common.LoadBalancer.Common;
using Photon.Common.LoadBalancer.LoadShedding;
using Photon.SocketServer.Annotations;

namespace Photon.Common.LoadBalancer.Priorities
{
    internal class ServerBunch<TServer>  where TServer : IComparable<TServer>
    {
        #region Constants and fields

        // ReSharper disable StaticFieldInGenericType
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();
        // ReSharper restore StaticFieldInGenericType

        // dictionary for fast server instance lookup
        private readonly SortedDictionary<TServer, ServerStateData<TServer>> serverList;

        // stores the sum of the load levels of all server instances
        // used to calculate the average load level
        private int totalWorkload;

        private readonly float reserveRatio;

        private int reservedServersCount;

        private int serversInUseWorkload;

        private readonly int takeFromReserveLoadLevel;
        private int serversUsedFromReserveCount;

        #endregion

        #region .ctr

        public ServerBunch(byte priority, float reserveRatio, FeedbackLevel takeFromReserveLevel = FeedbackLevel.Highest)
        {
            this.reserveRatio = reserveRatio;
            this.Priority = priority;
            this.takeFromReserveLoadLevel = (int)takeFromReserveLevel;
            this.serverList = new SortedDictionary<TServer, ServerStateData<TServer>>();
        }

        #endregion

        #region Properties
        [PublicAPI]
        public byte Priority { get; private set; }

        public FeedbackLevel AverageWorkload
        {
            get
            {
                if (this.serverList.Count == 0)
                {
                    return 0;
                }

                return (FeedbackLevel)(int)Math.Round((double)this.totalWorkload / this.serverList.Count);
            }
        }

        public int ServersCount
        {
            get { return this.serverList.Count; }
        }

        public int ServersInUseAverageWorkload
        {
            get
            {
                if (this.serverList.Count == 0)
                {
                    return 0;
                }

                return (int)Math.Round((double)this.serversInUseWorkload/(this.serverList.Count - this.reservedServersCount + this.serversUsedFromReserveCount));
            }
        }

        public int ReturnToReserveThreshold { get; private set; }

        public int ServersUsedFromReserveCount
        {
            get { return this.serversUsedFromReserveCount; }
        }

        public int ReservedServersCount
        {
            get { return this.reservedServersCount; }
        }
        #endregion

        #region Publics

        public ServerStateData<TServer> TryAddServer(TServer server, FeedbackLevel loadLevel, int loadLevelWeight)
        {
            // check if the server instance was already added
            if (this.serverList.ContainsKey(server))
            {
                log.WarnFormat("LoadBalancer already contains server {0}", server);
                return null;
            }

            var serverState = new ServerStateData<TServer>(server)
            {
                LoadLevel = loadLevel,
                Weight = loadLevelWeight,
                Priority = this.Priority
            };

            this.serverList.Add(server, serverState);

            // we add new server to reserve only when it should be increased by 1
            // let's say we had 9 server and ratio is 0.2, then reserve will contain 1 server
            // we add new server. 10*0.2 == 2. 2 - reservedServersCount(1) == 1. so we increase amount of reserved servers
            if (this.reserveRatio * this.serverList.Count - this.reservedServersCount >= 1.0)
            {
                serverState.MarkReserved();

                if (log.IsDebugEnabled)
                {
                    log.Debug($"Server marked as reserved and is put to reserve. Server={serverState}");
                }

                ++this.reservedServersCount;
            }

            this.UpdateServerReturnThreshold();
            return serverState;
        }

        public bool TryGetServer(TServer server, out ServerStateData<TServer> serverState)
        {
            return this.serverList.TryGetValue(server, out serverState);
        }

        public void RemoveServer(ServerStateData<TServer> server, out ServerStateData<TServer> fromReserve)
        {
            if (log.IsDebugEnabled)
            {
                log.Debug($"Removing server. Server={server}");
            }

            this.serverList.Remove(server.Server);
            fromReserve = null;
            if (server.IsReserved)
            {
                --this.reservedServersCount;
                if (!server.IsInReserve)
                {
                    --this.serversUsedFromReserveCount;
                }

                // we take first not reserved server to mark it as reserved if we need to increase reserve. 
//                var lastNotReserved = this.serverList.LastOrDefault(s => !s.Value.IsReserved && s.Value.IsInAvailableList);
                var lastNotReserved = this.serverList.LastOrDefault(s => !s.Value.IsReserved);
                if (this.reserveRatio * this.serverList.Count - this.reservedServersCount >= 1.0)
                {
                    // All not reserved servers are in available list of LoadBalancer
                    // so we do not return it through fromReserve 

                    lastNotReserved.Value.IsReserved = true;
                    ++this.reservedServersCount;
                    //this server were in available list. so, we think it is used
                    ++this.serversUsedFromReserveCount;
                    if (log.IsDebugEnabled)
                    {
                        log.Debug($"Server marked as reserved. Server={lastNotReserved}");
                    }
                }
            }
            else if (this.reserveRatio * this.serverList.Count - this.reservedServersCount < 0.0)
            {
                var serverState = this.serverList.FirstOrDefault(s => s.Value.IsReserved).Value;
                if (serverState != null)
                {
                    // we managed to find reserved (not involved/used) server in reserve
                    serverState.MarkReserved(false);
                    fromReserve = serverState;
                    if (log.IsDebugEnabled)
                    {
                        log.Debug($"Server is not marked as reserved anymore and taken out of reserve. Server={serverState}");
                    }

                }
                --this.reservedServersCount;
            }
            this.UpdateServerReturnThreshold();
        }

        public IEnumerable<ServerStateData<TServer>> GetServers()
        {
            return this.serverList.Values;
        }

        public ServerStateData<TServer> GetServerFromReserve()
        {
            if (this.reservedServersCount == 0)
            {
                return null;
            }
            var  result = this.serverList.FirstOrDefault(s => s.Value.IsInReserve).Value;
            if (result != null)
            {
                ++this.serversUsedFromReserveCount;
                this.serversInUseWorkload += (int)result.LoadLevel;
                result.IsInReserve = false;
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("Server is taken from reserve. Server:{0}, servers count:{1}, Reserved:{2}, in Reserve:{3}",
                        result, this.serverList.Count, this.reservedServersCount, this.reservedServersCount - this.serversUsedFromReserveCount);
                }
            }

            return result;
        }

        public void ReturnServerIntoReserve(ServerStateData<TServer> server)
        {
            server.IsInReserve = true;
            --this.serversUsedFromReserveCount;
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Server is taken from reserve. Server:{0}, servers count:{1}, Reserved:{2}, in Reserve:{3}",
                    server, this.serverList.Count, this.reservedServersCount, this.reservedServersCount - this.serversUsedFromReserveCount);
            }

            this.serversInUseWorkload -= (int)server.LoadLevel;
        }

        public ServerStateData<TServer> GetFirstReservedServerInUsage()
        {
            if (this.serversUsedFromReserveCount == 0)
            {
                return null;
            }

            return this.serverList.FirstOrDefault(s => s.Value.IsReserved && s.Value.IsInAvailableList).Value;
        }

        public void UpdateTotalWorkload(ServerStateData<TServer> server, FeedbackLevel oldLoadLevel, FeedbackLevel newLoadLevel)
        {
            this.totalWorkload -= (int)oldLoadLevel;
            this.totalWorkload += (int)newLoadLevel;

            if (!server.IsInReserve)
            {
                this.serversInUseWorkload -= (int) oldLoadLevel;
                this.serversInUseWorkload += (int) newLoadLevel;
            }
        }

        public ServerStateData<TServer> UpdateReserve(ServerStateData<TServer> server)
        {
            if (server.IsReserved)
            {
                return null;
            }

            var reserve = this.serverList.Where(s => s.Value.IsReserved);
            var firstInReserve = reserve.FirstOrDefault().Value;
            if (firstInReserve == null || server.Server.CompareTo(firstInReserve.Server) == -1)
            {
                return null;
            }

            server.MarkReserved();
            firstInReserve.MarkReserved(false);

            if (log.IsDebugEnabled)
            {
                log.Debug($"Server={server} replaced '{firstInReserve}'");
            }

            return firstInReserve;
        }
        #endregion

        #region Methods

        private void UpdateServerReturnThreshold()
        {
            if (this.reservedServersCount == 0)
            {

                this.ReturnToReserveThreshold = this.takeFromReserveLoadLevel - 1;
            }

            var serversCount = this.serverList.Count - this.reservedServersCount;

            // we calculate what average load we will have if we will return one server to reserve. 
            this.ReturnToReserveThreshold = (int)Math.Round( (double)this.takeFromReserveLoadLevel * serversCount/(serversCount + 1));
            this.ReturnToReserveThreshold -= 1;
        }
        #endregion
    }
}
