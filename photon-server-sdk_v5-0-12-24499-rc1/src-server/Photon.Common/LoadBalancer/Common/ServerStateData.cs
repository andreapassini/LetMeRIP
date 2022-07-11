using System;
using System.Collections.Generic;
using Photon.Common.LoadBalancer.LoadShedding;

namespace Photon.Common.LoadBalancer.Common
{
    public class ServerStateData<TServer>
    {
        [Flags]
        enum ServerStateFlags
        {
            /// <summary>
            /// Normal server. nothing special
            /// </summary>
            Normal,
            /// <summary>
            /// Server which is treated to be used later when load goes up
            /// </summary>
            IsReserved,
            /// <summary>
            /// Server which is reserved and not used.
            /// </summary>
            IsInReserve,
        }

        #region .flds and consts

        private ServerStateFlags serverStateFlags;

        #endregion

        #region .ctr

        public ServerStateData(TServer server)
        {
            this.Server = server;
        }

        #endregion

        public TServer Server { get; private set; }

        public FeedbackLevel LoadLevel { get; set; }

        public int Weight { get; set; }

        public byte Priority { get; set; }

        public LinkedListNode<ServerStateData<TServer>> Node { get; set; }

        public ServerState ServerState { get; set; }

        public bool IsInAvailableList
        {
            get { return this.Node != null; }
        }

        public bool IsReserved
        {
            get { return this.serverStateFlags.HasFlag(ServerStateFlags.IsReserved); }
            set
            {
                if (value)
                {
                    this.serverStateFlags |= ServerStateFlags.IsReserved;
                }
                else
                {
                    this.serverStateFlags &= ~ServerStateFlags.IsReserved;
                }
            }
        }

        public bool IsInReserve
        {
            get { return this.serverStateFlags.HasFlag(ServerStateFlags.IsInReserve); }
            set
            {
                if (value)
                {
                    this.serverStateFlags |= ServerStateFlags.IsInReserve;
                }
                else
                {
                    this.serverStateFlags &= ~ServerStateFlags.IsInReserve;
                }
            }
        }

        public void MarkReserved(bool reserved = true)
        {
            if (reserved)
            {
                this.serverStateFlags |= ServerStateFlags.IsReserved | ServerStateFlags.IsInReserve;
            }
            else
            {
                this.serverStateFlags &= ~(ServerStateFlags.IsReserved | ServerStateFlags.IsInReserve);
            }
        }

        public override string ToString()
        {
            return string.Format("[Server:{0}, State:{1}, Load:{2}, Prio:{3}, Weight:{4}]", 
                this.Server, this.ServerState, this.LoadLevel, this.Priority, this.Weight);
        }
    }
}