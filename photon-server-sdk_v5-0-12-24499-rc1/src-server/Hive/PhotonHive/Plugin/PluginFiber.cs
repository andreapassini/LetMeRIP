using System;
using ExitGames.Concurrency.Fibers;

namespace Photon.Hive.Plugin
{
    public class PluginFiber : IPluginFiber
    {
        #region .flds

        private PoolFiber fiber;

        #endregion

        #region .ctr

        public PluginFiber(PoolFiber fiber)
        {
            this.fiber = fiber;
        }

        #endregion

        #region .properties

        public bool IsClosed { get; set; }

        #endregion

        #region IPluginFiber

        public int Enqueue(Action action)
        {
            if (this.IsClosed)
            {
                return EnqueueStatus.Closed;
            }

            this.fiber.Enqueue(action);
            return EnqueueStatus.Success;
        }

        public object CreateTimer(Action action, int firstInMs, int regularInMs)
        {
            return this.fiber.ScheduleOnInterval(action, firstInMs, regularInMs);
        }

        [Obsolete("as of 2021.03.09")]
        public object CreateOneTimeTimer(Action action, long firstInMs)
        {
            return this.fiber.Schedule(action, (int)firstInMs);
        }

        public object CreateOneTimeTimer(Action action, int firstInMs)
        {
            return this.fiber.Schedule(action, (int)firstInMs);
        }

        public void StopTimer(object timer)
        {
            if (timer is IDisposable d)
            {
                d.Dispose();
            }
        }

        #endregion
    }
}
