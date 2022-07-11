using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Realtime;
using Photon.LoadBalancing.UnifiedClient;
using Photon.LoadBalancing.UnitTests.UnifiedServer.Policy;
using Photon.SocketServer.Security;
using Photon.UnitTest.Utils.Basic;

namespace Photon.LoadBalancing.UnitTests.UnifiedServer
{
    public enum AuthPolicy
    {
        AuthOnMaster,
        AuthOnNameServer,
        UseAuthOnce
    }

    public class LoadBalancingUnifiedTestsBase : UnifiedTestsBase
    {
        #region Fields and constants

        protected AuthPolicy authPolicy;

        protected string Player1 = "Player1";

        protected string Player2 = "Player2";

        protected string Player3 = "Player3";

        private string masterHostName;
        private string gameServerHostName;

        #endregion

        #region .ctr

        protected LoadBalancingUnifiedTestsBase(ConnectPolicy policy, AuthPolicy authPolicy = AuthPolicy.AuthOnNameServer) : base(policy)
        {
            this.UsePlugins = true;
            this.authPolicy = authPolicy;
        }

        #endregion

        #region Properties

        protected string MasterAddress
        {
            set { this.masterHostName = value; }
            get
            {
                if (string.IsNullOrEmpty(this.masterHostName))
                {
                    return ((LBConnectPolicyBase)this.connectPolicy).MasterServerAddress;
                }

                return this.masterHostName;
            }
        }

        protected string GameServerAddress
        {
            set { this.gameServerHostName = value; }
            get
            {
                if (string.IsNullOrEmpty(this.gameServerHostName))
                {
                    return ((LBConnectPolicyBase)this.connectPolicy).GameServerAddress;
                }

                return this.gameServerHostName;
            }

        }

        protected string NameServerAddress
        {
            get
            {
                return ((LBConnectPolicyBase)this.connectPolicy).NameServerAddress;
            }
        }

        protected bool UsePlugins { get; set; }

        protected AuthPolicy AuthPolicy { get { return this.authPolicy; } }
        #endregion

        #region Methods

        protected override void FixtureSetup()
        {
            base.FixtureSetup();
            UnifiedTestClient client = null;
            try
            {
                // this call will update Master server address
                client = (UnifiedTestClient)this.CreateTestClient();
                client.UserId = this.Player1;

                if (this.authPolicy != AuthPolicy.AuthOnMaster || this.connectPolicy.UseNSToGetMS)
                {
                    var authParams = new Dictionary<byte, object>
                    {
                        {ParameterCode.EncryptionMode, (byte) EncryptionModes.PayloadEncryption}
                    };
                    var response = this.ConnectAndAuthenticate(client, this.NameServerAddress, authParams, ErrorCode.Ok);
                    this.MasterAddress = (string)response[ParameterCode.Address];
                }

                this.ConnectAndAuthenticate(client, this.MasterAddress, client.UserId, null, reuseToken: true);

                var roomName = this.GenerateRandomString("room");
                var createGameResponse = client.CreateGame(roomName);
                this.GameServerAddress = createGameResponse.Address;
                this.ConnectAndAuthenticate(client, this.GameServerAddress);
                client.CreateGame(roomName);

                client.LeaveGame();
            }
            finally
            {
                DisposeClients(client);
            }
        }

        /// <summary>
        /// Creates a TestClientBase and connects to the master server.
        /// Sends an Authenticate request after connection is completed. The TestClientBase's IAuthenticationScheme determines which parameters are used for Authenticate. 
        /// </summary>
        protected virtual UnifiedTestClient CreateMasterClientAndAuthenticate(string userId = null, Dictionary<byte, object> authParameter = null)
        {
            var client = (UnifiedTestClient)this.CreateTestClient();
            client.UserId = userId;

            this.ConnectAndAuthenticateUsingAuthPolicy(client, authParameter);
            return client;
        }

        protected void ConnectAndAuthenticateUsingAuthPolicy(UnifiedTestClient client, Dictionary<byte, object> authParameter = null, short expectedErrorCode = 0)
        {
            OperationResponse response;
            switch (this.authPolicy)
            {
#pragma warning disable CS0618 // Type or member is obsolete
                case AuthPolicy.AuthOnMaster:
#pragma warning restore CS0618 // Type or member is obsolete
                    this.ConnectAndAuthenticate(client, this.MasterAddress, authParameter, expectedErrorCode);
                    break;
                case AuthPolicy.AuthOnNameServer:
                {
                    response = this.ConnectAndAuthenticate(client, this.NameServerAddress, authParameter);
                    this.MasterAddress = (string)response[ParameterCode.Address];
                    this.ConnectAndAuthenticate(client, this.MasterAddress, client.UserId, authParameter, reuseToken: true, expectedErrorCode);
                    break;
                }
                case AuthPolicy.UseAuthOnce:
                    response = this.ConnectAndAuthenticate(client, this.NameServerAddress, authParameter);
                    this.MasterAddress = (string)response[ParameterCode.Address];
                    this.ConnectAndAuthenticate(client, this.MasterAddress, client.UserId, authParameter, reuseToken:true, expectedErrorCode);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        protected virtual OperationResponse ConnectAndAuthenticate(UnifiedTestClient client, string address, Dictionary<byte, object> authParameter = null, short expectedErrorCode = 0)
        {
            return this.ConnectAndAuthenticate(client, address, client.UserId, authParameter, expectedErrorCode:expectedErrorCode);
        }

        protected virtual OperationResponse ConnectAndAuthenticate(UnifiedTestClient client, string address, 
            string userName, Dictionary<byte, object> authParameter = null, bool reuseToken  = false, short expectedErrorCode = 0)
        {
            if (client.Connected)
            {
                client.Disconnect();
            }

            if (!reuseToken
                && address == this.MasterAddress
#pragma warning disable CS0618 // Type or member is obsolete
                && this.authPolicy == AuthPolicy.AuthOnMaster)
#pragma warning restore CS0618 // Type or member is obsolete
            {
                client.Token = string.Empty;
            }

            client.OperationResponseQueueClear();
            client.EventQueueClear();

            if (authParameter == null)
            {
                authParameter = new Dictionary<byte, object>();
            }

            if (this.authPolicy != AuthPolicy.UseAuthOnce 
                || this.NameServerAddress == address)
            {
                ConnectToServer(client, address);
                if (this.authPolicy != AuthPolicy.UseAuthOnce)
                {
                    return client.Authenticate(userName, authParameter, expectedErrorCode);
                }

                return client.AuthOnce(userName, authParameter, expectedErrorCode);
            }

            client.ConnectWithAuthOnce(address, expectedErrorCode);
            return null;
        }

        protected string GenerateRandomString(string baseString)
        {
            return baseString + Guid.NewGuid().ToString().Substring(0, 6);
        }

        #endregion
    }
}
