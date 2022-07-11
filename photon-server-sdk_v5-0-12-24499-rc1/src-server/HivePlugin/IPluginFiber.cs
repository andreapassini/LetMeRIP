using System;

namespace Photon.Hive.Plugin
{
    public static class EnqueueStatus
    {
        public const int Success = 0;
        public const int Closed = 1;
    }

    public interface IPluginFiber
    {
        int Enqueue(Action action);
        object CreateTimer(Action action, int firstInMs, int regularInMs);
        [Obsolete]
        object CreateOneTimeTimer(Action action, long firstInMs);
        object CreateOneTimeTimer(Action action, int firstInMs);
        void StopTimer(object timer);
    }
}
