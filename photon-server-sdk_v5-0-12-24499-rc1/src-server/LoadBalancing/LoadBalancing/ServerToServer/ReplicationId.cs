namespace Photon.LoadBalancing.ServerToServer
{
    public static class ReplicationId
    {
        public const byte NotReplication = 0;
        public const byte Renitialization = 1;
        public const byte Replication = 2;
        public const byte ReplicationOfEmptyGame = 3;
    }
}