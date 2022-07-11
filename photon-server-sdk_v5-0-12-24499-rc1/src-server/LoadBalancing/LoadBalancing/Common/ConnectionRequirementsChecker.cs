using ExitGames.Logging;

using Photon.Common;
using Photon.SocketServer;

namespace Photon.LoadBalancing.Common
{
    /// <summary>
    /// Checks that client used connection with required properties
    /// </summary>
    public class ConnectionRequirementsChecker
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        public static bool Check(PeerBase peer, string appId, bool authOnceUsed)
        {
            if (!CommonSettings.Default.RequireSecureConnection)
            {
                if (log.IsDebugEnabled)
                {
                    log.Debug($"Secure Connection Check: Account does not require connection to be secure. appId:{appId}");
                }
                return true;
            }

            return CheckSecureConnectionRequirement(peer, appId, authOnceUsed);
        }

        public static bool CheckSecureConnectionRequirement(PeerBase peer, string appId, bool authOnceUsed)
        {
            if (log.IsDebugEnabled)
            {
                log.Debug($"Secure Connection Check: Account requires connection to be secure. appId:{appId}");
            }
            if (peer.IsConnectionSecure)
            {
                if (log.IsDebugEnabled)
                {
                    log.Debug($"Secure Connection Check passed. Peer uses secure connect. appId:{appId}, p:{peer}");
                }
                return true;
            }

            if (log.IsDebugEnabled)
            {
                log.Debug($"Secure Connection Check failed. appId:{appId}, Connection Type:{peer.NetworkProtocol}, AuthOnceUsed:{authOnceUsed}");
            }

            peer.SendOperationResponseAndDisconnect(new OperationResponse((byte) (authOnceUsed ? Operations.OperationCode.AuthOnce : Operations.OperationCode.Authenticate))
            {
                ReturnCode = (int)ErrorCode.SecureConnectionRequired,
                DebugMessage = LBErrorMessages.SecureConnectionRequired,
            }, new SendParameters());

            return false;
        }
    }
}
