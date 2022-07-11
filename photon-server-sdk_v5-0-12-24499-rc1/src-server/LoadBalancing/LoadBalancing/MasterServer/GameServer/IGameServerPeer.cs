using ExitGames.Concurrency.Fibers;
using Photon.SocketServer;

namespace Photon.LoadBalancing.MasterServer.GameServer
{
    public interface IGameServerPeer
    {
        IRpcProtocol Protocol { get; }

        IFiber RequestFiber { get; }

        string RemoteIP { get; }

        int RemotePort { get; }

        void AttachToContext(GameServerContext context);
        void DettachFromContext();

        void Disconnect(int disconnectError);
    }
}
