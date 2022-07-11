using System;
using System.Runtime.CompilerServices;
using ExitGames.Concurrency.Fibers;
using ExitGames.Logging;


namespace Photon.Common.Authentication
{
    public interface IAuthTimeoutCheckerClient
    {
        IDisposable AuthTimeoutTimer { get; set; }

        IFiber Fiber { get; }

        void OnAuthTimeout(byte authOpCode);
    }
    public static class  AuthTimeoutChecker
    {
        public static void StartWaitForAuthRequest(IAuthTimeoutCheckerClient client, ILogger log, byte authOpCode)
        {
            client.AuthTimeoutTimer = client.Fiber.Schedule(()=>OnAuthRequestWaitFailure(client, log, authOpCode), Settings.Default.AuthTimeout);
            if (log.IsDebugEnabled)
            {
                log.Debug($"Auth Timeout Checker scheduled. authTimeout:{Settings.Default.AuthTimeout} p:{client}");
            }
        }

       public static void StopWaitForAuthRequest(IAuthTimeoutCheckerClient client, ILogger log)
        {
            if (client.AuthTimeoutTimer != null)
            {
                client.AuthTimeoutTimer.Dispose();
                client.AuthTimeoutTimer = null;
            }
            if (log.IsDebugEnabled)
            {
                log.Debug($"Auth Timeout Checker stopped. authTimeout:{Settings.Default.AuthTimeout} p:{client}");
            }
        }

        #region .privates

        private static void OnAuthRequestWaitFailure(IAuthTimeoutCheckerClient client, ILogger log, byte authOpCode)
        {
            client.AuthTimeoutTimer = null;
            if (log.IsDebugEnabled)
            {
                log.Debug($"Disconnect peer by auth timeout. timeout:{Settings.Default.AuthTimeout}, p:{client}");
            }

            client.OnAuthTimeout(authOpCode);
        }

        #endregion

    }
}
