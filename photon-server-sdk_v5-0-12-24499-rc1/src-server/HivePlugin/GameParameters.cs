namespace Photon.Hive.Plugin
{
    public static class GameParameters
    {
        public const byte MaxPlayers = 255;

        public const byte IsVisible = 254;
        public const byte IsOpen = 253;
        //252 and 251 are reserved
        public const byte LobbyProperties = 250;
        public const byte MasterClientId = 248;
        public const byte ExpectedUsers = 247;
        public const byte PlayerTTL = 246;
        public const byte EmptyRoomTTL = 245;
    }
}
