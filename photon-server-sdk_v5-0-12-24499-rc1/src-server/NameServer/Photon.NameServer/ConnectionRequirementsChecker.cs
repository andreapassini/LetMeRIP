// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ClientPeer.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using ExitGames.Logging;
using Photon.Common;
using Photon.NameServer.Diagnostic;
using Photon.SocketServer;

namespace Photon.NameServer
{
    public class ConnectionRequirementsChecker
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        public static bool Check(SocketServer.ClientPeer peer, bool requireSecureConnection, string appId, bool authOnceUsed)
        {
            if (!requireSecureConnection)
            {
                if (log.IsDebugEnabled)
                {
                    log.Debug($"Secure Connection Check: Account does not require connection to be secure. appId:{appId}");
                }
                return true;
            }

            if (log.IsDebugEnabled)
            {
                log.Debug($"Secure Connection Check: Account requires connection to be secure. appId:{appId}");
            }
            if (peer.IsConnectionSecure)
            {
                if (log.IsDebugEnabled)
                {
                    log.Debug($"Secure Connection Check passed. Peer uses SecureWebSocket. appId:{appId}");
                }
                return true;
            }

            if (log.IsDebugEnabled)
            {
                log.Debug($"Secure Connection Check failed. appId:{appId}, Connection Type:{peer.NetworkProtocol}, AuthOnceUsed:{authOnceUsed}");
            }

            peer.SendOperationResponseAndDisconnect(
                new OperationResponse((byte)(authOnceUsed
                    ? Operations.OperationCode.AuthOnce
                    : Operations.OperationCode.Authenticate))
                {
                    ReturnCode = (int)ErrorCode.SecureConnectionRequired,
                    DebugMessage = ErrorMessages.SecureConnectionRequired,
                }, new SendParameters());

            return false;
        }
    }
}