using System;
using System.Collections.Generic;
using System.Diagnostics;
using ExitGames.Concurrency.Fibers;
using ExitGames.Logging;
using Photon.LoadBalancing.Operations;
using Photon.LoadBalancing.ServerToServer.Events;
using Photon.SocketServer;
using Photon.SocketServer.Rpc;

namespace Photon.LoadBalancing.GameServer
{
    public class GameUpdatesBatchEvent : DataContract
    {
        #region .ctr & .dtr

        public GameUpdatesBatchEvent(IRpcProtocol protocol, IEventData eventData)
            : base(protocol, eventData.Parameters)
        {
        }

        #endregion

        [DataMember(Code = (byte)ParameterCode.ApplicationId, IsOptional = true)]
        public string ApplicationId { get; set; }

        [DataMember(Code = (byte)ParameterCode.AppVersion, IsOptional = true)]
        public string ApplicationVersion { get; set; }

        [DataMember(Code = (byte)ServerParameterCode.GameUpdatesBatch, IsOptional = false)]
        public Dictionary<byte, object>[] BatchedEvents { get; set; }
    }

    public struct GameUpdatesBatcherParams
    {
        public string ApplicationId;
        public string ApplicationVersion;
        public MasterServerConnection MasterServerConnection;
        public IFiber Fiber;
        public bool UseBatcher;
        public int MaxUpdatesToBatch;
        public int BatchPeriod;
    }

    public class GameUpdatesBatcher : IDisposable
    {

        #region .flds

        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private readonly MasterServerConnection masterConnection;

        private IFiber fiber;

        private IDisposable scheduler;

        private long lastSendTime;

        private readonly Dictionary<byte, object> batchEvent = new Dictionary<byte, object>();
        private readonly List<Dictionary<byte, object>> batchedEvents;

        private readonly bool useBatcher;

        private readonly int maxUpdatesToBatch;

        private readonly int batchPeriod;

        private readonly string applicationId;
        private readonly string applicationVersion;
        #endregion

        #region .ctr

        public GameUpdatesBatcher(GameUpdatesBatcherParams parameters)
        {
            this.applicationId = parameters.ApplicationId;
            this.applicationVersion = parameters.ApplicationVersion;

            this.batchEvent.Add((byte)ParameterCode.ApplicationId, parameters.ApplicationId);
            this.batchEvent.Add((byte)ParameterCode.AppVersion, parameters.ApplicationVersion);

            this.masterConnection = parameters.MasterServerConnection;
            this.fiber = parameters.Fiber;
            this.useBatcher = parameters.UseBatcher;

            this.batchPeriod = parameters.BatchPeriod == 0 ? 200 : parameters.BatchPeriod;
            this.maxUpdatesToBatch = parameters.MaxUpdatesToBatch == 0 ? 1000 : parameters.MaxUpdatesToBatch;
            this.batchedEvents = new List<Dictionary<byte, object>>(this.maxUpdatesToBatch);
        }

        ~GameUpdatesBatcher()
        {
            this.Dispose();
            GC.SuppressFinalize(this);
        }

        #endregion

        #region .publics

        public void SendGameUpdate(UpdateGameEvent updateGameEvent)
        {
            if (!this.useBatcher)
            {
                SendUpdateDirectly(updateGameEvent);
                return;
            }

            lock(this.batchEvent)
            {
                var now = Stopwatch.GetTimestamp();

                var tsp = new TimeSpan(now - this.lastSendTime);
                if (tsp.TotalMilliseconds > this.batchPeriod)
                {
                    if (log.IsDebugEnabled)
                    {
                        log.DebugFormat("Message was sent directly");
                    }
                    this.SendUpdateDirectly(updateGameEvent);
                }
                else
                {
                    if (log.IsDebugEnabled)
                    {
                        log.DebugFormat("Message was batched");
                    }
                    this.SendUpdateBatched(updateGameEvent);
                }

                this.lastSendTime = now;
            }
        }

        public void OnRemoveGame()
        {
            this.Flush();
        }

        public void Dispose()
        {
            this.fiber = null;

            var s = this.scheduler;
            if (s != null)
            {
                s.Dispose();
                this.scheduler = null;
            }
        }
        #endregion

        #region .privates

        private void SendUpdateDirectly(UpdateGameEvent updateGameEvent)
        {
            var eventData = new EventData((byte)ServerEventCode.UpdateGameState, updateGameEvent);
            this.masterConnection.SendEventIfRegistered(eventData, new SendParameters());
        }

        private void SendUpdateBatched(UpdateGameEvent updateGameEvent)
        {
            var dict = updateGameEvent.ToDictionary();

            dict.Remove((byte) ParameterCode.AppVersion);
            dict.Remove((byte) ParameterCode.ApplicationId);

            this.batchedEvents.Add(dict);
            if (this.batchedEvents.Count >= this.maxUpdatesToBatch)
            {
                this.Flush();
                return;
            }

            this.ScheduleSending();
        }

        private void ScheduleSending()
        {
            if (this.scheduler != null)
            {
                return;
            }

            this.scheduler = this.fiber.Schedule(this.SendScheduledBatch, this.batchPeriod);
        }

        private void SendScheduledBatch()
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("SendScheduledBatch for appId:{0}, version:{1}", this.applicationId, this.applicationVersion);
            }

            lock (this.batchEvent)
            {
                if (this.scheduler == null)//that means that Flush was already called and we do not have to send anything
                {
                    return;
                }

                this.scheduler.Dispose();
                this.scheduler = null;

                this.SendBatch();
            }
        }

        private void SendBatch()
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Sending Batch for appId:{0}, version:{1}", this.applicationId, this.applicationVersion);
            }

            this.batchEvent[(byte)ServerParameterCode.GameUpdatesBatch] = this.batchedEvents.ToArray();
            var eventData = new EventData((byte)ServerEventCode.GameUpdatesBatch, this.batchEvent);

            this.masterConnection.SendEventIfRegistered(eventData, new SendParameters());
            this.batchedEvents.Clear();
        }

        private void Flush()
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Flush for appId:{0}, version:{1}", this.applicationId, this.applicationVersion);
            }

            if (this.scheduler != null)
            {
                this.scheduler.Dispose();
                this.scheduler = null;
            }

            if (this.batchedEvents.Count != 0)
            {
                this.SendBatch();
            }
        }

        #endregion

    }
}
