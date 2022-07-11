using Photon.Plugins.Common;

namespace Photon.Common.Plugins
{
    public interface ICallInfoImpl : ICallInfo
    {
        void InternalDefer();
        void Pause();
        void Reset();
    }
}
