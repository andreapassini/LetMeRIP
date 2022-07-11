using ExitGames.Logging;

namespace Photon.Hive.Common
{
    public class Loggers
    {
        public static readonly ILogger DisconnectLogger = LogManager.GetLogger("Photon.Disconnect");
        public static readonly ILogger InvalidOpLogger = LogManager.GetLogger("Photon.InvalidOp");
    }
}
