using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using ExitGames.Client.Photon;
using ExitGames.Logging;

namespace Photon.UnitTest.Utils.Basic
{
    class PhotonListener : IPhotonPeerListener
    {
        private readonly ManualResetEventSlim signalObject = new ManualResetEventSlim(false);

        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private readonly bool logConnectErrors;

        public PhotonListener(bool logConnectErrors)
        {
            this.logConnectErrors = logConnectErrors;
        }

        public void DebugReturn(DebugLevel level, string message)
        {
            if (!this.logConnectErrors && message.Contains("Connect() failed:"))
            {
                return;
            }
            switch (level)
            {
                case DebugLevel.ERROR:
                    log.Error(message);
                    break;
                case DebugLevel.WARNING:
                    log.Warn(message);
                    break;
                case DebugLevel.INFO:
                    log.Info(message);
                    break;
                default:
                    log.Debug(message);
                    break;
            }
        }

        public void OnOperationResponse(OperationResponse operationResponse)
        {
        }

        public void OnStatusChanged(StatusCode statusCode)
        {
            if (statusCode == StatusCode.Connect
                || statusCode == StatusCode.Disconnect)
            {
                this.signalObject.Set();
            }
        }

        public void OnEvent(EventData eventData)
        {
        }

        public void OnMessage(object messages)
        {
        }

        public bool WaitForConnection(int timeout)
        {
            return this.signalObject.Wait(timeout);
        }
    }

    public class PhotonStarter
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        public static Process Start(string serverAddress, string appId, ConnectionProtocol protocol, string photonCmdLine)
        {
            var ps = new PhotonStarter();
            const string defaultRelativeWorkingDirectory = "/../../../../../../dev_out/framework/config/";
            return ps.Start(serverAddress, appId, protocol, photonCmdLine, defaultRelativeWorkingDirectory);
        }

        protected Process Start(string serverAddress, string appId, ConnectionProtocol protocol, string photonCmdLine, string relativeWorkingDirectory)
        {
            if (CheckConnection(serverAddress, appId, protocol))
            {
                return null;
            }

            if (!IsLocalAddress(serverAddress))
            {
                throw new Exception(string.Format("Failed to establish connection to remote server:'{0}' using protocol {1}", serverAddress, protocol));
            }

            var path = FindPhoton();
            if (Environment.CurrentDirectory.Contains("\\bin\\"))
            {
                Environment.CurrentDirectory += relativeWorkingDirectory;
            }

            return StartPhoton(serverAddress, appId, protocol, path, photonCmdLine);
        }

        private static Process StartPhoton(string serverAddress, string appId, ConnectionProtocol protocol, string path, string photonCmdLine)
        {
            var process = Process.Start(path, photonCmdLine);
            if (process == null)
            {
                throw new Exception("PhotonSocketServer.exe can not be started");
            }

            if (process.WaitForExit(6500))
            {
                throw new Exception(string.Format("'{0}' finished unexpectedly.", path));
            }

            // we try to connect to new process
            var count = 15;
            while (count-- > 0)
            {
                if (CheckConnection(serverAddress, appId, protocol))
                {
                    return process;
                }

                if (process.WaitForExit(1000))
                {
                    process.Close();
                    throw new Exception(string.Format("'{0}' finished unexpectedly", path));
                }
            }

            process.CloseMainWindow();
            if (!process.WaitForExit(1000))
            {
                process.Kill();
            }
            process.Close();
            throw new Exception(string.Format("Unable to connect to address={1} and process {0}", path, serverAddress));
        }

        protected virtual string FindPhoton()
        {
            return FindClassicPhoton();
        }

        private static string FindClassicPhoton()
        {
            var dirs = Environment.CurrentDirectory.Split(Path.DirectorySeparatorChar);
            var index = dirs.TakeWhile(dir => dir != "src-server").Count();
            if (index == dirs.Length)
            {
                throw new Exception(string.Format("Can't find 'src-server' in folder '{0}'", Environment.CurrentDirectory));
            }

            var path = string.Empty;
            for (var i = 0; i < index; ++i)
            {
                path += dirs[i] + "\\";
            }
            path += "deploy\\";

            if (Environment.Is64BitOperatingSystem)
            {
                path += "bin_Win64\\";
            }
            else
            {
                path += "bin_Win32\\";
            }

            path += "PhotonSocketServer.exe";
            return path;
        }

        protected static string FindManagedPhoton()
        {
            var dirs = Environment.CurrentDirectory.Split(Path.DirectorySeparatorChar);
            var index = dirs.TakeWhile(dir => dir != "src-server").Count();
            if (index == dirs.Length)
            {
                throw new Exception(string.Format("Can't find 'src-server' in folder '{0}'", Environment.CurrentDirectory));
            }

            var path = string.Empty;
            for (var i = 0; i < index; ++i)
            {
                path += dirs[i] + "/";
            }
            path += "deploy_win/";

            path += "bin/";

            path += "PhotonServer.exe";
            return path;
        }

        private static bool ValidateServerCert(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslpolicyerrors)
        {
            return true;
        }
        private static bool CheckConnection(string serverAddress, string appId, ConnectionProtocol protocol)
        {
            ServicePointManager.ServerCertificateValidationCallback = ValidateServerCert;

            try
            {
                var listner = new PhotonListener(false);
                var peer = new PhotonPeer(listner, protocol);

                if (!peer.Connect(serverAddress, appId))
                {
                    return false;
                }

                var counter = 100;
                while (--counter > 0)
                {
                    peer.Service();
                    if (listner.WaitForConnection(0))
                    {
                        var res = peer.PeerState == PeerStateValue.Connected;
                        peer.Disconnect();
                        return res;
                    }

                    Thread.Sleep(50);
                }
            }
            finally
            {
                ServicePointManager.ServerCertificateValidationCallback = null;
            }

            return false;
        }

        private static bool IsLocalAddress(string serverAddress)
        {
            return (serverAddress.Contains("127.0.0.1")
                    || serverAddress.Contains("localhost"));
        }

    }
}