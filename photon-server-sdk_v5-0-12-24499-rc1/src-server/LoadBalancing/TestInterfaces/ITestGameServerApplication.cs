namespace LoadBalancing.TestInterfaces
{
    public interface ITestGameServerApplication
    {
        bool WaitGameDisposed(string gameName, int timeout);
        void SetGamingTcpPort(int port);
        int PeerCount { get; }
        int RoomOptionsAndFlags { get; set; }
        int RoomOptionsOrFlags { get; set; }
    }
}
