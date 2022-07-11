using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using ExitGames.Client.Photon;
using NUnit.Framework;
using Photon.Common.Authentication;
using Photon.Hive.Common.Lobby;
using Photon.Hive.Operations;
using Photon.Hive.Plugin;
using Photon.LoadBalancing.Common;
using Photon.LoadBalancing.GameServer;
using Photon.LoadBalancing.Operations;
using Photon.LoadBalancing.UnifiedClient;
using Photon.LoadBalancing.UnifiedClient.AuthenticationSchemes;
using Photon.LoadBalancing.UnitTests.UnifiedServer;
using Photon.LoadBalancing.UnitTests.UnifiedServer.Policy;
using Photon.NameServer.Operations;
using Photon.Realtime;
using Photon.UnitTest.Utils.Basic;
using ErrorCode = Photon.Realtime.ErrorCode;
using EventCode = Photon.Realtime.EventCode;
using OperationCode = Photon.Realtime.OperationCode;
using ParameterCode = Photon.Realtime.ParameterCode;
using Photon.SocketServer.Net;
using Hashtable = System.Collections.Hashtable;
using ParameterKey = Photon.Hive.Operations.ParameterKey;

namespace Photon.LoadBalancing.UnitTests.UnifiedTests
{
    public abstract partial class LBApiTestsImpl : LoadBalancingUnifiedTestsBase
    {
        protected string GameNamePrefix = "ForwardPlugin2"; //string.Empty;

        protected AuthTokenFactory tokenFactory;

        protected LBApiTestsImpl(ConnectPolicy policy, AuthPolicy authPolicy) : base(policy, authPolicy)
        {
        }

        protected override void FixtureSetup()
        {
            base.FixtureSetup();

            this.tokenFactory = this.CreateAuthTokenFactory();

            var hmacKey = Settings.Default.HMACTokenKey;

            this.tokenFactory.Initialize(Settings.Default.AuthTokenKey, hmacKey,
                TimeSpan.FromSeconds(Settings.Default.AuthTokenExpirationSeconds), Environment.MachineName);

        }


        protected static IAuthenticationScheme GetAuthScheme(string name)
        {
            if ("TokenLessAuthForOldClients" == name)
            {
                return new TokenLessAuthenticationScheme();
            }
            return new TokenAuthenticationScheme();
        }

        private int ranTestsCount;
        [SetUp]
        public void TestSetup()
        {
            this.WaitTimeout = 10000;
            ++this.ranTestsCount;
            this.WaitUntilEmptyGameList();
        }

        [TearDown]
        public void TestTearDown()
        {
            if (this.connectPolicy.IsOnline)
            {
                return;
            }

            var policy = (OfflineConnectPolicy) this.connectPolicy;

            Assert.That(policy.MSApplication.PeerCount, Is.GreaterThanOrEqualTo(0));
            Assert.That(policy.GSApplication.PeerCount, Is.GreaterThanOrEqualTo(0));
            Assert.That(policy.GS2Application.PeerCount, Is.GreaterThanOrEqualTo(0));
        }

        #region Auth Tests

        protected virtual AuthTokenFactory CreateAuthTokenFactory()
        {
            return new AuthTokenFactory();
        }

        protected virtual void UpdateTokensGSAndGame(UnifiedTestClient client, string gs, string game)
        {
            Assert.That(this.tokenFactory.DecryptAuthenticationToken((string)client.Token, out var authToke, out var errorMsg), Is.True, errorMsg);

            authToke.ExpectedGS = gs;
            authToke.ExpectedGameId = game;

            client.Token = this.tokenFactory.EncryptAuthenticationToken(authToke, true);
        }

        [Test]
        public void Auth_AuthResponseTest()
        {
            // master: 
            var client = (UnifiedTestClient) this.CreateTestClient();
            try
            {
                this.ConnectToServer(client, this.NameServerAddress);
                var response = client.Authenticate(this.Player1, new Dictionary<byte, object>());
                if (string.IsNullOrEmpty(this.Player1))
                {
                    Assert.IsNotNull(response.Parameters[ParameterCode.UserId]);
                }
                Assert.IsNotNull(response.Parameters[ParameterCode.Token]);
                client.Disconnect();

                this.ConnectToServer(client, this.MasterAddress);
                var parameters = new Dictionary<byte, object>()
                {
                    {ParameterCode.Token, response.Parameters[ParameterCode.Token]},
                };

                var response2 = client.Authenticate(this.Player1, parameters);

                Assert.LessOrEqual(2, response2.Parameters.Count);
                Assert.AreNotEqual(response.Parameters[ParameterCode.Token], response2.Parameters[ParameterCode.Token]);
                if (string.IsNullOrEmpty(this.Player1))
                {
                    Assert.That(response.Parameters.ContainsKey(ParameterCode.UserId), Is.True);
                }
                client.Disconnect();

            }
            finally
            {
                DisposeClients(client);
            }
        }

        [Test]
        public void Auth_AuthTokenRenewalTest()
        {
            if (this.connectPolicy.IsRemote)
            {
                Assert.Ignore("This test does not work with remote servers");
            }
            // master: 
            var client = (UnifiedTestClient)this.CreateTestClient();
            try
            {
                this.ConnectToServer(client, this.NameServerAddress);
                var response = client.Authenticate(this.Player1, new Dictionary<byte, object>());
                if (string.IsNullOrEmpty(this.Player1))
                {
                    Assert.IsNotNull(response.Parameters[ParameterCode.UserId]);
                }
//                Assert.IsNotNull(response.Parameters[ParameterCode.Token]);
                client.Disconnect();

                var encryptedToken1 = (string)response.Parameters[ParameterCode.Token];

                this.ConnectToServer(client, this.MasterAddress);
                var parameters = new Dictionary<byte, object>
                {
                    {ParameterCode.Token, encryptedToken1},
                };

                Thread.Sleep(1500);
                var response2 = client.Authenticate(this.Player1, parameters);

                Assert.LessOrEqual(2, response2.Parameters.Count);

                var encryptedToken2 = (string)response2.Parameters[ParameterCode.Token];

                Assert.AreNotEqual(encryptedToken1, encryptedToken2);
                client.Disconnect();

                var hmacKey = Settings.Default.HMACTokenKey;

                this.tokenFactory.Initialize(Settings.Default.AuthTokenKey, hmacKey,
                    TimeSpan.FromSeconds(Settings.Default.AuthTokenExpirationSeconds), Environment.MachineName);

                this.tokenFactory.DecryptAuthenticationToken(encryptedToken1, out AuthenticationToken decryptedToken1, out _);
                this.tokenFactory.DecryptAuthenticationToken(encryptedToken2, out AuthenticationToken decryptedToken2, out _);

                Assert.IsTrue(decryptedToken1.AreEqual(decryptedToken2));
                Assert.Less(decryptedToken1.ExpireAt, decryptedToken2.ExpireAt);
            }
            finally
            {
                DisposeClients(client);
            }
        }

        [Test]
        public void Auth_AuthOnceEncryptionModeTest()
        {
            // master: 
            var client = (UnifiedTestClient)this.CreateTestClient();
            try
            {
                this.ConnectToServer(client, this.NameServerAddress);
                client.AuthOnce(this.Player1, 
                    new Dictionary<byte, object>()
                    { {ParameterCode.EncryptionMode, (byte)255}}, ErrorCode.InvalidOperation);
                client.Disconnect();

                if (this.connectPolicy.IsOnline)
                {// no datagramm encryption for offline mode

                    this.ConnectToServer(client, this.NameServerAddress);
                    var response = client.AuthOnce(this.Player1,
                        new Dictionary<byte, object>()
                            {{ParameterCode.EncryptionMode, (byte) EncryptionMode.DatagramEncryption}});
                    if (string.IsNullOrEmpty(this.Player1))
                    {
                        Assert.IsNotNull(response.Parameters[ParameterCode.UserId]);
                    }

                    Assert.IsNotNull(response.Parameters[ParameterCode.Token]);
                    client.Disconnect();
                }

            }
            finally
            {
                DisposeClients(client);
            }
        }

        [TestCase(AuthPolicy.AuthOnNameServer, EncryptionMode.PayloadEncryption)]
        [TestCase(AuthPolicy.UseAuthOnce, EncryptionMode.PayloadEncryption)]
        [TestCase(AuthPolicy.UseAuthOnce, EncryptionMode.DatagramEncryption)]
        public virtual void Auth_JoinGameTest(AuthPolicy policy, EncryptionMode encryptionMode)
        {
            if (this.IsOffline &&
                (encryptionMode == EncryptionMode.DatagramEncryption ||
                 encryptionMode == EncryptionMode.DatagramEncryptionGCM ||
                 encryptionMode == EncryptionMode.DatagramEncryptionRandomSequence)
            )
            {
                Assert.Ignore("Offline tests simulate Tcp and do not support Datagramm encryption");
            }

            var oldPolicy = this.authPolicy;
            try
            {
                this.authPolicy = policy;
                this.Auth_JoinGameTestBody(encryptionMode);
            }
            finally
            {
                this.authPolicy = oldPolicy;
            }
        }

        protected void Auth_JoinGameTestBody(EncryptionMode encryptionMode)
        {
            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;

            try
            {
                masterClient1 = (UnifiedTestClient)this.CreateTestClient();
                masterClient1.UserId = this.Player1;

                masterClient2 = (UnifiedTestClient)this.CreateTestClient();
                masterClient2.UserId = this.Player2;

                var ap = new Dictionary<byte, object> {{ParameterCode.EncryptionMode, (byte) encryptionMode}};
                this.ConnectAndAuthenticateUsingAuthPolicy(masterClient1, ap);
                this.ConnectAndAuthenticateUsingAuthPolicy(masterClient2, ap);

                // create game
                string roomName = this.GenerateRandomString(MethodBase.GetCurrentMethod().Name);
                var createResponse = masterClient1.CreateGame(roomName);


                var player1Properties = new Hashtable { { "Name", this.Player1 } };
                var createRequest = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>()
                };
                createRequest.Parameters[ParameterCode.RoomName] = roomName;
                createRequest.Parameters[ParameterCode.Broadcast] = true;
                createRequest.Parameters[ParameterCode.PlayerProperties] = player1Properties;

                this.ConnectAndAuthenticate(masterClient1, createResponse.Address, masterClient1.UserId);
                masterClient1.SendRequestAndWaitForResponse(createRequest);

                // get own join event: 
                var ev = masterClient1.WaitForEvent();
                Assert.AreEqual(EventCode.Join, ev.Code);
                Assert.AreEqual(1, ev.Parameters[ParameterCode.ActorNr]);
                var ActorProperties = ((Hashtable)ev.Parameters[ParameterCode.PlayerProperties]);
                Assert.AreEqual(this.Player1, ActorProperties["Name"]);

                // in order to send game state from gs to ms
                Thread.Sleep(100);

                // join 2nd client on master - ok: 
                var joinResponse = masterClient2.JoinGame(roomName);

                // disconnect and move 2nd client to GS: 
                masterClient2.Disconnect();

                var player2Properties = new Hashtable { { "Name", this.Player2 } };
                var joinRequest = new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>()
                };
                joinRequest.Parameters[ParameterCode.RoomName] = roomName;
                joinRequest.Parameters[ParameterCode.Broadcast] = true;
                joinRequest.Parameters[ParameterCode.PlayerProperties] = player2Properties;

                this.ConnectAndAuthenticate(masterClient2, joinResponse.Address, masterClient2.UserId);
                masterClient2.SendRequestAndWaitForResponse(joinRequest);

                ev = masterClient1.WaitForEvent();
                Assert.AreEqual(EventCode.Join, ev.Code);
                Assert.AreEqual(2, ev.Parameters[ParameterCode.ActorNr]);
                ActorProperties = ((Hashtable)ev.Parameters[ParameterCode.PlayerProperties]);
                Assert.AreEqual(this.Player2, ActorProperties["Name"]);

                ev = masterClient2.WaitForEvent();
                Assert.AreEqual(EventCode.Join, ev.Code);
                Assert.AreEqual(2, ev.Parameters[ParameterCode.ActorNr]);
                ActorProperties = ((Hashtable)ev.Parameters[ParameterCode.PlayerProperties]);
                Assert.AreEqual(this.Player2, ActorProperties["Name"]);
            }
            finally
            {
                DisposeClients(masterClient1, masterClient2);
            }
        }


        [Test]
        public void Auth_AuthTimeoutTest()
        {
            if (this.IsOnline)
            {
                Assert.Ignore("this test supports only offline mode. it takes 1 minute for online");
            }

            var client = (UnifiedTestClient)this.CreateTestClient();
            try
            {
                this.ConnectToServer(client, this.NameServerAddress);
                Thread.Sleep(NameServer.Settings.Default.AuthTimeout + Photon.SocketServer.PeerBase.DefaultDisconnectInterval + 200);

                Assert.That(client.Connected, Is.False);

                client.EventQueueClear();
                client.OperationResponseQueueClear();

                this.ConnectToServer(client, this.NameServerAddress);

                var response = client.AuthOnce(this.Player1,
                    new Dictionary<byte, object> {{ParameterCode.EncryptionMode, (byte) EncryptionMode.DatagramEncryption}});

                client.Token = response.Parameters[ParameterCode.Token];

                var masterAddress = (string)response.Parameters[ParameterCode.Address];

                this.ConnectToServer(client, masterAddress);
                Thread.Sleep(Settings.Default.AuthTimeout + Photon.SocketServer.PeerBase.DefaultDisconnectInterval + 200);

                Assert.That(client.Connected, Is.False);

                client.EventQueueClear();
                client.OperationResponseQueueClear();

                DisposeClients(client);

                client = this.CreateMasterClientAndAuthenticate(this.Player1);

                this.ConnectAndAuthenticate(client, masterAddress);

                var jgResponse = client.CreateGame(this.GenerateRandomizedRoomName("game"));

                this.ConnectToServer(client, jgResponse.Address);
                Thread.Sleep(Settings.Default.AuthTimeout + Photon.SocketServer.PeerBase.DefaultDisconnectInterval + 200);

                Assert.That(client.Connected, Is.False);
            }
            finally
            {
                DisposeClients(client);
            }
        }

        /// <summary>
        /// we check that if everything is right we do not disconnect
        /// </summary>
        [Test]
        public void Auth_AuthTimeoutTest2()
        {
            if (this.IsOnline)
            {
                Assert.Ignore("this test supports only offline mode. it takes 1 minute for online");
            }
            // master: 
            var client = (UnifiedTestClient)this.CreateTestClient();
            try
            {
                this.ConnectToServer(client, this.NameServerAddress);

                client.AuthOnce(this.Player1,
                    new Dictionary<byte, object> {{ParameterCode.EncryptionMode, (byte) EncryptionMode.DatagramEncryption}});

                Thread.Sleep(NameServer.Settings.Default.AuthTimeout + Photon.SocketServer.PeerBase.DefaultDisconnectInterval + 200);

                Assert.That(client.Connected, Is.True);

                DisposeClients(client);

                client = this.CreateMasterClientAndAuthenticate(this.Player1);

                Thread.Sleep(Settings.Default.AuthTimeout + Photon.SocketServer.PeerBase.DefaultDisconnectInterval + 200);

                Assert.That(client.Connected, Is.True);

                var jgResponse = client.CreateGame(this.GenerateRandomizedRoomName("game"));

                this.ConnectAndAuthenticate(client, jgResponse.Address);
                Thread.Sleep(Settings.Default.AuthTimeout + Photon.SocketServer.PeerBase.DefaultDisconnectInterval + 200);

                Assert.That(client.Connected, Is.True);
            }
            finally
            {
                DisposeClients(client);
            }
        }
        #endregion

        [Test]
        public void DiffConnectionsEncryptedCommunication()
        {
            if (this.IsOffline)
            {
                Assert.Ignore("Test supposed to be ran in online mode");
            }

            UnifiedTestClient clientTcp = null, clientUdp = null, clientWs = null, clientWss = null;
            var oldProtocol = this.connectPolicy.Protocol;
            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);

                this.connectPolicy.Protocol = ConnectionProtocol.Tcp;
                clientTcp = this.CreateGameOnGameServer(this.Player1, new CreateGameRequest { GameId = roomName });

                this.connectPolicy.Protocol = ConnectionProtocol.Udp;
                clientUdp = this.CreateMasterClientAndAuthenticate(this.Player2);

                this.connectPolicy.Protocol = ConnectionProtocol.WebSocket;
                clientWs = this.CreateMasterClientAndAuthenticate(this.Player3);

                this.connectPolicy.Protocol = ConnectionProtocol.WebSocketSecure;
                clientWss = this.CreateMasterClientAndAuthenticate("Player4");

                this.ConnectClientToGame(clientUdp, roomName);
                this.ConnectClientToGame(clientWs, roomName);
                this.ConnectClientToGame(clientWss, roomName);

                clientTcp.SendRequest(new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Code, (byte)1 },
                        {ParameterCode.Data, new Hashtable{ { "data", "data"} } }
                    },
                }, true);

                clientUdp.WaitEvent(1);
                clientWs.WaitEvent(1);
                clientWss.WaitEvent(1);
            }
            finally
            {
                this.connectPolicy.Protocol = oldProtocol;
                DisposeClients(clientTcp, clientUdp, clientWs, clientWss);
            }
        }

        [Test]
        public void CreateGameTwice()
        {
            UnifiedTestClient masterClient = null;

            try
            {
                masterClient = this.CreateMasterClientAndAuthenticate(this.Player1);
                string roomName = this.GenerateRandomizedRoomName("CreateGameTwice_");
                masterClient.CreateGame(roomName);
                masterClient.CreateGame(roomName, ErrorCode.GameIdAlreadyExists);
            }
            finally
            {
                DisposeClients(masterClient);
            }
        }

        [Test]
        public void CreateGameTwice2()
        {
            UnifiedTestClient masterClient = null;
            UnifiedTestClient masterClient2 = null;

            try
            {
                masterClient = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                var roomName = this.GenerateRandomizedRoomName("CreateGameTwice_");
                masterClient.CreateGame(roomName);
                masterClient2.CreateGame(new CreateGameRequest
                {
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    GameId = roomName,
                }, ErrorCode.GameIdAlreadyExists);
            }
            finally
            {
                DisposeClients(masterClient, masterClient2);
            }
        }

        void LobbyTestBody(byte? type, short responseCode)
        {
            UnifiedTestClient masterClient = null;

            try
            {
                masterClient = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient.JoinLobby("tests", type, 0, responseCode);
            }
            finally
            {
                DisposeClients(masterClient);
            }
        }

        [Test]
        public void LobbyTypeTest()
        {
            this.LobbyTestBody(null, 0);
            this.LobbyTestBody(1, 0);
            this.LobbyTestBody(2, 0);
            this.LobbyTestBody(3, 0);
            this.LobbyTestBody(4, ErrorCode.InvalidOperation);
        }

        [Test]
        public void InvisibleGame()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;

            try
            {
                string roomName = this.GenerateRandomizedRoomName("InvisibleGame_");

                // create room 
                client1 = this.CreateGameOnGameServer(this.Player1, roomName, null, 0, false, true, 0, null, null);

                // connect 2nd client to master
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                // try join random game should fail because the game is not visible
                var joinRandomGameRequest = new JoinRandomGameRequest { JoinRandomType = (byte)MatchmakingMode.FillRoom };
                client2.JoinRandomGame(joinRandomGameRequest, ErrorCode.NoRandomMatchFound);

                // join 2nd client on master - ok - and disconnect from master: 
                var joinGameRequest = new JoinGameRequest { GameId = roomName };
                var joinResponse = client2.JoinGame(joinGameRequest);
                client2.Disconnect();

                // join directly on GS - game full:
                this.ConnectAndAuthenticate(client2, joinResponse.Address, client2.UserId);
                client2.JoinGame(joinGameRequest);
            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [Test]
        public void ClosedGame()
        {
            if (this.IsOnline)
            {
                Assert.Ignore("this test works only in offline mode");
            }
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;

            try
            {
                // create the game
                string roomName = this.GenerateRandomizedRoomName("ClosedGame_");
                client1 = this.CreateGameOnGameServer(this.Player1, roomName, null, 0, true, false, 0, null, null);


                // join 2nd client on master - closed: 
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                var joinGameRequest = new JoinGameRequest { GameId = roomName };
                client2.JoinGame(joinGameRequest, ErrorCode.GameClosed);
                client2.Disconnect();

                // we update token to pass host and game check on GS
                this.UpdateTokensGSAndGame(client2, "localhost", roomName);

                // join directly on GS - game closed: 
                this.ConnectAndAuthenticate(client2, client1.RemoteEndPoint, client2.UserId);
                client2.JoinGame(roomName, ErrorCode.GameClosed);
            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [Test]
        public void UserIdIsNotNull()
        {
            // master: 
            var client = (UnifiedTestClient)this.CreateTestClient();
            try
            {
                this.ConnectToServer(client, this.NameServerAddress);
                var response = client.Authenticate(null, new Dictionary<byte, object>());
                Assert.IsNotNull(response.Parameters[ParameterCode.UserId]);
                client.Disconnect();
                client.Dispose();

                client = (UnifiedTestClient) this.CreateTestClient();
                this.ConnectToServer(client, this.NameServerAddress);

                response = client.Authenticate("", new Dictionary<byte, object>());
                Assert.IsNotNull(response.Parameters[ParameterCode.UserId]);
                client.Disconnect();

            }
            finally
            {
                DisposeClients(client);
            }
        }

        [Test]
        public void MaxPlayers()
        {
            UnifiedTestClient masterClient = null;
            UnifiedTestClient gameClient1 = null;

            try
            {
                string roomName = this.GenerateRandomizedRoomName("MaxPlayers_");
                gameClient1 = this.CreateGameOnGameServer("GameClient", roomName, null, 0, true, true, 1, null, null);

                // join 2nd client on master - full: 
                masterClient = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient.JoinGame(roomName, ErrorCode.GameFull);

                // join random 2nd client on master - full: 
                var joinRequest = new JoinRandomGameRequest();
                masterClient.JoinRandomGame(joinRequest, ErrorCode.NoRandomMatchFound);
                joinRequest.JoinRandomType = (byte)MatchmakingMode.SerialMatching;
                masterClient.JoinRandomGame(joinRequest, ErrorCode.NoRandomMatchFound);
                joinRequest.JoinRandomType = (byte)MatchmakingMode.RandomMatching;
                masterClient.JoinRandomGame(joinRequest, ErrorCode.NoRandomMatchFound);
//                masterClient.Disconnect();

                // we do this to get correct token with GS and Game
                gameClient1.OpSetPropertiesOfRoom(new Hashtable {{(byte) GameParameter.MaxPlayers, 2}});

                masterClient.JoinGame(roomName);

                gameClient1.OpSetPropertiesOfRoom(new Hashtable { { (byte)GameParameter.MaxPlayers, 1 } });
                // join directly on GS: 
                this.ConnectAndAuthenticate(masterClient, gameClient1.RemoteEndPoint, masterClient.UserId);
                masterClient.JoinGame(roomName, ErrorCode.GameFull);
            }
            finally
            {
                DisposeClients(masterClient, gameClient1);
            }
        }


        [Test]
        public void MaxPlayers_SetToValueLessThenCurrentAmount([Values(0, 5)]byte initialValue)
        {
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("this tests requires userId to be set");
            }

            var client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
            var client2 = this.CreateMasterClientAndAuthenticate(this.Player2);
            var client3 = this.CreateMasterClientAndAuthenticate(this.Player3);
            try
            {
                var createGameRequest = new CreateGameRequest
                {
                    GameProperties = new Hashtable { {(byte)GameParameter.MaxPlayers, initialValue}},
                    GameId = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name)
                };

                var createGameResp = client1.CreateGame(createGameRequest);

                this.ConnectAndAuthenticate(client1, createGameResp.Address);

                client1.CreateGame(createGameRequest);

                Thread.Sleep(300);

                this.ConnectClientToGame(client2, createGameRequest.GameId);
                this.ConnectClientToGame(client3, createGameRequest.GameId);

                client1.SendRequestAndWaitForResponse(new OperationRequest()
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        { ParameterCode.Properties, new Hashtable { { (byte)GameParameter.MaxPlayers, (byte)2 } } },
                        { ParameterCode.Broadcast, true }
                    }
                }, ErrorCode.InvalidOperation);

                client1.SendRequestAndWaitForResponse(new OperationRequest()
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        { ParameterCode.Properties, new Hashtable { { (byte)GameParameter.MaxPlayers, (byte)0 } } },
                        { ParameterCode.Broadcast, true }
                    }
                });

            }
            finally
            {
                DisposeClients(client1, client2, client3);
            }
        }
        [Test]
        public void LobbyGameListEvents()
        {
            // previous tests could just have leaved games on the game server
            // so there might be AppStats or GameListUpdate event in schedule.
            // Just wait a second so this events can be published before starting the test
            Thread.Sleep(1100);

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;

            try
            {
                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                if (string.IsNullOrEmpty(this.Player1) && masterClient1.Token == null)
                {
                    Assert.Ignore("This test does not work correctly for old clients without userId and token");
                }

                Assert.IsTrue(masterClient1.OpJoinLobby());
                var ev = masterClient1.WaitForEvent(EventCode.GameList, 1000 + ConnectPolicy.WaitTime);
                Assert.AreEqual(EventCode.GameList, ev.Code);
                var gameList = (Hashtable)ev.Parameters[ParameterCode.GameList];
                this.CheckGameListCount(0, gameList);

                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                Assert.IsTrue(masterClient2.OpJoinLobby());
                ev = masterClient2.WaitForEvent(EventCode.GameList);
                Assert.AreEqual(EventCode.GameList, ev.Code);
                gameList = (Hashtable)ev.Parameters[ParameterCode.GameList];
                this.CheckGameListCount(0, gameList);

                // join lobby again: 
                masterClient1.OperationResponseQueueClear();
                Assert.IsTrue(masterClient1.OpJoinLobby());

                // wait for old app stats event
                masterClient2.CheckThereIsEvent(EventCode.AppStats, 10000);

                masterClient1.EventQueueClear();
                masterClient2.EventQueueClear();

                // open game
                string roomName = "LobbyGamelistEvents_1_" + Guid.NewGuid().ToString().Substring(0, 6);
                this.CreateRoomOnGameServer(masterClient1, roomName);

                // in order to get updates from gs on master server
                Thread.Sleep(1000);

                var timeout = Environment.TickCount + 10000;

                bool gameListUpdateReceived = false;
                bool appStatsReceived = false;

                while (Environment.TickCount < timeout && (!gameListUpdateReceived || !appStatsReceived))
                {
                    try
                    {
                        ev = masterClient2.WaitForEvent(1000);

                        if (ev.Code == EventCode.AppStats)
                        {
                            appStatsReceived = true;
                            Assert.AreEqual(1, ev[ParameterCode.GameCount]);
                        }
                        else if (ev.Code == EventCode.GameListUpdate)
                        {
                            gameListUpdateReceived = true;
                            var roomList = (Hashtable)ev.Parameters[ParameterCode.GameList];
                            this.CheckGameListCount(1, roomList);

                            Assert.IsTrue(roomList.ContainsKey(roomName), "Room not found in game list");

                            var room = (Hashtable)roomList[roomName];
                            Assert.IsNotNull(room);
                            Assert.AreEqual(3, room.Count);

                            Assert.IsNotNull(room[GamePropertyKey.IsOpen], "IsOpen");
                            Assert.IsNotNull(room[GamePropertyKey.MaxPlayers], "MaxPlayers");
                            Assert.IsNotNull(room[GamePropertyKey.PlayerCount], "PlayerCount");

                            Assert.AreEqual(true, room[GamePropertyKey.IsOpen]);
                            Assert.AreEqual(0, room[GamePropertyKey.MaxPlayers]);
                            Assert.AreEqual(1, room[GamePropertyKey.PlayerCount]);
                        }
                    }
                    catch (TimeoutException)
                    {
                    }
                }

                Assert.IsTrue(gameListUpdateReceived, "GameListUpdate event received");
                Assert.IsTrue(appStatsReceived, "AppStats event received");


                masterClient1.SendRequestAndWaitForResponse(new OperationRequest { OperationCode = (byte)Hive.Operations.OperationCode.Leave });
                masterClient1.Disconnect();

                gameListUpdateReceived = false;
                appStatsReceived = false;

                timeout = Environment.TickCount + 10000;
                while (Environment.TickCount < timeout && (!gameListUpdateReceived || !appStatsReceived))
                {
                    try
                    {
                        ev = masterClient2.WaitForEvent(1000);

                        if (ev.Code == EventCode.AppStats)
                        {
                            appStatsReceived = true;
                            Assert.AreEqual(0, ev[ParameterCode.GameCount]);
                        }

                        if (ev.Code == EventCode.GameListUpdate)
                        {
                            gameListUpdateReceived = true;

                            var roomList = (Hashtable)ev.Parameters[ParameterCode.GameList];

                            // count may be greater than one because games from previous tests are 
                            // being removed
                            //Assert.AreEqual(1, roomList.Count); 
                            Assert.IsTrue(roomList.ContainsKey(roomName));
                            var room = (Hashtable)roomList[roomName];
                            Assert.IsNotNull(room);

                            Assert.AreEqual(1, room.Count);
                            Assert.IsNotNull(room[GamePropertyKey.Removed], "Removed");
                            Assert.AreEqual(true, room[GamePropertyKey.Removed]);
                        }
                    }
                    catch (TimeoutException)
                    {
                    }
                }

                Assert.IsTrue(gameListUpdateReceived, "GameListUpdate event received");
                Assert.IsTrue(appStatsReceived, "AppStats event received");

                // leave lobby
                masterClient2.OpLeaveLobby();

                gameListUpdateReceived = false;
                appStatsReceived = false;

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                roomName = this.GenerateRandomizedRoomName("LobbyGamelistEvents_2_");

                this.CreateRoomOnGameServer(masterClient1, roomName);

                timeout = Environment.TickCount + 10000;

                while (Environment.TickCount < timeout && (!gameListUpdateReceived || !appStatsReceived))
                {
                    try
                    {
                        ev = masterClient2.WaitForEvent(1200);

                        if (ev.Code == EventCode.AppStats)
                        {
                            appStatsReceived = true;
                            Assert.AreEqual(1, ev[ParameterCode.GameCount]);
                        }

                        if (ev.Code == EventCode.GameListUpdate)
                        {
                            gameListUpdateReceived = true;
                        }
                    }
                    catch (TimeoutException)
                    {
                    }

                }
                Assert.IsFalse(gameListUpdateReceived, "GameListUpdate event received");
                Assert.IsTrue(appStatsReceived, "AppStats event received");
            }
            finally
            {
                DisposeClients(masterClient1, masterClient2);
            }
        }

        #region Join Tests

        [Test]
        public void JoinNotExistingGame()
        {
            if (this.connectPolicy.IsOnline)
            {
                Assert.Ignore("This is an offline test");
            }
            
            UnifiedTestClient client = null;

            try
            {
                string roomName = this.GenerateRandomizedRoomName("JoinNoMatchFound_");

                // try join game on master
                client = this.CreateMasterClientAndAuthenticate(this.Player1);
                client.JoinGame(roomName, ErrorCode.GameDoesNotExist);
                client.Disconnect();

                this.UpdateTokensGSAndGame(client, "localhost", roomName);
                // try join game on gameServer
                this.ConnectAndAuthenticate(client, this.GameServerAddress, client.UserId);
                client.JoinGame(roomName, ErrorCode.GameDoesNotExist);
                client.Disconnect();
            }
            finally
            {
                DisposeClients(client);
            }
        }

        [Test]
        public void JoinWithEmptyPluginListTest()
        {
            UnifiedTestClient masterClient1 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    Plugins = new string[0],
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                // client 1: try to join a game on master which does not exist (create if not exists) 
                var joinResponse1 = masterClient1.JoinGame(joinRequest);
                masterClient1.Disconnect();

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address);
                masterClient1.JoinGame(joinRequest);

            }
            finally
            {
                DisposeClients(masterClient1);
            }
        }

        [Test]
        public void CreateWithEmptyPluginListTest()
        {
            UnifiedTestClient masterClient1 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);
                var joinRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    Plugins = new string[0],
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                // client 1: try to join a game on master which does not exist (create if not exists) 
                var joinResponse1 = masterClient1.CreateGame(joinRequest);
                masterClient1.Disconnect();

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address);
                masterClient1.CreateGame(joinRequest);

            }
            finally
            {
                DisposeClients(masterClient1);
            }
        }

        [Test]
        public void JoinCreateIfNotExists()
        {
            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;

            try
            {
                string roomName = this.GenerateRandomizedRoomName("JoinCreateIfNotExists_");
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);
//                masterClient2 = this.CreateMasterClientAndAuthenticate(Player3);

                // client 1: try to join a game on master which does not exist (create if not exists) 
                var joinResponse1 = masterClient1.JoinGame(joinRequest);
                masterClient1.Disconnect();

                // client 2: try to random join a game which exists but is not created on the game server
                masterClient2.JoinRandomGame(new JoinRandomGameRequest(), ErrorCode.NoRandomMatchFound);

                // client 2: try to join (name) a game which exists but is not created on the game server
                var joinResponse2 = masterClient2.JoinGame(joinRequest);
                Assert.AreEqual(joinResponse1.Address, joinResponse2.Address);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address);
                masterClient1.JoinGame(joinRequest);

                joinRequest.JoinMode = JoinModeConstants.RejoinOrJoin;
                // client 2: try to join a game which exists and is created on the game server
                joinResponse2 = masterClient2.JoinGame(joinRequest);
                Assert.AreEqual(joinResponse1.Address, joinResponse2.Address);
            }
            finally
            {
                DisposeClients(masterClient1, masterClient2);
            }
        }

        /// <summary>
        /// this test checks that if new properties were added by plugin during game creation - meta data search does not fail with exception
        /// </summary>
        [Test]
        public void JoinUsingNewPropertyPlugin()
        {
            UnifiedTestClient masterClient1 = null;

            try
            {
                string roomName = this.GenerateRandomizedRoomName("JoinUsingNewPropertyPlugin_");
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    GameProperties = new Hashtable{{"x", "y"}},
                    Plugins = new []{ "NewPropertyPlugin" }
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                // client 1: try to join a game on master which does not exist (create if not exists) 
                var joinResponse1 = masterClient1.JoinGame(joinRequest);
                masterClient1.Disconnect();


                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address);
                masterClient1.JoinGame(joinRequest);
            }
            finally
            {
                DisposeClients(masterClient1);
            }
        }

        [Test]
        public void JoinCreateIfNotExistsLobbyProps()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;

            try
            {
                // connect to master server
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client1.JoinLobby();

                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                client2.JoinLobby();

                string roomName = this.GenerateRandomizedRoomName("JoinCreateIfNotExistsLobby_");

                var gameProps1 = new Hashtable {{(byte) 250, new object[] {"A"}}};

                var gameProps2 = new Hashtable {{"A", 1}, {"B", 2}};


                // join game with CreateIfNotExists parameter set
                var joinRequest = new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>()
                };
                joinRequest.Parameters[ParameterCode.RoomName] = roomName;
                joinRequest.Parameters[ParameterCode.JoinMode] = JoinModeConstants.CreateIfNotExists;

                var response = client1.SendRequestAndWaitForResponse(joinRequest);

                client2.EventQueueClear();

                client1.Token = response.Parameters[ParameterCode.Token];
                // try to join not existing game on the game server
                this.ConnectAndAuthenticate(client1, (string) response.Parameters[ParameterCode.Address], client1.UserId);
                client1.SendRequestAndWaitForResponse(joinRequest);

                // wait for the game list update 
                var ev = client2.WaitForEvent();
                if (ev.Code == 226)
                {
                    // app stats received first
                    ev = client2.WaitForEvent();
                }

                Console.WriteLine("EventCode: {0}", ev.Code);

                // set properties for lobby and properties in two requests send in one package
                var op = new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>()
                };

                op.Parameters[ParameterCode.Properties] = gameProps1;
                client1.SendRequest(op);

                op.Parameters[ParameterCode.Properties] = gameProps2;
                client1.SendRequest(op);


                // wait for the game list update event
                ev = client2.WaitForEvent(EventCode.GameListUpdate);
                Assert.IsTrue(ev.Parameters.ContainsKey(ParameterCode.GameList));
                var gameList = ev.Parameters[ParameterCode.GameList] as Hashtable;
                Assert.IsNotNull(gameList);
                Assert.IsTrue(gameList.ContainsKey(roomName));
                var gameProperties = gameList[roomName] as Hashtable;
                Assert.IsNotNull(gameProperties);
                Assert.IsTrue(gameProperties.ContainsKey("A"));
                Assert.IsFalse(gameProperties.ContainsKey("B"));
            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [Test]
        public void JoinOnGameServer()
        {
            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;

            try
            {
                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                // create game
                string roomName = this.GenerateRandomizedRoomName("JoinOnGameServer_");
                var createResponse = masterClient1.CreateGame(roomName);

                // join on master while the first client is not yet on GS:
                //masterClient2.JoinGame(roomName, ErrorCode.GameDoesNotExist);

                // move 1st client to GS: 
                masterClient1.Disconnect();

                var player1Properties = new Hashtable {{"Name", this.Player1}};
                var createRequest = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>()
                };
                createRequest.Parameters[ParameterCode.RoomName] = roomName;
                createRequest.Parameters[ParameterCode.Broadcast] = true;
                createRequest.Parameters[ParameterCode.PlayerProperties] = player1Properties;

                this.ConnectAndAuthenticate(masterClient1, createResponse.Address, masterClient1.UserId);
                masterClient1.SendRequestAndWaitForResponse(createRequest);

                // get own join event: 
                var ev = masterClient1.WaitForEvent();
                Assert.AreEqual(EventCode.Join, ev.Code);
                Assert.AreEqual(1, ev.Parameters[ParameterCode.ActorNr]);
                var ActorProperties = ((Hashtable) ev.Parameters[ParameterCode.PlayerProperties]);
                Assert.AreEqual(this.Player1, ActorProperties["Name"]);

                // in order to send game state from gs to ms
                Thread.Sleep(100);

                // join 2nd client on master - ok: 
                var joinResponse = masterClient2.JoinGame(roomName);

                // disconnect and move 2nd client to GS: 
                masterClient2.Disconnect();

                var player2Properties = new Hashtable {{"Name", this.Player2}};
                var joinRequest = new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>()
                };
                joinRequest.Parameters[ParameterCode.RoomName] = roomName;
                joinRequest.Parameters[ParameterCode.Broadcast] = true;
                joinRequest.Parameters[ParameterCode.PlayerProperties] = player2Properties;

                this.ConnectAndAuthenticate(masterClient2, joinResponse.Address, masterClient2.UserId);
                masterClient2.SendRequestAndWaitForResponse(joinRequest);

                ev = masterClient1.WaitForEvent();
                Assert.AreEqual(EventCode.Join, ev.Code);
                Assert.AreEqual(2, ev.Parameters[ParameterCode.ActorNr]);
                ActorProperties = ((Hashtable) ev.Parameters[ParameterCode.PlayerProperties]);
                Assert.AreEqual(this.Player2, ActorProperties["Name"]);

                ev = masterClient2.WaitForEvent();
                Assert.AreEqual(EventCode.Join, ev.Code);
                Assert.AreEqual(2, ev.Parameters[ParameterCode.ActorNr]);
                ActorProperties = ((Hashtable) ev.Parameters[ParameterCode.PlayerProperties]);
                Assert.AreEqual(this.Player2, ActorProperties["Name"]);

                // TODO: continue implementation
                // raise event, leave etc.        
            }
            finally
            {
                DisposeClients(masterClient1, masterClient2);
            }
        }

        [Test]
        public void JoinOnGameServerWithoutAuth()
        {
            UnifiedTestClient masterClient1 = null;

            try
            {
                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                // create game
                var roomName = this.GenerateRandomizedRoomName("JoinOnGameServer_");
                var createResponse = masterClient1.CreateGame(roomName);

                // move 1st client to GS: 
                masterClient1.Disconnect();

                var player1Properties = new Hashtable {{"Name", this.Player1}};
                var createRequest = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>()
                };
                createRequest.Parameters[ParameterCode.RoomName] = roomName;
                createRequest.Parameters[ParameterCode.Broadcast] = true;
                createRequest.Parameters[ParameterCode.PlayerProperties] = player1Properties;


                this.ConnectToServer(masterClient1, createResponse.Address);

                masterClient1.SendRequestAndWaitForResponse(createRequest, ErrorCode.OperationNotAllowedInCurrentState);

            }
            finally
            {
                DisposeClients(masterClient1);
            }
        }

        [Test]
        public void JoinDisconnectRejoin()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;

            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                // create game
                string roomName = this.GenerateRandomizedRoomName("JoinTwice");
                var createResponse = client1.CreateGame(roomName);

                // join on master while the first client is not yet on GS:
                //client2.JoinGame(roomName, ErrorCode.GameDoesNotExist);

                var player1Properties = new Hashtable {{"Name", this.Player1}};
                var createRequest = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>()
                };
                createRequest.Parameters[ParameterCode.RoomName] = roomName;
                createRequest.Parameters[ParameterCode.Broadcast] = true;
                createRequest.Parameters[ParameterCode.PlayerProperties] = player1Properties;

                // move first client to GS: 
                this.ConnectAndAuthenticate(client1, createResponse.Address, this.Player1);
                client1.SendRequestAndWaitForResponse(createRequest);

                // get own join event: 
                var ev = client1.WaitForEvent();
                Assert.AreEqual(EventCode.Join, ev.Code);
                Assert.AreEqual(1, ev.Parameters[ParameterCode.ActorNr]);
                var ActorProperties = ((Hashtable) ev.Parameters[ParameterCode.PlayerProperties]);
                Assert.AreEqual(this.Player1, ActorProperties["Name"]);

                Thread.Sleep(300);
                // join 2nd client on master - ok: 
                var joinResponse = client2.JoinGame(roomName);

                var player2Properties = new Hashtable {{"Name", this.Player2}};

                var joinRequest = new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>()
                };
                joinRequest.Parameters[ParameterCode.RoomName] = roomName;
                joinRequest.Parameters[ParameterCode.Broadcast] = true;
                joinRequest.Parameters[ParameterCode.PlayerProperties] = player2Properties;


                // move second client to GS: 
                this.ConnectAndAuthenticate(client2, joinResponse.Address, this.Player2);
                client2.SendRequestAndWaitForResponse(joinRequest);

                ev = client1.WaitForEvent();
                Assert.AreEqual(EventCode.Join, ev.Code);
                Assert.AreEqual(2, ev.Parameters[ParameterCode.ActorNr]);
                ActorProperties = ((Hashtable) ev.Parameters[ParameterCode.PlayerProperties]);
                Assert.AreEqual(this.Player2, ActorProperties["Name"]);

                ev = client2.WaitForEvent();
                Assert.AreEqual(EventCode.Join, ev.Code);
                Assert.AreEqual(2, ev.Parameters[ParameterCode.ActorNr]);
                ActorProperties = ((Hashtable) ev.Parameters[ParameterCode.PlayerProperties]);
                Assert.AreEqual(this.Player2, ActorProperties["Name"]);

                client2.LeaveGame();

                // get leave event on client1: 
                ev = client1.WaitForEvent();
                Assert.AreEqual(EventCode.Leave, ev.Code);
                Assert.AreEqual(2, ev.Parameters[ParameterCode.ActorNr]);

                // join again on GS - ok - disconnect and move to GS: 
                this.ConnectAndAuthenticate(client2, this.MasterAddress, this.Player2);
                joinResponse = client2.JoinGame(roomName);

                this.ConnectAndAuthenticate(client2, joinResponse.Address, this.Player2);
                client2.SendRequestAndWaitForResponse(joinRequest);

                // get join event on client1: 
                ev = client2.WaitForEvent();
                Assert.AreEqual(EventCode.Join, ev.Code);
                Assert.AreEqual(3, ev.Parameters[ParameterCode.ActorNr]);
                ActorProperties = ((Hashtable) ev.Parameters[ParameterCode.PlayerProperties]);
                Assert.AreEqual(this.Player2, ActorProperties["Name"]);

                // TODO: continue implementation
                // raise event, leave etc.        
            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [Test]
        public void JoinRandomNoMatchFound()
        {
            UnifiedTestClient masterClient = null;

            try
            {
                masterClient = this.CreateMasterClientAndAuthenticate(this.Player1);

                masterClient.JoinRandomGame(new Hashtable(), 0, new Hashtable(),
                    MatchmakingMode.FillRoom, string.Empty, AppLobbyType.Default, null, ErrorCode.NoRandomMatchFound);
            }
            finally
            {
                DisposeClients(masterClient);
            }
        }

        [Test]
        public void JoinRandomOnGameServer()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;

            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                // create
                string roomName = "JoinRandomOnGameServer_" + Guid.NewGuid().ToString().Substring(0, 6);

                var operationResponse = client1.CreateGame(roomName, true, true, 0);

                var gameServerAddress1 = operationResponse.Address;
                Console.WriteLine("Match on GS: " + gameServerAddress1);

                // join on master while the first client is not yet on GS:
                client2.JoinRandomGame(new Hashtable(), 0, new Hashtable(), MatchmakingMode.FillRoom,
                    string.Empty, AppLobbyType.Default, null, ErrorCode.NoRandomMatchFound);

                // move 1st client to GS: 
                this.ConnectAndAuthenticate(client1, gameServerAddress1, client1.UserId);

                var player1Properties = new Hashtable {{"Name", this.Player1}};

                client1.CreateGame(roomName, true, true, 0, null, null, player1Properties);

                // get own join event: 
                var ev = client1.WaitForEvent();
                Assert.AreEqual(EventCode.Join, ev.Code);
                Assert.AreEqual(1, ev.Parameters[ParameterCode.ActorNr]);
                var ActorProperties = ((Hashtable) ev.Parameters[ParameterCode.PlayerProperties]);
                Assert.AreEqual(this.Player1, ActorProperties["Name"]);

                Thread.Sleep(300);
                // join 2nd client on master - ok: 
                var opResponse = client2.JoinRandomGame(new Hashtable(), 0, new Hashtable(), MatchmakingMode.FillRoom, string.Empty,
                    AppLobbyType.Default, null);

                var gameServerAddress2 = opResponse.Address;
                Assert.AreEqual(gameServerAddress1, gameServerAddress2);

                var roomName2 = operationResponse.GameId;
                Assert.AreEqual(roomName, roomName2);

                // disconnect and move 2nd client to GS: 
                this.ConnectAndAuthenticate(client2, gameServerAddress2, client2.UserId);

                // clean up - just in case: 
                client1.OperationResponseQueueClear();
                client2.OperationResponseQueueClear();

                client1.EventQueueClear();
                client2.EventQueueClear();

                // join 2nd client on GS: 
                var player2Properties = new Hashtable {{"Name", this.Player2}};

                var request = new JoinGameRequest
                {
                    ActorProperties = player2Properties,
                    GameId = roomName,
                    ActorNr = 0,
                    JoinMode = 0,
                };

                client2.JoinGame(request);

                ev = client1.WaitForEvent();
                Assert.AreEqual(EventCode.Join, ev.Code);
                Assert.AreEqual(2, ev.Parameters[ParameterCode.ActorNr]);
                ActorProperties = ((Hashtable) ev.Parameters[ParameterCode.PlayerProperties]);
                Assert.AreEqual(this.Player2, ActorProperties["Name"]);

                ev = client2.WaitForEvent();
                Assert.AreEqual(EventCode.Join, ev.Code);
                Assert.AreEqual(2, ev.Parameters[ParameterCode.ActorNr]);
                ActorProperties = ((Hashtable) ev.Parameters[ParameterCode.PlayerProperties]);
                Assert.AreEqual(this.Player2, ActorProperties["Name"]);

                // disconnect 2nd client
                client2.Disconnect();

                ev = client1.WaitForEvent();
                Assert.AreEqual(EventCode.Leave, ev.Code);
                Assert.AreEqual(2, ev.Parameters[ParameterCode.ActorNr]);

                // TODO: continue implementation
                // raise event, leave etc.        
            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [Test]
        public void JoinRandomEmptyProperties()
        {
            UnifiedTestClient masterClient = null;
            UnifiedTestClient masterClient2 = null;

            try
            {
                const string lobbyName = null;
                const byte lobbyType = 0;

                var gameProperties = new Hashtable
                {
                    {"EmptyGameProperty", null}
                };

                var roomName = MethodBase.GetCurrentMethod().Name;
                masterClient2 = this.CreateGameOnGameServer("GameClient", roomName, lobbyName, lobbyType, true, true, 0, gameProperties, null);

                Thread.Sleep(10);

                masterClient = this.CreateMasterClientAndAuthenticate("Tester");

                // specifying the lobby name and type should give some matches
                masterClient.JoinRandomGame(null, null, ErrorCode.Ok, lobbyName, lobbyType);
                masterClient.JoinRandomGame(gameProperties, null, ErrorCode.NoRandomMatchFound, lobbyName, lobbyType);
            }
            finally
            {
                DisposeClients(masterClient);
                DisposeClients(masterClient2);
            }
        }

        [Test]
        public void JoinJoinLeaveLeaveFastRejoinTest()
        {
            if (!this.UsePlugins)
            {
                Assert.Ignore("test needs plugin support");
            }

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;

            try
            {
                string roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    EmptyRoomLiveTime = 3000,
                    PlayerTTL = int.MaxValue,
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                var joinResponse1 = masterClient1.JoinGame(joinRequest);

                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address);
                masterClient1.JoinGame(joinRequest);

                Thread.Sleep(100);

                joinRequest.JoinMode = JoinModeConstants.JoinOnly;
                // client 2: try to join a game which exists and is created on the game server

                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                var joinResponse2 = masterClient2.JoinGame(joinRequest);
                Assert.AreEqual(joinResponse1.Address, joinResponse2.Address);

                this.ConnectAndAuthenticate(masterClient2, joinResponse2.Address);

                masterClient2.JoinGame(joinRequest);

                Thread.Sleep(100);

                masterClient2.Disconnect();

                Thread.Sleep(100);
                masterClient1.Disconnect();

                Thread.Sleep(500);

                this.ConnectAndAuthenticate(masterClient2, this.MasterAddress);
                var joinRequest2 = new JoinGameRequest
                {
                    GameId = roomName,
                    ActorNr = 2,
                    JoinMode = (byte) JoinMode.JoinOrRejoin,
                };
                var response = masterClient2.JoinGame(joinRequest2);

                this.ConnectAndAuthenticate(masterClient2, response.Address);
                masterClient2.JoinGame(joinRequest2);

                Thread.Sleep(500);

                masterClient2.Disconnect();

                Thread.Sleep(5000);
            }
            finally
            {
                DisposeClients(masterClient1, masterClient2);
            }
        }

        [Test]
        public void JoiningNonExistingRoomCounterBug()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;

            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                // create
                var roomName = MethodBase.GetCurrentMethod().Name + Guid.NewGuid().ToString().Substring(0, 6);

                var request = new CreateGameRequest
                {
                    CheckUserOnJoin = false,
                    GameId = roomName,
                };

                var response = client1.CreateGame(request);

                this.ConnectAndAuthenticate(client1, response.Address);

                client1.CreateGame(request);

                Thread.Sleep(300);
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = (byte) JoinMode.Default,
                };

                var joinResponse = client2.JoinGame(joinRequest);


                //client1 leaves room
                client1.LeaveGame();
                Thread.Sleep(10);
                client1.Disconnect();

                Thread.Sleep(5500);

                this.ConnectAndAuthenticate(client2, joinResponse.Address);

                client2.JoinGame(joinRequest, ErrorCode.GameDoesNotExist);
                Thread.Sleep(100);

                client2.Disconnect();
            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        // this should not find anything
        [Test]
        public void JoinRandomWhileInactiveInGame()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;

            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                var lobbyType = LobbyType.SqlLobby;
                // create
                var roomName = MethodBase.GetCurrentMethod().Name + Guid.NewGuid().ToString().Substring(0, 6);

                var request = new CreateGameRequest
                {
                    CheckUserOnJoin = true,
                    GameId = roomName,
                    PlayerTTL = 10000,
                    LobbyType = (byte)lobbyType,
                    LobbyName = "lobby",
                    GameProperties = new Hashtable
                    {
                        { "C0", "10" }
                    },
                };

                var response = client1.CreateGame(request);

                this.ConnectAndAuthenticate(client1, response.Address);

                client1.CreateGame(request);

                Thread.Sleep(300);

                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = (byte)JoinMode.Default,
                    LobbyType = (byte)lobbyType,
                    LobbyName = "lobby"
                };

                var joinResponse = client2.JoinGame(joinRequest);

                this.ConnectAndAuthenticate(client2, joinResponse.Address);

                client2.JoinGame(joinRequest);

                //client1 leaves room
                client2.LeaveGame(true);
                Thread.Sleep(50);

                this.ConnectAndAuthenticate(client2, this.MasterAddress, client2.UserId, null, string.IsNullOrEmpty(client2.UserId));

                client2.JoinRandomGame(null, "C0>0", ErrorCode.NoRandomMatchFound, "lobby", (byte)lobbyType);
            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        // this tests should find another game
        [Test]
        public void JoinRandomWhileInactiveInGame2()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;
            UnifiedTestClient client3 = null;

            var lobbyType = LobbyType.SqlLobby;
            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                client3 = this.CreateMasterClientAndAuthenticate(this.Player3);

                // create
                var roomName = MethodBase.GetCurrentMethod().Name + Guid.NewGuid().ToString().Substring(0, 6);
                var roomName2 = MethodBase.GetCurrentMethod().Name + Guid.NewGuid().ToString().Substring(0, 6);

                var request = new CreateGameRequest
                {
                    CheckUserOnJoin = true,
                    GameId = roomName,
                    PlayerTTL = 10000,
                    LobbyType = (byte)lobbyType,
                    LobbyName = "lobby",
                    GameProperties = new Hashtable
                    {
                        { "C0", "10" }
                    },
                };

                var response = client1.CreateGame(request);
                this.ConnectAndAuthenticate(client1, response.Address);
                client1.CreateGame(request);

                request.GameId = roomName2;

                response = client3.CreateGame(request);
                this.ConnectAndAuthenticate(client3, response.Address);
                client3.CreateGame(request);


                Thread.Sleep(100);

                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = (byte)JoinMode.Default,
                    LobbyType = (byte)lobbyType,
                    LobbyName = "lobby"
                };

                var joinResponse = client2.JoinGame(joinRequest);
                this.ConnectAndAuthenticate(client2, joinResponse.Address);
                client2.JoinGame(joinRequest);

                //client1 leaves room
                client2.LeaveGame(true);
                Thread.Sleep(50);

                this.ConnectAndAuthenticate(client2, this.MasterAddress, client2.UserId, null, string.IsNullOrEmpty(client2.UserId));

                var jrResponse = client2.JoinRandomGame(null, "C0>0", ErrorCode.Ok, "lobby", (byte)lobbyType);

                Assert.That(jrResponse.Parameters[(byte)ParameterKey.GameId], Is.EqualTo(roomName2));
            }
            finally
            {
                DisposeClients(client1, client2, client3);
            }
        }

        [Test]
        public void JoinWithSameNameCheckUserOnJoinFalse()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;
            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player1);

                // create
                var roomName = MethodBase.GetCurrentMethod().Name + Guid.NewGuid().ToString().Substring(0, 6);

                var request = new CreateGameRequest
                {
                    CheckUserOnJoin = false,
                    GameId = roomName,
                };

                var response = client1.CreateGame(request);
                this.ConnectAndAuthenticate(client1, response.Address);
                client1.CreateGame(request);

                Thread.Sleep(300);

                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = (byte)JoinMode.Default,
                };

                var joinResponse = client2.JoinGame(joinRequest);
                this.ConnectAndAuthenticate(client2, joinResponse.Address);
                client2.JoinGame(joinRequest);

                Thread.Sleep(50);
            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [Test]
        public void ReJoinWithSameNameCheckUserOnJoinFalse()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;
            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player1);

                // create
                var roomName = MethodBase.GetCurrentMethod().Name + Guid.NewGuid().ToString().Substring(0, 6);

                var request = new CreateGameRequest
                {
                    CheckUserOnJoin = false,
                    PlayerTTL = 10000,
                    GameId = roomName,
                };

                var response = client1.CreateGame(request);
                this.ConnectAndAuthenticate(client1, response.Address);
                client1.CreateGame(request);

                Thread.Sleep(300);

                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = (byte)JoinMode.Default,
                };

                var joinResponse = client2.JoinGame(joinRequest);
                this.ConnectAndAuthenticate(client2, joinResponse.Address);
                client2.JoinGame(joinRequest);

                var ev = client2.WaitForEvent(EventCode.Join);

                //client2 leaves room
                client2.LeaveGame(true);
                Thread.Sleep(50);
                client2.Disconnect();

                joinRequest.JoinMode = (byte) JoinMode.RejoinOnly;
                joinRequest.ActorNr = (int)ev[(byte) ParameterKey.ActorNr];

                this.ConnectAndAuthenticate(client2, joinResponse.Address);
                client2.JoinGame(joinRequest);
            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        // Joining game with deferred response tests
        [Test]
        public void JGDR_JoinNonCreatedOnGSGame()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;
            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                // create
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);

                var request = new CreateGameRequest
                {
                    CheckUserOnJoin = true,
                    GameId = roomName,
                };

                // create game on MS
                var response = client1.CreateGame(request);
                this.ConnectAndAuthenticate(client1, response.Address);

                // join game
                client2.SendRequest(new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        { ParameterCode.RoomName, roomName },
                        { ParameterCode.JoinMode, (byte)JoinMode.Default },
                    }
                });

                Assert.IsFalse(client2.TryWaitForOperationResponse(1000, out OperationResponse resp));

                // Create game on GS
                client1.CreateGame(request);

                Thread.Sleep(300);

                Assert.IsTrue(client2.TryWaitForOperationResponse(1000, out resp));

                Assert.That(0, Is.EqualTo(resp.ReturnCode));

                client2.Token = resp[ParameterCode.Token];

                this.ConnectAndAuthenticate(client2, response.Address);
                client2.JoinGame(new JoinGameRequest { GameId = roomName });
                Thread.Sleep(300);
            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [Test]
        public void JoinMultipleGamesWithSameUserIdAndDifferentSessionsId()
        {
            const int Count = 10;
            UnifiedTestClient[] clients = new UnifiedTestClient[Count];
            try
            {
                for (int i = 0; i < Count; ++i)
                {
                    var client = this.CreateMasterClientAndAuthenticate(this.Player1);

                    clients[i] = client;

                    var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);

                    var request = new CreateGameRequest
                    {
                        CheckUserOnJoin = true,
                        GameId = roomName,
                    };

                    // create game on MS
                    var response = client.CreateGame(request);
                    this.ConnectAndAuthenticate(client, response.Address);

                    // join game
                    client.CreateGame(request);
                }

                Thread.Sleep(80_000);
            }
            finally
            {
                DisposeClients(clients);
            }
        }

        [Test]
        public void JGDR_JoinNonCreatedOnGSGameFailedToCreate()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;
            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                // create
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);

                var request = new CreateGameRequest
                {
                    CheckUserOnJoin = true,
                    GameId = roomName,
                };

                // create game on MS
                var response = client1.CreateGame(request);
                this.ConnectAndAuthenticate(client1, response.Address);

                // join game
                client2.SendRequest(new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        { ParameterCode.RoomName, roomName },
                        { ParameterCode.JoinMode, (byte)JoinMode.Default },
                    }
                });

                // join timeout is 15 seconds + we check only every 7.5 seconds.
                Assert.IsTrue(client2.TryWaitForOperationResponse(25_000, out OperationResponse resp));

                Assert.That(ErrorCode.GameDoesNotExist, Is.EqualTo(resp.ReturnCode));
            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [Test]
        public void JGDR_JoinNonCreatedOnGSGameWithExpectedUsers()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;
            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                // create
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);

                // we create with expected users. The goal is that player who connects to non created on GS game should get GameIsFull error
                var request = new CreateGameRequest
                {
                    CheckUserOnJoin = true,
                    GameId = roomName,
                    GameProperties = new Hashtable
                    {
                        { (byte)GameParameter.MaxPlayers, 2}
                    },
                    AddUsers = new [] {"Player3"}
                };

                // create game on MS
                var response = client1.CreateGame(request);
                this.ConnectAndAuthenticate(client1, response.Address);

                // join game
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = (byte)JoinMode.Default,
                };

                client2.JoinGame(joinRequest, ErrorCode.GameFull);

                // Create game on GS
                client1.CreateGame(request);

                Thread.Sleep(300);

                // same error after actual game creation
                client2.JoinGame(joinRequest, ErrorCode.GameFull);
            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [Test]
        public void JGDR_JoinNonCreatedOnGSGameWithExpectedUsers2()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;
            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                // create
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);

                // we create with expected users. The goal is that player who connects to non created on GS game should get GameIsFull error
                var request = new CreateGameRequest
                {
                    CheckUserOnJoin = true,
                    GameId = roomName,
                    GameProperties = new Hashtable
                    {
                        { (byte)GameParameter.MaxPlayers, 2}
                    },
                    AddUsers = new [] {"Player2"}
                };

                // create game on MS
                var response = client1.CreateGame(request);
                this.ConnectAndAuthenticate(client1, response.Address);

                // join game

                client2.SendRequest(new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        { ParameterCode.RoomName, roomName },
                        { ParameterCode.JoinMode, (byte)JoinMode.Default },
                    }
                });

                Assert.IsFalse(client2.TryWaitForOperationResponse(1000, out _));

                // Create game on GS
                client1.CreateGame(request);

                Thread.Sleep(300);

                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = (byte)JoinMode.Default,
                    CheckUserOnJoin = true,
                };

                client2.JoinGame(joinRequest);
            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }


        [Test]
        public void JoinNeitherActorPropsNorBroadcastAreSent()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;

            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                // create
                var roomName = MethodBase.GetCurrentMethod().Name + Guid.NewGuid().ToString().Substring(0, 6);

                var request = new CreateGameRequest
                {
                    CheckUserOnJoin = true,
                    GameId = roomName,
                    PublishUserId = true,
                };

                var response = client1.CreateGame(request);

                this.ConnectAndAuthenticate(client1, response.Address);

                client1.CreateGame(request);

                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = (byte)JoinMode.Default,
                };

                var joinResponse = client2.JoinGame(joinRequest);

                this.ConnectAndAuthenticate(client2, joinResponse.Address);

                var joinGameResponse = client2.JoinGame(joinRequest);

                Assert.That(joinGameResponse.ActorsProperties, Is.Not.Null.Or.Empty);
                Assert.That(joinGameResponse.ActorsProperties[1], Is.Not.Null.Or.Empty);

                Assert.That(((Hashtable)joinGameResponse.ActorsProperties[1])[ParameterCode.TargetActorNr], Is.EqualTo(this.Player1));

                client2.Disconnect();
            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }
        #endregion

        #region Create Game with parameters from client or Plugin

        [Test]
        public void CG_CreateGameWithInternalProperties()
        {
            UnifiedTestClient client1 = null;
            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                // create
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);

                // we create with expected users. The goal is that player who connects to non created on GS game should get GameIsFull error
                var request = new CreateGameRequest
                {
                    CheckUserOnJoin = true,
                    GameId = roomName,
                    GameProperties = new Hashtable
                    {
                        { (byte)GameParameter.Removed, true}
                    },
                };

                // create game on MS
                client1.CreateGame(request, ErrorCode.InvalidOperation);

                request = new CreateGameRequest
                {
                    CheckUserOnJoin = true,
                    GameId = roomName,
                    GameProperties = new Hashtable
                    {
                        { (byte)GameParameter.PlayerCount, 1111}
                    },
                };

                client1.CreateGame(request, ErrorCode.InvalidOperation);
            }
            finally
            {
                DisposeClients(client1);
            }
        }

        [TestCase("UseInternalProperties")]
        [TestCase("WrongExpectedUsers")]
        [TestCase("WrongEmptyRoomTTL")]
        [TestCase("WrongMasterClientId")]
        //we decided on current stage to not throw an exception to plugin if it sets game properties before call of Continue method
//        [TestCase("SetPropertiesFromOnCreateGame", ErrorCode.PluginReportedError)]
        public void CG_CreateGameWithWrongParamsFromPlugin(string testCase, short expectedResult = ErrorCode.InvalidOperation)
        {
            UnifiedTestClient client1 = null;
            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                // create
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + testCase);

                // we create with expected users. The goal is that player who connects to non created on GS game should get GameIsFull error
                var request = new CreateGameRequest
                {
                    CheckUserOnJoin = true,
                    GameId = roomName,
                    GameProperties = new Hashtable(),
                    Plugins = new[] { "BrokenPropsDuringGameCreationPlugin" }
                };

                // create game on MS
                var response = client1.CreateGame(request);
                this.ConnectAndAuthenticate(client1, response.Address);

                client1.CreateGame(request, expectedResult);
            }
            finally
            {
                DisposeClients(client1);
            }
        }

        #endregion Create Game with wrong parameters from client or Plugin

        [Test]
        public void ApplicationStats()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;
            UnifiedTestClient client3 = null;

            const int repeatChecksCount = 5;
            try
            {
                string roomName = "ApplicationStats_" + Guid.NewGuid().ToString().Substring(0, 6);
                // in order to clean up all previous peers on server
                Thread.Sleep(3500);

                Console.WriteLine("-----------creating of client 1------------------------");
                // create clients
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                Console.WriteLine("-----------creating of client 2------------------------");
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                Console.WriteLine("-----------creating of client 3------------------------");
                client3 = this.CreateMasterClientAndAuthenticate(this.Player3);
                Console.WriteLine("-----------creation finished. getting stats------------------------");

                Thread.Sleep(1500);
                // app stats
                Func<bool, bool> cond = isFinalCheck => CheckAppStatEvent(client3, 3, 0, 0, isFinalCheck);
                Assert.IsTrue(RepetitiveCheck(cond, repeatChecksCount));

                Console.WriteLine("------------------client 1 creates game-----------------");

                // create a game on the game server
                this.CreateRoomOnGameServer(client1, true, true, 10, roomName);

                // app stats: 
                cond = isFinalCheck => CheckAppStatEvent(client3, 2, 1, 1, isFinalCheck);
                Assert.IsTrue(RepetitiveCheck(cond, repeatChecksCount));

                Console.WriteLine("-------------client 2 joins random game ----------------------");
                // join random game
                var joinRequest = new JoinRandomGameRequest { GameProperties = new Hashtable(), JoinRandomType = (byte)MatchmakingMode.FillRoom };
                var joinResponse = client2.JoinRandomGame(joinRequest, ErrorCode.Ok);

                //                Assert.AreEqual(client1.RemoteEndPoint, joinResponse.Address);
                Assert.AreEqual(joinResponse.GameId, roomName);

                Console.WriteLine("-------------client 2 connects to game server ----------------------");
                this.ConnectAndAuthenticate(client2, joinResponse.Address, client2.UserId);
                client2.JoinGame(roomName);

                // app stats: 
                cond = isFinalCheck => CheckAppStatEvent(client3, 1, 2, 1, isFinalCheck);
                Assert.IsTrue(RepetitiveCheck(cond, repeatChecksCount));


                Console.WriteLine("-------------client 1 and 2 leaving ----------------------");
                client2.LeaveGame();
                client1.LeaveGame();


                Console.WriteLine("-------------client 3 waits for updated stats ----------------------");

                // we check here that peerCount == 0, although we do not do explicit disconnect for client1 and client2
                // we ensure in such a way that peers are disconnected by GS some time after leaving a room
                // if this check fails with Timeout, then either auto disconnect removed or it is performed later 
                // current delay for auto disconnect is 10000 ms
                cond = isFinalCheck => CheckAppStatEvent(client3, masterPeerCount:1, 
                    peerCount:0, gameCount:0, finalCheck: isFinalCheck);
                Assert.IsTrue(RepetitiveCheck(cond, repeatChecksCount));

            }
            finally
            {
                DisposeClients(client1, client2, client3);
            }
        }

        [Test]
        public void NegativePeersCountBugTest() //https://app.asana.com/0/199189943394/36771544536765
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;

            var gameName = MethodBase.GetCurrentMethod().Name;
            var gameName2 = MethodBase.GetCurrentMethod().Name + "_second_game";
            var authParams = new Dictionary<byte, object>
            {
                {(byte)ParameterKey.LobbyStats, true}
            };
            
            try
            {
                //1.
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1, authParams);
                if (string.IsNullOrEmpty(this.Player1) || client1.Token == null)
                {
                    Assert.Ignore("This test does not work correctly for old clients without userId and token");
                }

                Thread.Sleep(500);
                //3.
                var lobbyStatsResponse = client1.GetLobbyStats(null, null);
                Assert_IsOneOf(new[] { 0 }, lobbyStatsResponse.PeerCount[0], "Wrong peers count.");
                Assert_IsOneOf(new[] { 0 }, lobbyStatsResponse.GameCount[0], "Wrong games count.");

                //4.
                var createGame = new CreateGameRequest
                {
                    GameId = gameName,
                    EmptyRoomLiveTime = 11000,
                    PlayerTTL = 10000000,
                };
                var response = client1.CreateGame(createGame);

                this.ConnectAndAuthenticate(client1, response.Address);

                client1.CreateGame(createGame);

                Thread.Sleep(1000);
                //5.
                client1.Disconnect();

                Thread.Sleep(1000);
                //6.
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2, authParams);

                Thread.Sleep(100);
                //7.
                lobbyStatsResponse = client2.GetLobbyStats(null, null);
                Assert_IsOneOf(new[] { 1 }, lobbyStatsResponse.PeerCount[0], "Wrong peers count.");
                Assert_IsOneOf(new[] { 1 }, lobbyStatsResponse.GameCount[0], "Wrong games count.");


                //8.
                createGame = new CreateGameRequest
                {
                    GameId = gameName2,
                    JoinMode = (byte)JoinMode.CreateIfNotExists,
                    PlayerTTL = 10000000,
                };
                var joinResponse = client2.CreateGame(createGame);

                this.ConnectAndAuthenticate(client2, joinResponse.Address);

                client2.CreateGame(createGame);

                Thread.Sleep(300);
                //9.
                client2.LeaveGame(true);

                this.ConnectAndAuthenticate(client2, this.MasterAddress, authParams);

                //10.
                lobbyStatsResponse = client2.GetLobbyStats(null, null);
                Assert_IsOneOf(new[] { 1 }, lobbyStatsResponse.PeerCount[0], "Wrong peers count.");
                Assert_IsOneOf(new[] { 1 }, lobbyStatsResponse.GameCount[0], "Wrong games count.");

                //11.
                client2.Disconnect();

                //12.
                Thread.Sleep(1500);

                //13.
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1, authParams);

                Thread.Sleep(7500);
                //14.
                lobbyStatsResponse = client1.GetLobbyStats(null, null);
                Assert_IsOneOf(new[] { 0 }, lobbyStatsResponse.PeerCount[0], "Wrong peers count.");
                Assert_IsOneOf(new[] { 0 }, lobbyStatsResponse.GameCount[0], "Wrong games count.");

            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [Test]
        public void MatchmakingTypes()
        {
            UnifiedTestClient masterClient = null;
            var gameClients = new UnifiedTestClient[3];
            var roomNames = new string[3];
            try
            {
                // create games on game server
                for (int i = 0; i < gameClients.Length; i++)
                {
                    roomNames[i] = this.GenerateRandomizedRoomName("MatchmakingTypes_" + i + "_");
                    var createGameRequest = new CreateGameRequest { GameId = roomNames[i] };
                    gameClients[i] = this.CreateGameOnGameServer("Player_" + i, createGameRequest);
                }

                // fill room - 3x: 
                masterClient = this.CreateMasterClientAndAuthenticate(this.Player1);
                var joinRandomRequest = new JoinRandomGameRequest { JoinRandomType = (byte)MatchmakingMode.FillRoom };

                masterClient.JoinRandomGame(joinRandomRequest, ErrorCode.Ok, roomNames[0]);
                masterClient.Disconnect();
                masterClient = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient.JoinRandomGame(joinRandomRequest, ErrorCode.Ok, roomNames[0]);
                masterClient.JoinRandomGame(joinRandomRequest, ErrorCode.Ok, roomNames[0]);

                masterClient.Disconnect();
                masterClient.Dispose();

                // we reconnect to not break current join operations limit
                masterClient = this.CreateMasterClientAndAuthenticate(this.Player1);

                // serial matching - 4x: 
                joinRandomRequest = new JoinRandomGameRequest { JoinRandomType = (byte)MatchmakingMode.SerialMatching };
                masterClient.JoinRandomGame(joinRandomRequest, ErrorCode.Ok, roomNames[1]);
                masterClient.JoinRandomGame(joinRandomRequest, ErrorCode.Ok, roomNames[2]);

                // we disconnect to reset counters for limits
                masterClient.Disconnect();
                masterClient = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient.JoinRandomGame(joinRandomRequest, ErrorCode.Ok, roomNames[0]);
                masterClient.JoinRandomGame(joinRandomRequest, ErrorCode.Ok, roomNames[1]);

                for (int i = 0; i < gameClients.Length; i++)
                {
                    gameClients[i].LeaveGame();
                }
            }
            finally
            {
                DisposeClients(masterClient);
                DisposeClients(gameClients);
            }
        }

        #region Find Friends Tests

        [Test]
        public void FindFriends()
        {
            var userIds = new[] { "User1", "User2", "User3" };

            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;
            UnifiedTestClient client3 = null;

            try
            {


                // connect first client 
                client1 = this.CreateMasterClientAndAuthenticate(userIds[0]);
                client1.FindFriends(userIds, out bool[] onlineStates, out string[] userStates);
                Assert.AreEqual(true, onlineStates[0]);
                Assert.AreEqual(false, onlineStates[1]);
                Assert.AreEqual(false, onlineStates[2]);
                Assert.AreEqual(string.Empty, userStates[0]);
                Assert.AreEqual(string.Empty, userStates[1]);
                Assert.AreEqual(string.Empty, userStates[2]);

                // connect second client 
                client2 = this.CreateMasterClientAndAuthenticate(userIds[1]);
                client1.FindFriends(userIds, out onlineStates, out userStates);
                Assert.AreEqual(true, onlineStates[0]);
                Assert.AreEqual(true, onlineStates[1]);
                Assert.AreEqual(false, onlineStates[2]);
                Assert.AreEqual(string.Empty, userStates[0]);
                Assert.AreEqual(string.Empty, userStates[1]);
                Assert.AreEqual(string.Empty, userStates[2]);

                // connect third client and create game on game server
                client3 = this.CreateMasterClientAndAuthenticate(userIds[2]);
                var response = client3.CreateGame("FiendFriendsGame1");

                this.ConnectAndAuthenticate(client3, response.Address, userIds[2]);
                client3.CreateGame("FiendFriendsGame1");

                client1.FindFriends(userIds, out onlineStates, out userStates);
                Assert.AreEqual(true, onlineStates[0]);
                Assert.AreEqual(true, onlineStates[1]);
                Assert.AreEqual(true, onlineStates[2]);
                Assert.AreEqual(string.Empty, userStates[0]);
                Assert.AreEqual(string.Empty, userStates[1]);
                Assert.AreEqual("FiendFriendsGame1", userStates[2]);
                client1.EventQueueClear();

                // disconnect client2 and client3
                client2.Disconnect();
                client3.Disconnect();

                // wait some time until disconnect of client 3 was reported to game server
                Thread.Sleep(1000);

                client1.FindFriends(userIds, out onlineStates, out userStates);
                Assert.AreEqual(true, onlineStates[0]);
                Assert.AreEqual(false, onlineStates[1], $"{userIds[1]} disconnected from Master, but not shown as offline");
                Assert.AreEqual(false, onlineStates[2], $"{userIds[2]} disconnected from GS, but was not published to master in time");
                Assert.AreEqual(string.Empty, userStates[0]);
                Assert.AreEqual(string.Empty, userStates[1]);
                Assert.AreEqual(string.Empty, userStates[2]);

            }
            finally
            {
                DisposeClients(client1, client2, client3);
            }
        }

        [Test]
        public void FindFriendsAfterFailureToJoin()
        {
            var userIds = new[] { "User1", "User2", "User3" };

            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;
            UnifiedTestClient client3 = null;

            try
            {
                // connect first client 
                client1 = this.CreateMasterClientAndAuthenticate(userIds[0]);
                client1.FindFriends(userIds, out var onlineStates, out var userStates);
                Assert.AreEqual(true, onlineStates[0]);
                Assert.AreEqual(false, onlineStates[1]);
                Assert.AreEqual(false, onlineStates[2]);
                Assert.AreEqual(string.Empty, userStates[0]);
                Assert.AreEqual(string.Empty, userStates[1]);
                Assert.AreEqual(string.Empty, userStates[2]);

                // connect second client 
                client2 = this.CreateMasterClientAndAuthenticate(userIds[1]);
                client1.FindFriends(userIds, out onlineStates, out userStates);
                Assert.AreEqual(true, onlineStates[0]);
                Assert.AreEqual(true, onlineStates[1]);
                Assert.AreEqual(false, onlineStates[2]);
                Assert.AreEqual(string.Empty, userStates[0]);
                Assert.AreEqual(string.Empty, userStates[1]);
                Assert.AreEqual(string.Empty, userStates[2]);

                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);
                // connect third client and create game on game server
                client3 = this.CreateMasterClientAndAuthenticate(userIds[2]);
                var request = new CreateGameRequest
                {
                    GameId = roomName,
                    GameProperties = new Hashtable { { GameParameters.MaxPlayers, 1 } },
                };
                var response = client3.CreateGame(request);

                this.ConnectAndAuthenticate(client3, response.Address, userIds[2]);
                client3.CreateGame(request);

                client1.FindFriends(userIds, out onlineStates, out userStates);
                Assert.AreEqual(true, onlineStates[0]);
                Assert.AreEqual(true, onlineStates[1]);
                Assert.AreEqual(true, onlineStates[2]);
                Assert.AreEqual(string.Empty, userStates[0]);
                Assert.AreEqual(string.Empty, userStates[1]);
                Assert.AreEqual(roomName, userStates[2]);
                client1.EventQueueClear();

                // disconnect client2 and client3
                client2.Disconnect();
                client3.Disconnect();

                // wait some time until disconnect of client 3 was reported to game server
                Thread.Sleep(1000);

                client1.FindFriends(userIds, out onlineStates, out userStates);
                Assert.AreEqual(true, onlineStates[0]);
                Assert.AreEqual(false, onlineStates[1], $"{userIds[1]} disconnected from Master, but not shown as offline");
                Assert.AreEqual(false, onlineStates[2], $"{userIds[2]} disconnected from GS, but was not published to master in time");
                Assert.AreEqual(string.Empty, userStates[0]);
                Assert.AreEqual(string.Empty, userStates[1]);
                Assert.AreEqual(string.Empty, userStates[2]);

            }
            finally
            {
                DisposeClients(client1, client2, client3);
            }
        }

        [Test]
        public void FindFriendsWithOptions()
        {
            var userIds = new[] { "User1", "User2", "User3" };

            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;
            UnifiedTestClient client3 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);


                // connect first client 
                client1 = this.CreateMasterClientAndAuthenticate(userIds[0]);

                // connect second client 
                client2 = this.CreateMasterClientAndAuthenticate(userIds[1]);
                client1.FindFriends(userIds, out bool[] onlineStates, out string[] userStates);

                // connect third client and create game on game server
                client3 = this.CreateMasterClientAndAuthenticate(userIds[2]);
                var response = client3.CreateGame(roomName);

                // default: options 0
                client1.FindFriends(userIds, out onlineStates, out userStates);
                Assert.AreEqual(true, onlineStates[0]);
                Assert.AreEqual(true, onlineStates[1]);
                Assert.AreEqual(true, onlineStates[2]);
                Assert.AreEqual(string.Empty, userStates[0]);
                Assert.AreEqual(string.Empty, userStates[1]);
                Assert.AreEqual(roomName, userStates[2]);

                // Crated on GS: options 1
                client1.FindFriends(userIds, 0x01, out onlineStates, out userStates);
                Assert.AreEqual(true, onlineStates[2]);
                Assert.AreEqual(string.Empty, userStates[2]);

                // Visible: options 2
                client1.FindFriends(userIds, 0x02, out onlineStates, out userStates);
                Assert.AreEqual(true, onlineStates[2]);
                Assert.AreEqual(roomName, userStates[2]);

                // Open: options 2
                client1.FindFriends(userIds, 0x04, out onlineStates, out userStates);
                Assert.AreEqual(true, onlineStates[2]);
                Assert.AreEqual(roomName, userStates[2]);

                // Visible + Created: options 0x03
                client1.FindFriends(userIds, 0x03, out onlineStates, out userStates);
                Assert.AreEqual(true, onlineStates[2]);
                Assert.AreEqual(string.Empty, userStates[2]);

                // Open + Created: options 0x05
                client1.FindFriends(userIds, 0x05, out onlineStates, out userStates);
                Assert.AreEqual(true, onlineStates[2]);
                Assert.AreEqual(string.Empty, userStates[2]);

                this.ConnectAndAuthenticate(client3, response.Address, userIds[2]);
                client3.CreateGame(roomName);

                Thread.Sleep(300);

                client1.FindFriends(userIds, out onlineStates, out userStates);
                Assert.AreEqual(true, onlineStates[2]);
                Assert.AreEqual(roomName, userStates[2]);
                client1.EventQueueClear();

                // Crated on GS: options 1
                client1.FindFriends(userIds, 0x01, out onlineStates, out userStates);
                Assert.AreEqual(true, onlineStates[2]);
                Assert.AreEqual(roomName, userStates[2]);
                client1.EventQueueClear();

                // Visible: options 2
                client1.FindFriends(userIds, 0x02, out onlineStates, out userStates);
                Assert.AreEqual(true, onlineStates[2]);
                Assert.AreEqual(roomName, userStates[2]);
                client1.EventQueueClear();

                // Open: options 2
                client1.FindFriends(userIds, 0x04, out onlineStates, out userStates);
                Assert.AreEqual(true, onlineStates[2]);
                Assert.AreEqual(roomName, userStates[2]);
                client1.EventQueueClear();

                // game is invisible now
                client3.OpSetPropertiesOfRoom(new Hashtable { { (byte)GameParameter.IsVisible, false } });

                Thread.Sleep(300);

                client1.FindFriends(userIds, out onlineStates, out userStates);
                Assert.AreEqual(true, onlineStates[2]);
                Assert.AreEqual(roomName, userStates[2]);
                client1.EventQueueClear();

                // Crated on GS: options 1
                client1.FindFriends(userIds, 0x01, out onlineStates, out userStates);
                Assert.AreEqual(true, onlineStates[2]);
                Assert.AreEqual(roomName, userStates[2]);
                client1.EventQueueClear();

                // Visible: options 2
                client1.FindFriends(userIds, 0x02, out onlineStates, out userStates);
                Assert.AreEqual(true, onlineStates[2]);
                Assert.AreEqual(string.Empty, userStates[2]);
                client1.EventQueueClear();

                // Open: options 2
                client1.FindFriends(userIds, 0x04, out onlineStates, out userStates);
                Assert.AreEqual(true, onlineStates[2]);
                Assert.AreEqual(roomName, userStates[2]);
                client1.EventQueueClear();

                // once again to check options combining
                // Visible + Open: options 2
                client1.FindFriends(userIds, 0x06, out onlineStates, out userStates);
                Assert.AreEqual(true, onlineStates[2]);
                // room is open but invisible that is why we get empty string 
                Assert.AreEqual(string.Empty, userStates[2]);
                client1.EventQueueClear();

                // game closed now
                client3.OpSetPropertiesOfRoom(new Hashtable { { (byte)GameParameter.IsOpen, false } });

                Thread.Sleep(300);

                client1.FindFriends(userIds, out onlineStates, out userStates);
                Assert.AreEqual(true, onlineStates[2]);
                Assert.AreEqual(roomName, userStates[2]);
                client1.EventQueueClear();

                // Crated on GS: options 1
                client1.FindFriends(userIds, 0x01, out onlineStates, out userStates);
                Assert.AreEqual(true, onlineStates[2]);
                Assert.AreEqual(roomName, userStates[2]);
                client1.EventQueueClear();

                // Visible: options 2
                client1.FindFriends(userIds, 0x02, out onlineStates, out userStates);
                Assert.AreEqual(true, onlineStates[2]);
                Assert.AreEqual(string.Empty, userStates[2]);
                client1.EventQueueClear();

                // Open: options 2
                client1.FindFriends(userIds, 0x04, out onlineStates, out userStates);
                Assert.AreEqual(true, onlineStates[2]);
                Assert.AreEqual(string.Empty, userStates[2]);
                client1.EventQueueClear();

                // game is visible now
                client3.OpSetPropertiesOfRoom(new Hashtable { { (byte)GameParameter.IsVisible, true } });

                Thread.Sleep(300);

                client1.FindFriends(userIds, out onlineStates, out userStates);
                Assert.AreEqual(true, onlineStates[2]);
                Assert.AreEqual(roomName, userStates[2]);
                client1.EventQueueClear();

                // Crated on GS: options 1
                client1.FindFriends(userIds, 0x01, out onlineStates, out userStates);
                Assert.AreEqual(true, onlineStates[2]);
                Assert.AreEqual(roomName, userStates[2]);
                client1.EventQueueClear();

                // Visible: options 2
                client1.FindFriends(userIds, 0x02, out onlineStates, out userStates);
                Assert.AreEqual(true, onlineStates[2]);
                Assert.AreEqual(roomName, userStates[2]);
                client1.EventQueueClear();

                // Open: options 2
                client1.FindFriends(userIds, 0x04, out onlineStates, out userStates);
                Assert.AreEqual(true, onlineStates[2]);
                Assert.AreEqual(string.Empty, userStates[2]);
                client1.EventQueueClear();

                // once again to check options combining
                // Visible + Open: options 2
                client1.FindFriends(userIds, 0x06, out onlineStates, out userStates);
                Assert.AreEqual(true, onlineStates[2]);
                // room is visible but closed that is why we get empty string 
                Assert.AreEqual(string.Empty, userStates[2]);
                client1.EventQueueClear();

                // game is open now
                client3.OpSetPropertiesOfRoom(new Hashtable { { (byte)GameParameter.IsOpen, true } });

                Thread.Sleep(300);

                // Visible + Open: options 2
                client1.FindFriends(userIds, 0x06, out onlineStates, out userStates);
                Assert.AreEqual(true, onlineStates[2]);

                Assert.AreEqual(roomName, userStates[2]);
                client1.EventQueueClear();
            }
            finally
            {
                DisposeClients(client1, client2, client3);
            }
        }

        #endregion

        #region Raise Event Tests

        [Test]
        public void RaiseEventTargetActorsListTooooBig()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;
            try
            {
                var roomName = this.GenerateRandomizedRoomName("RaiseEventTargetActorsListTooooBig_");

                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                var createGameResponse = client1.CreateGame(roomName, true, true, 4);

                // switch client 1 to GS 
                this.ConnectAndAuthenticate(client1, createGameResponse.Address, client1.UserId);

                var createRequest = new OperationRequest { OperationCode = OperationCode.CreateGame, Parameters = new Dictionary<byte, object>() };
                createRequest.Parameters.Add(ParameterCode.RoomName, createGameResponse.GameId);
                createRequest.Parameters.Add((byte)Operations.ParameterCode.SuppressRoomEvents, true);
                client1.SendRequestAndWaitForResponse(createRequest);

                client2.JoinGame(roomName);
                this.ConnectAndAuthenticate(client2, createGameResponse.Address, client1.UserId);
                client2.JoinGame(roomName);

                // client 2 inserted twice, we expect only one event
                var raiseEventOp = new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.ActorList, new[]{3, 2, 2}},
                        {ParameterCode.Data, new Hashtable()}
                    }
                };

                client2.EventQueueClear();
                client2.SendRequest(raiseEventOp);

                Assert.IsTrue(client2.TryWaitForEvent(this.WaitTimeout, out _));
                Assert.IsFalse(client2.TryWaitForEvent(this.WaitTimeout, out _));

                raiseEventOp = new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Data, new Hashtable()}
                    }
                };

                client1.EventQueueClear();
                client2.EventQueueClear();
                client2.SendRequest(raiseEventOp);

                Assert.IsTrue(client1.TryWaitForEvent(this.WaitTimeout, out _));
                var result = client2.TryWaitForEvent(this.WaitTimeout, out var ev);
                Assert.That(!result || ev.Code != 0);

                raiseEventOp = new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.ActorList, new[]{1, 2, 3, 4, 5, 6}},
                        {ParameterCode.Data, new Hashtable()}
                    }
                };

                client2.SendRequestAndWaitForResponse(raiseEventOp, ErrorCode.InvalidOperation);

            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [Test]
        public void RaisePhotonEventTest()
        {
            UnifiedTestClient client1 = null;
            try
            {
                var roomName = this.GenerateRandomizedRoomName("RaisePhotonEventTest_");

                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                var createGameResponse = client1.CreateGame(roomName, true, true, 4);

                // switch client 1 to GS 
                this.ConnectAndAuthenticate(client1, createGameResponse.Address, client1.UserId);

                var createRequest = new OperationRequest { OperationCode = OperationCode.CreateGame, Parameters = new Dictionary<byte, object>() };
                createRequest.Parameters.Add(ParameterCode.RoomName, createGameResponse.GameId);
                createRequest.Parameters.Add((byte)Operations.ParameterCode.SuppressRoomEvents, true);
                client1.SendRequestAndWaitForResponse(createRequest);

                var raiseEventOp = new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Code, EventCode.Join },
                        {ParameterCode.Data, new Hashtable()}
                    }
                };

                client1.SendRequestAndWaitForResponse(raiseEventOp, ErrorCode.InvalidOperation);

            }
            finally
            {
                DisposeClients(client1);
            }
        }

        [Test]
        public void RaiseEventTargetPrioritiesTest()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;
            UnifiedTestClient client3 = null;
            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);

                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                client3 = this.CreateMasterClientAndAuthenticate(this.Player3);

                var createGameResponse = client1.CreateGame(roomName, true, true, 4);

                // switch client 1 to GS 
                this.ConnectAndAuthenticate(client1, createGameResponse.Address, client1.UserId);

                var createRequest = new OperationRequest { OperationCode = OperationCode.CreateGame, Parameters = new Dictionary<byte, object>() };
                createRequest.Parameters.Add(ParameterCode.RoomName, createGameResponse.GameId);
                createRequest.Parameters.Add((byte)Operations.ParameterCode.SuppressRoomEvents, true);
                client1.SendRequestAndWaitForResponse(createRequest);

                Thread.Sleep(300);

                var joinGameResponse = client2.JoinGame(roomName);
                this.ConnectAndAuthenticate(client2, joinGameResponse.Address);
                client2.JoinGame(roomName);

                joinGameResponse = client3.JoinGame(roomName);
                this.ConnectAndAuthenticate(client3, joinGameResponse.Address);
                client3.JoinGame(roomName);


                client2.SendRequest(new OperationRequest
                {
                    OperationCode = OperationCode.ChangeGroups,
                    Parameters = new Dictionary<byte, object>
                    {
                        { (byte)ParameterKey.GroupsForAdd, new byte[] {1, 1} }
                    }
                });

                Thread.Sleep(300);
                client1.SendRequest(new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {(byte)ParameterKey.Actors, new[] {2, 3} },
                        { (byte)ParameterKey.Group, (byte)1},
                        {(byte)ParameterKey.ReceiverGroup, (byte)Realtime.ReceiverGroup.MasterClient },
                        {(byte)ParameterKey.Code, (byte)2 },
                    }
                });

                Assert.That(client2.TryWaitForEvent(2, this.WaitTimeout, out _), Is.True);
                Assert.That(client3.TryWaitForEvent(2, this.WaitTimeout, out _), Is.True);
                Assert.That(client1.TryWaitForEvent(2, this.WaitTimeout, out _), Is.False);


                Thread.Sleep(300);
                client1.SendRequest(new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        { (byte)ParameterKey.Group, (byte)1},
                        {(byte)ParameterKey.ReceiverGroup, (byte)Realtime.ReceiverGroup.MasterClient },
                        {(byte)ParameterKey.Code, (byte)2 },
                    }
                });

                Assert.That(client2.TryWaitForEvent(2, this.WaitTimeout, out _), Is.True);
                Assert.That(client3.TryWaitForEvent(2, this.WaitTimeout, out _), Is.False);
                Assert.That(client1.TryWaitForEvent(2, this.WaitTimeout, out _), Is.False);

                Thread.Sleep(300);
                client1.SendRequest(new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {(byte)ParameterKey.ReceiverGroup, (byte)Realtime.ReceiverGroup.MasterClient },
                        {(byte)ParameterKey.Code, (byte)2 },
                    }
                });

                Assert.That(client1.TryWaitForEvent(2, this.WaitTimeout, out _), Is.True);
                Assert.That(client2.TryWaitForEvent(2, this.WaitTimeout, out _), Is.False);
                Assert.That(client3.TryWaitForEvent(2, this.WaitTimeout, out _), Is.False);
            }
            finally
            {
                DisposeClients(client1, client2, client3);
            }
        }

        [Test]
        public void RaiseEventCacheLimitExceeded()
        {
            if (this.IsOnline)
            {
                Assert.Ignore("Does not support online mode");
            }

            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;
            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);

                client1 = this.CreateMasterClientAndAuthenticate(Player1);

                var createGameResponse = client1.CreateGame(roomName, true, true, 4);

                // switch client 1 to GS 
                this.ConnectAndAuthenticate(client1, createGameResponse.Address, client1.UserId);

                var createRequest = new OperationRequest { OperationCode = OperationCode.CreateGame, Parameters = new Dictionary<byte, object>() };
                createRequest.Parameters.Add(ParameterCode.RoomName, createGameResponse.GameId);
                createRequest.Parameters.Add((byte)Operations.ParameterCode.SuppressRoomEvents, true);
                client1.SendRequestAndWaitForResponse(createRequest);

                var raiseEventOp = new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Data, new Hashtable()},
                        {ParameterCode.Cache, (byte)CacheOperation.AddToRoomCache }
                    }
                };

                for (int i = 0; i < 11; ++i)
                {
                    client1.SendRequest(raiseEventOp);
                }

                client1.WaitForEvent(EventCode.PropertiesChanged);
                client1.WaitForEvent(EventCode.ErrorInfo);

                client2 = this.CreateMasterClientAndAuthenticate(Player2);
                client2.JoinGame(roomName, ErrorCode.GameClosed);
                
                this.UpdateTokensGSAndGame(client2, "localhost", roomName);
                this.ConnectAndAuthenticate(client2, createGameResponse.Address);
                client2.JoinGame(roomName, ErrorCode.GameClosed);
            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [Test]
        public void RaiseEventCacheSliceLimitExceeded()
        {
            if (this.IsOnline)
            {
                Assert.Ignore("Does not support online mode");
            }

            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;
            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);

                client1 = this.CreateMasterClientAndAuthenticate(Player1);
                client2 = this.CreateMasterClientAndAuthenticate(Player2);

                var createGameResponse = client1.CreateGame(roomName, true, true, 4);

                // switch client 1 to GS 
                this.ConnectAndAuthenticate(client1, createGameResponse.Address, client1.UserId);

                var createRequest = new OperationRequest { OperationCode = OperationCode.CreateGame, Parameters = new Dictionary<byte, object>() };
                createRequest.Parameters.Add(ParameterCode.RoomName, createGameResponse.GameId);
                createRequest.Parameters.Add((byte)Operations.ParameterCode.SuppressRoomEvents, true);
                client1.SendRequestAndWaitForResponse(createRequest);

                var raiseEventOp = new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Data, new Hashtable()},
                        {ParameterCode.Cache, (byte)CacheOperation.SliceIncreaseIndex }
                    }
                };

                for (int i = 0; i < 10; ++i)
                {
                    client1.SendRequest(raiseEventOp);
                }

                client1.SendRequestAndWaitForResponse(raiseEventOp, ErrorCode.InvalidOperation);

                client2.JoinGame(roomName);
                this.ConnectAndAuthenticate(client2, createGameResponse.Address);
                client2.JoinGame(roomName);
            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [Test]
        public void RaiseEventActorCacheLimitExceeded()
        {
            if (this.IsOnline)
            {
                Assert.Ignore("Does not support online mode");
            }

            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;
            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);

                client1 = this.CreateMasterClientAndAuthenticate(Player1);

                var createGameResponse = client1.CreateGame(roomName, true, true, 4);

                // switch client 1 to GS 
                this.ConnectAndAuthenticate(client1, createGameResponse.Address, client1.UserId);

                var createRequest = new OperationRequest { OperationCode = OperationCode.CreateGame, Parameters = new Dictionary<byte, object>() };
                createRequest.Parameters.Add(ParameterCode.RoomName, createGameResponse.GameId);
                createRequest.Parameters.Add((byte)Operations.ParameterCode.SuppressRoomEvents, true);
                client1.SendRequestAndWaitForResponse(createRequest);

                client2 = this.CreateMasterClientAndAuthenticate(Player2);
                client2.JoinGame(roomName);
                var client2Token = client2.Token;


                for (byte i = 1; i <= 11; ++i)
                {
                    var raiseEventOp = new OperationRequest
                    {
                        OperationCode = OperationCode.RaiseEvent,
                        Parameters = new Dictionary<byte, object>
                        {
                            {ParameterCode.Data, new Hashtable()},
                            {ParameterCode.Cache, (byte)CacheOperation.MergeCache },
                            {ParameterCode.Code, i }
                        }
                    };
                    client1.SendRequest(raiseEventOp);
                }
                client1.WaitForEvent(EventCode.PropertiesChanged);
                client1.WaitForEvent(EventCode.ErrorInfo);

                client2.Dispose();
                client2 = this.CreateMasterClientAndAuthenticate(Player2);
                client2.JoinGame(roomName, ErrorCode.GameClosed);

                client2.Token = client2Token;
                this.ConnectAndAuthenticate(client2, createGameResponse.Address);
                client2.JoinGame(roomName, ErrorCode.GameClosed);
            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        #endregion

        [Test]
        public void EmptyRoomLiveTime()
        {
            UnifiedTestClient gameClient = null;

            try
            {
                string gameId = this.GenerateRandomizedRoomName("EmptyRoomLiveTime");

                var createGameRequest = new CreateGameRequest
                {
                    GameId = gameId,
                    EmptyRoomLiveTime = 2000
                };

                gameClient = this.CreateMasterClientAndAuthenticate(this.Player1);
                var response = gameClient.CreateGame(createGameRequest);

                this.ConnectAndAuthenticate(gameClient, response.Address);
                gameClient.CreateGame(createGameRequest);

                // in order to give server some time to update data about game on master server
                Thread.Sleep(100);
                gameClient.Disconnect();
                this.ConnectAndAuthenticate(gameClient, response.Address);

                // Rejoin the game. The game should be still in the room cache
                gameClient.JoinGame(gameId);
                gameClient.LeaveGame();

                gameClient.Disconnect();
                this.ConnectAndAuthenticate(gameClient, response.Address);

                // Rejoin the game again. Second clients leave should not have set the empty room live time to zero.
                gameClient.JoinGame(gameId);
                gameClient.LeaveGame();

                gameClient.Disconnect();
                this.ConnectAndAuthenticate(gameClient, response.Address);
                Thread.Sleep(2500);
                // Rejoin the game. The game should not be in the room cache anymore
                gameClient.JoinGame(gameId, ErrorCode.GameDoesNotExist);
            }
            finally
            {
                DisposeClients(gameClient);
            }
        }

        [Test]
        public void CheckPluginMismatch()
        {
            if (!this.UsePlugins)
            {
                Assert.Ignore("test needs plugin support");
            }
            UnifiedTestClient masterClient = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName("CheckPluginMismatch_");
                var createRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    Plugins = new[] { "WrongPlugin" },
                };
                masterClient = this.CreateMasterClientAndAuthenticate(this.Player1);
                var response = masterClient.CreateGame(createRequest);
                masterClient.Disconnect();

                this.ConnectAndAuthenticate(masterClient, response.Address, masterClient.UserId);
                masterClient.CreateGame(createRequest, ErrorCode.PluginMismatch);// TODO replace numbers with constant after client lib update

                masterClient.Disconnect();
            }
            finally
            {
                DisposeClients(masterClient);
            }
        }

        [Test]
        public void RandomGameMaxPlayerTest()
        {
            UnifiedTestClient masterClient = null;
            UnifiedTestClient masterClient2 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName("RandomGameMaxPlayerTest_");
                var createRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    GameProperties = new Hashtable
                    {
                        {GamePropertyKey.MaxPlayers, 6}
                    }
                };

                masterClient = this.CreateMasterClientAndAuthenticate(this.Player1);
                var response = masterClient.CreateGame(createRequest);
                this.ConnectAndAuthenticate(masterClient, response.Address, this.Player1);
                masterClient.CreateGame(createRequest);
                Thread.Sleep(300);// wait while game is created on game server

                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                masterClient2.JoinRandomGame(null, 3, null, MatchmakingMode.FillRoom, null, AppLobbyType.Default, "", ErrorCode.NoRandomMatchFound);
                masterClient2.JoinRandomGame(null, 3, null, MatchmakingMode.RandomMatching, null, AppLobbyType.Default, "", ErrorCode.NoRandomMatchFound);
                //to pass around limits
                masterClient2.Disconnect();
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                masterClient2.JoinRandomGame(null, 3, null, MatchmakingMode.SerialMatching, null, AppLobbyType.Default, "", ErrorCode.NoRandomMatchFound);
                masterClient2.JoinRandomGame(null, 6, null, MatchmakingMode.FillRoom, null, AppLobbyType.Default, "");
                //to pass around limits
                masterClient2.Disconnect();
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                masterClient2.JoinRandomGame(null, 6, null, MatchmakingMode.SerialMatching, null, AppLobbyType.Default, "");
                masterClient2.JoinRandomGame(null, 6, null, MatchmakingMode.RandomMatching, null, AppLobbyType.Default, "");
            }
            finally
            {
                DisposeClients(masterClient);
                DisposeClients(masterClient2);
            }
        }

        [Test]
        public void GameStateTest()
        {
            if (!this.UsePlugins)
            {
                Assert.Ignore("test needs plugin support");
            }
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("this tests requires userId to be set");
            }

            UnifiedTestClient masterClient = null;
            UnifiedTestClient masterClient2 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName("GameStateTest_");

                var expectedUsers = new[] {"P1", "P2", "P3"};
                var customProperties = new Hashtable { { "player_id", "12345" } };

                var createGameProperties = new Hashtable
                {
                    {(byte)GameParameter.MaxPlayers, 10 },
                    {(byte)GameParameter.IsOpen, true },
                    {(byte)GameParameter.IsVisible, true },
                    {(byte)GameParameter.ExpectedUsers,  expectedUsers},
                    {(byte)GameParameter.LobbyProperties, new[] { "lobby3Key", "lobby4Key" } },
                    {"prop1Key", "prop1Val"},
                    {"prop2Key", "prop2Val"},
                    {"lobby3Key", "lobby3Val"},
                    {"lobby4Key", "lobby4Val"},
                    {"map_name", "mymap"},
                };

                var createGameRequest = new CreateGameRequest
                {
                    PlayerTTL = int.MaxValue,
                    GameProperties = createGameProperties,
                    GameId = roomName,
                    Plugins = new [] { "SaveLoadStateTestPlugin" },
                    ActorProperties = customProperties,
                };

                masterClient = this.CreateMasterClientAndAuthenticate(this.Player1);
                var response = masterClient.CreateGame(roomName);
                this.ConnectAndAuthenticate(masterClient, response.Address, this.Player1);

                masterClient.CreateGame(createGameRequest);
                Thread.Sleep(300);// wait while game is created on game server

                // send messages to fill up cache
                FillEventsCache(masterClient);

                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                };
                var jgResponse = masterClient2.JoinGame(joinRequest);

                this.ConnectAndAuthenticate(masterClient2, jgResponse.Address, this.Player2);

                var customProperties2 = new Hashtable { { "player_id", "__12345" } };
                joinRequest.ActorProperties = customProperties2;
                var jgr = masterClient2.JoinGame(joinRequest);
                var gameProperties = jgr.GameProperties;

                Thread.Sleep(300);

                masterClient.Disconnect();
                masterClient2.Disconnect();

                Thread.Sleep(6000);

                this.ConnectAndAuthenticate(masterClient, this.MasterAddress);
                this.ConnectAndAuthenticate(masterClient2, this.MasterAddress);

                joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    GameProperties = new Hashtable
                    {
                        {GamePropertyKey.MaxPlayers, 6},
                    },
                    ActorNr = 2,
                    Plugins = new[] { "SaveLoadStateTestPlugin" },
                    JoinMode = (byte)JoinMode.RejoinOnly
                };

                // we join second player first in order to get MasterClientId == 2
                jgResponse = masterClient2.JoinGame(joinRequest);
                this.ConnectAndAuthenticate(masterClient2, jgResponse.Address, this.Player2);

                jgResponse = masterClient2.JoinGame(joinRequest);

                gameProperties[GamePropertyKey.MasterClientId] = 2;
                Assert.AreEqual(gameProperties, jgResponse.GameProperties);
                Assert.That(jgResponse.GameProperties[(byte)GameParameter.ExpectedUsers], Is.TypeOf(gameProperties[(byte)GameParameter.ExpectedUsers].GetType()));

                Thread.Sleep(100);
                joinRequest.ActorNr = 1;
                jgResponse = masterClient.JoinGame(joinRequest);
                this.ConnectAndAuthenticate(masterClient, jgResponse.Address, this.Player1);

                jgResponse = masterClient.JoinGame(joinRequest);
                Assert.AreEqual(gameProperties, jgResponse.GameProperties);

                Thread.Sleep(10);
                var gapResponse = masterClient.GetActorsProperties();
                Assert.IsNotNull(gapResponse.ActorProperties);
                Assert.IsNotNull(gapResponse.ActorProperties[1]);
                Assert.IsNotNull(gapResponse.ActorProperties[2]);

                Assert.AreEqual(customProperties, gapResponse.ActorProperties[1]);
                Assert.AreEqual(customProperties2, gapResponse.ActorProperties[2]);

                Assert.IsNotNull(masterClient2.WaitForEvent(3, 1000));
            }
            finally
            {
                DisposeClients(masterClient);
                DisposeClients(masterClient2);
            }
        }

        [Test]
        public void NickNameTest()
        {
            UnifiedTestClient client1 = null, client2 = null;
            var gameName = MethodBase.GetCurrentMethod().Name;

            const string User1Nick = "NickOfUser1";
            const string User2Nick = "NickOfUser2";

            try
            {
                var roomName = this.GenerateRandomizedRoomName(gameName);
                var createRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    GameProperties = new Hashtable
                    {
                        {GamePropertyKey.MaxPlayers, 6},
                    },
                    PlayerTTL = 5000,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    ActorProperties = new Hashtable
                    {
                        {ActorProperties.PlayerName, User1Nick}
                    },
                    BroadcastActorProperties = true,
                };

                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                var response = client1.CreateGame(createRequest);
                this.ConnectAndAuthenticate(client1, response.Address, this.Player1);
                client1.CreateGame(createRequest);
                Thread.Sleep(300); // wait while game is created on game server

                client1.EventQueueClear();

                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                var joinGameResponse = this.ConnectClientToGame(client2, roomName, nickName: User2Nick);
                Assert.AreEqual(1, (int)joinGameResponse.GameProperties[GamePropertyKey.MasterClientId]);

                Assert.IsNotNull(joinGameResponse.ActorsProperties);
                var actor1Properties = (Hashtable) joinGameResponse.ActorsProperties[1];
                Assert.AreEqual(User1Nick, actor1Properties[ActorProperties.PlayerName]);

                var ev = client1.WaitForEvent(EventCode.Join);
                Assert.IsNotNull(ev[ParameterCode.PlayerProperties]);
                var playerProperties = (Hashtable)ev[ParameterCode.PlayerProperties];

                Assert.AreEqual(User2Nick, playerProperties[ActorProperties.PlayerName]);

            }
            finally
            {
                DisposeClients(client1);
                DisposeClients(client2);
            }
        }

        #region http tests
        [Test]
        public void Http_HttpResponseHeaders()
        {
            if (!this.UsePlugins)
            {
                Assert.Ignore("This test needs plugins");
            }

            string pluginName = "HttpResponseHeadersPlugin";
            UnifiedTestClient client = null;

            try
            {
                var createGameRequest = new CreateGameRequest()
                {
                    GameId = this.GenerateRandomizedRoomName(pluginName),
                    Plugins = new[] { pluginName }
                };

                client = this.CreateMasterClientAndAuthenticate(this.Player1);
                var responseMS = client.CreateGame(createGameRequest);

                this.ConnectAndAuthenticate(client, responseMS.Address);
                client.CreateGame(createGameRequest);

            }
            finally
            {
                DisposeClients(client);
            }
        }

        [TestCase("OldHttp")]
        [TestCase("NewHttp")]
        public void Http_HttpMethodsTest(string mode)
        {
            if (!this.UsePlugins)
            {
                Assert.Ignore("This test needs plugins");
            }

            string pluginName = "HttpMethodTestPlugin";
            UnifiedTestClient client = null;

            try
            {
                var createGameRequest = new CreateGameRequest()
                {
                    GameId = this.GenerateRandomizedRoomName(pluginName),
                    Plugins = new[] { pluginName }
                };

                client = this.CreateMasterClientAndAuthenticate(this.Player1);
                var responseMS = client.CreateGame(createGameRequest);

                this.ConnectAndAuthenticate(client, responseMS.Address);
                client.CreateGame(createGameRequest);

                byte evCode = 0;
                switch(mode)
                {
                    case "NewHttp":
                        evCode = 1;
                        break;
                    case "OldHttp":
                        evCode = 2;
                        break;
                }
                var methods = new[] {
                    "POST",
                    "POST_NO_DATA",
                    "PUT",
                    "PUT_NO_DATA",
                    "GET",
                    "DELETE",
                    "DELETE_WITH_DATA",
                    "HEAD",
                    "OPTIONS",
                    "OPTIONS_WITH_DATA",
                    "TRACE"
                };

                for (int i = 0; i < methods.Length; ++i)
                {
                    var request = new OperationRequest
                    {
                        OperationCode = OperationCode.RaiseEvent,
                        Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Code, evCode},
                        {ParameterCode.Data, new Dictionary<string, string>{{"method", methods[i]}}}
                    }
                    };

                    client.EventQueueClear();
                    client.SendRequest(request);


                    Assert.That(client.TryWaitEvent(123, this.WaitTimeout, out EventData eventData),
                        "Failed to get response for method {0}", methods[i]);

                    var result = (Dictionary<byte, object>)eventData.Parameters;
                    Assert.That(result[0], Is.EqualTo("OK"), "Failure for method {0}", methods[i]);
                }
            }
            finally
            {
                DisposeClients(client);
            }
        }

        [Test]
        public void Http_HttpResponseMaxSizeTest()
        {
            if (!this.UsePlugins)
            {
                Assert.Ignore("This test needs plugins");
            }

            this.WaitTimeout = 3000000;

            string pluginName = "HttpLimitTestPlugin";
            UnifiedTestClient client = null;

            try
            {
                var createGameRequest = new CreateGameRequest()
                {
                    GameId = this.GenerateRandomizedRoomName(pluginName),
                    Plugins = new[] { pluginName }
                };

                client = this.CreateMasterClientAndAuthenticate(this.Player1);
                var responseMS = client.CreateGame(createGameRequest);

                this.ConnectAndAuthenticate(client, responseMS.Address);
                client.CreateGame(createGameRequest);

            }
            finally
            {
                DisposeClients(client);
            }
        }

        #endregion// http tests

        #region Leave Event Tests

        [Test]
        public void LeaveEventTest()
        {
            UnifiedTestClient client1 = null, client2 = null;
            var gameName = MethodBase.GetCurrentMethod().Name;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(gameName);
                var createRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    PlayerTTL = 1000,
                };

                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                var response = client1.CreateGame(createRequest);
                this.ConnectAndAuthenticate(client1, response.Address, this.Player1);
                client1.CreateGame(createRequest);

                Thread.Sleep(300); // wait while game is created on game server

                client1.EventQueueClear();

                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                this.ConnectClientToGame(client2, roomName);

                client1.WaitForEvent(EventCode.Join);

                client2.LeaveGame(true);

                var ev = client1.WaitForEvent(EventCode.Leave);
                var actorsList = (int[])ev[ParameterCode.ActorList];
                Assert.That(actorsList, Is.Not.Null);
                Assert.That(actorsList.Length, Is.EqualTo(1));
                Assert.That(actorsList[0], Is.EqualTo(1));
                Assert.That(ev[ParameterCode.IsInactive], Is.True);
                Assert.That(ev[ParameterCode.ActorNr], Is.EqualTo(2));

                Thread.Sleep(1000);

                ev = client1.WaitForEvent(EventCode.Leave);
                actorsList = (int[])ev[ParameterCode.ActorList];
                Assert.That(actorsList, Is.Not.Null);
                Assert.That(actorsList.Length, Is.EqualTo(1));
                Assert.That(actorsList[0], Is.EqualTo(1));
                Assert.That(ev[ParameterCode.ActorNr], Is.EqualTo(2));

            }
            finally
            {
                DisposeClients(client1);
                DisposeClients(client2);
            }
        }

        [TestCase("PlayerTTLNonZero")]
        [TestCase("PlayerTTLZero")]
        public void CorrectOnLeaveIfPlayerTTLIsZero(string param)
        {
            if (!this.UsePlugins)
            {
                Assert.Ignore("This test needs plugins");
            }

            UnifiedTestClient client1 = null, client2 = null;
            var gameName = MethodBase.GetCurrentMethod().Name + param;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(gameName);
                var createRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    PlayerTTL = param == "PlayerTTLZero" ? 0 : 1000,
                    Plugins = new[] {"CorrectOnLeaveTestPlugin"},
                };

                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                var response = client1.CreateGame(createRequest);
                this.ConnectAndAuthenticate(client1, response.Address, this.Player1);
                client1.CreateGame(createRequest);

                Thread.Sleep(300); // wait while game is created on master

                client1.EventQueueClear();

                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                this.ConnectClientToGame(client2, roomName);

                client1.WaitForEvent(EventCode.Join);

                client2.LeaveGame(true);

                var ev = client1.WaitForEvent((byte)1);
                Assert.That(ev.Parameters[0], Is.Null.Or.Empty);

            }
            finally
            {
                DisposeClients(client1);
                DisposeClients(client2);
            }
        }
        #endregion

        #region Op Settings tests

        [Test]
        public void ReceiveLobbyStatSettingTest()
        {
            UnifiedTestClient client1 = null;

            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                Assert.IsFalse(client1.TryWaitForEvent(EventCode.LobbyStats, 3000, out _));

                var request = new OperationRequest
                {
                    OperationCode = 218,// Op Settings
                    Parameters = new Dictionary<byte, object>
                    {
                        {0, true}
                    }
                };
                client1.SendRequest(request);

                Assert.IsTrue(client1.TryWaitForEvent(EventCode.LobbyStats, 3000, out _));
            }
            finally
            {
                DisposeClients(client1);
            }
        }

        #endregion

        #region MasterClientId tests

        [Test]
        public void MasterClientIdChangeJoinRejoinTest()
        {
            UnifiedTestClient client1 = null, client2 = null;
            var gameName = MethodBase.GetCurrentMethod().Name;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(gameName);
                var createRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    GameProperties = new Hashtable
                    {
                        {GamePropertyKey.MaxPlayers, 6},
                    },
                    PlayerTTL = 5000,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1)
                };

                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                var response = client1.CreateGame(createRequest);
                this.ConnectAndAuthenticate(client1, response.Address, this.Player1);
                client1.CreateGame(createRequest);
                Thread.Sleep(300); // wait while game is created on game server

                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                var joinGameResponse = this.ConnectClientToGame(client2, roomName);
                Assert.AreEqual(1, (int) joinGameResponse.GameProperties[GamePropertyKey.MasterClientId]);

                client1.LeaveGame(true);

                var ev = client2.WaitForEvent(EventCode.Leave);
                Assert.AreEqual(2, (int) ev[ParameterCode.MasterClientId]);

                client1.Disconnect();
                this.ConnectAndAuthenticate(client1, this.MasterAddress, client1.UserId, reuseToken: string.IsNullOrEmpty(this.Player1));
                joinGameResponse = this.ConnectClientToGame(client1, roomName, 1);
                Assert.AreEqual(2, (int) joinGameResponse.GameProperties[GamePropertyKey.MasterClientId]);

                client2.Disconnect();
                ev = client1.WaitForEvent(EventCode.Leave);
                Assert.AreEqual(1, (int) ev[ParameterCode.MasterClientId]);

                // rejoin
                this.ConnectAndAuthenticate(client2, this.MasterAddress, client2.UserId, reuseToken: string.IsNullOrEmpty(this.Player2));
                joinGameResponse = this.ConnectClientToGame(client2, roomName, 2);
                Assert.AreEqual(1, (int) joinGameResponse.GameProperties[GamePropertyKey.MasterClientId]);
            }
            finally
            {
                DisposeClients(client1);
                DisposeClients(client2);
            }
        }

        [Test]
        public void MasterClientIdCASUpdate()
        {
            UnifiedTestClient client1 = null, client2 = null, client3 = null;
            var gameName = MethodBase.GetCurrentMethod().Name;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(gameName);
                var createRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    GameProperties = new Hashtable
                    {
                        {GamePropertyKey.MaxPlayers, 6},
                    },
                    PlayerTTL = 5000,
                };

                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                var response = client1.CreateGame(createRequest);
                this.ConnectAndAuthenticate(client1, response.Address, this.Player1);
                client1.CreateGame(createRequest);
                Thread.Sleep(300); // wait while game is created on game server

                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                var joinGameResponse = this.ConnectClientToGame(client2, roomName);
                Assert.AreEqual(1, (int) joinGameResponse.GameProperties[GamePropertyKey.MasterClientId]);

                // we send wrong requests
                // we will try to set master client id for non existing client
                var request = new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {
                            ParameterCode.Properties, new Hashtable {{GameParameter.MasterClientId, 7}}
                        },
                        {ParameterCode.ExpectedValues, new Hashtable {{GameParameter.MasterClientId, 1}}}
                    }
                };

                // error expected
                client1.SendRequestAndWaitForResponse(request, ErrorCode.InvalidOperation);

                // we will try to set master client id with wrong current value
                request = new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Properties, new Hashtable {{GameParameter.MasterClientId, 2}}},
                        {ParameterCode.ExpectedValues, new Hashtable {{GameParameter.MasterClientId, 2}}}
                    }
                };

                client1.SendRequestAndWaitForResponse(request, ErrorCode.InvalidOperation);

                // now correct request
                request = new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {
                            ParameterCode.Properties, new Hashtable {{GameParameter.MasterClientId, 2}}
                        },
                        {ParameterCode.ExpectedValues, new Hashtable {{GameParameter.MasterClientId, 1}}},
                    }
                };

                client1.SendRequestAndWaitForResponse(request);

                var ev = client2.WaitForEvent(EventCode.PropertiesChanged);
                var properties = (Hashtable) ev[ParameterCode.Properties];
                Assert.IsNotNull(properties);
                Assert.AreEqual(2, (int) properties[GamePropertyKey.MasterClientId]);

                client3 = this.CreateMasterClientAndAuthenticate("Player3");
                joinGameResponse = this.ConnectClientToGame(client3, roomName);
                Assert.AreEqual(2, (int) joinGameResponse.GameProperties[GamePropertyKey.MasterClientId]);

            }
            finally
            {
                DisposeClients(client1);
                DisposeClients(client2);
                DisposeClients(client3);
            }
        }

        [Test]
        public void RaiseEventMasterClientReceiverNullRefTest()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;
            UnifiedTestClient client3 = null;
            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                client3 = this.CreateMasterClientAndAuthenticate(this.Player3);

                var roomName = MethodBase.GetCurrentMethod().Name;
                var createRoomRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    SuppressRoomEvents = true,
                };
                var createGameResponse = client1.CreateGame(createRoomRequest);

                this.ConnectAndAuthenticate(client1, createGameResponse.Address);

                client1.CreateGame(createRoomRequest);

                Thread.Sleep(300);

                var joinRoomResponse = client2.JoinGame(roomName);

                this.ConnectAndAuthenticate(client2, joinRoomResponse.Address);

                client2.JoinGame(roomName);

                client3.JoinGame(roomName);
                this.ConnectAndAuthenticate(client3, joinRoomResponse.Address);
                client3.JoinGame(roomName);

                client1.Disconnect();

                Thread.Sleep(100);

                var request = new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Code, (byte) 1},
                        {ParameterCode.ReceiverGroup, (byte)2},
                        {ParameterCode.Data, new Hashtable{{0, 1}}}
                    }
                };

                client2.EventQueueClear();
                client3.SendRequest(request);

                client2.WaitForEvent((byte)1);
            }
            finally
            {
                DisposeClients(client1, client2, client3);
            }
        }

        #endregion

        #region Join mode tests

        private const string FAST_REJOIN_CASE_REJOIN_MASTER = "RejoinMasterServer";
        private const string FAST_REJOIN_CASE_REJOIN_GS = "RejoinGameServer";
        [TestCase(FAST_REJOIN_CASE_REJOIN_MASTER)]
        [TestCase(FAST_REJOIN_CASE_REJOIN_GS)]
        public void FastReJoinTest(string useCase)
        {
            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;
            UnifiedTestClient masterClient12 = null;

            var useMasterServer = useCase == FAST_REJOIN_CASE_REJOIN_MASTER;
            try
            {
                var roomName = this.GenerateRandomizedRoomName("FastReJoinTest_");
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    PlayerTTL = 1000,
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                var joinResponse1 = masterClient1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address, masterClient1.UserId);
                masterClient1.JoinGame(joinRequest);

                // client 2: try to join a game which exists and is created on the game server
                var joinResponse2 = masterClient2.JoinGame(joinRequest);
                Assert.AreEqual(joinResponse1.Address, joinResponse2.Address);

                this.ConnectAndAuthenticate(masterClient2, joinResponse2.Address, masterClient2.UserId);
                masterClient2.JoinGame(joinRequest);

                masterClient12 = (UnifiedTestClient) this.CreateTestClient();
                masterClient12.UserId = this.Player1;
                masterClient12.Token = masterClient1.Token;
                joinRequest.ActorNr = 1;
                joinRequest.JoinMode = (byte)JoinMode.RejoinOnly;

                if (useMasterServer)
                {
                    this.ConnectAndAuthenticate(masterClient12, this.MasterAddress, masterClient12.UserId, reuseToken: true);
                    joinResponse1 = masterClient12.JoinGame(joinRequest);
                }
                this.ConnectAndAuthenticate(masterClient12, joinResponse1.Address, masterClient12.UserId);
                if (masterClient1.Token == null)// means that we use old token less auth 
                {
                    masterClient12.JoinGame(joinRequest, ErrorCode.JoinFailedFoundActiveJoiner);
                }
                else
                {
                    masterClient12.JoinGame(joinRequest);
                }

                Thread.Sleep(1200);
                if (masterClient1.Token != null) 
                {
                    Assert.IsFalse(masterClient1.Connected, "masterClient1 is expected to be disconnected by server.");
                }
            }
            finally
            {
                DisposeClients(masterClient1);
                DisposeClients(masterClient2);
                DisposeClients(masterClient12);
            }
        }

        [Test]
        public void FastJoinOnMasterWithSameNameTest()
        {
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires userId to be set");
            }

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;
            UnifiedTestClient masterClient12 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName("FastReJoinTest_");
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    PlayerTTL = 1000,
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                var joinResponse1 = masterClient1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address, masterClient1.UserId);
                masterClient1.JoinGame(joinRequest);

                // client 2: try to join a game which exists and is created on the game server
                var joinResponse2 = masterClient2.JoinGame(joinRequest);
                Assert.AreEqual(joinResponse1.Address, joinResponse2.Address);

                this.ConnectAndAuthenticate(masterClient2, joinResponse2.Address, masterClient2.UserId);
                masterClient2.JoinGame(joinRequest);

                masterClient12 = this.CreateMasterClientAndAuthenticate(this.Player1);

                var joinRandomGame = new JoinRandomGameRequest();
                masterClient12.JoinRandomGame(joinRandomGame, ErrorCode.NoRandomMatchFound);
            }
            finally
            {
                DisposeClients(masterClient1);
                DisposeClients(masterClient2);
                DisposeClients(masterClient12);
            }
        }

        [Test]
        public void ReJoinFullRoomTest()
        {
            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;
            UnifiedTestClient masterClient3 = null;
            UnifiedTestClient masterClient4 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName("ReJoinFullRoomTest_");
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    PlayerTTL = int.MaxValue,
                    GameProperties = new Hashtable
                    {
                        {GamePropertyKey.MaxPlayers, 3}
                    }
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                masterClient3 = this.CreateMasterClientAndAuthenticate(this.Player3);
                masterClient4 = this.CreateMasterClientAndAuthenticate(this.Player3 != null ? "Player4" : null);

                if (string.IsNullOrEmpty(this.Player1) && masterClient1.Token == null)
                {
                    Assert.Ignore("This test does not work correctly for old clients without userId and token");
                }

                var joinResponse1 = masterClient1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address, masterClient1.UserId);
                masterClient1.JoinGame(joinRequest);

                var joinEvent = masterClient1.WaitForEvent(EventCode.Join);

                // client 2: try to join a game which exists and is created on the game server
                var joinResponse2 = masterClient2.JoinGame(joinRequest);
                Assert.AreEqual(joinResponse1.Address, joinResponse2.Address);

                this.ConnectAndAuthenticate(masterClient2, joinResponse2.Address, masterClient2.UserId);
                masterClient2.JoinGame(joinRequest);

                masterClient1.LeaveGame(true);

                // client 3: try to join a game which exists and is created on the game server
                joinResponse2 = masterClient3.JoinGame(joinRequest);
                Assert.AreEqual(joinResponse1.Address, joinResponse2.Address);

                this.ConnectAndAuthenticate(masterClient3, joinResponse2.Address, masterClient3.UserId);
                masterClient3.JoinGame(joinRequest);

                // client 4: try to join a game which exists and is created on the game server
                masterClient4.JoinGame(joinRequest, ErrorCode.GameFull);

                joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.RejoinOnly,
                    ActorNr = this.Player1 == null ? (int) joinEvent[ParameterCode.ActorNr] : -1,
                };

                Thread.Sleep(200);

                this.ConnectAndAuthenticate(masterClient1, this.MasterAddress, masterClient1.UserId,
                    reuseToken: string.IsNullOrEmpty(this.Player1));
                joinResponse1 = masterClient1.JoinGame(joinRequest);

                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address, masterClient1.UserId);
                masterClient1.JoinGame(joinRequest);

                // leave and wait while client will be disconnected from game
                masterClient1.LeaveGame(true);

                Thread.Sleep(200);

                // change join mode and rejoin again. should succeed
                joinRequest.JoinMode = JoinModeConstants.RejoinOrJoin;

                this.ConnectAndAuthenticate(masterClient1, this.MasterAddress, masterClient1.UserId,
                    reuseToken: string.IsNullOrEmpty(this.Player1));
                joinResponse1 = masterClient1.JoinGame(joinRequest);

                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address, masterClient1.UserId);
                masterClient1.JoinGame(joinRequest);
            }
            finally
            {
                DisposeClients(masterClient1);
                DisposeClients(masterClient2);
                DisposeClients(masterClient3);
                DisposeClients(masterClient4);
            }
        }

        [Test]
        public void ReJoinFullRoomTestJapanVersion()
        {
            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;
            UnifiedTestClient masterClient3 = null;
            UnifiedTestClient masterClient4 = null;
            UnifiedTestClient masterClient5 = null;

            const int TestPlayerTtl = 5000;
            try
            {
                var roomName = this.GenerateRandomizedRoomName("ReJoinFullRoomTestJapan_");
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    PlayerTTL = TestPlayerTtl,
                    GameProperties = new Hashtable
                    {
                        {GamePropertyKey.MaxPlayers, 4}
                    }
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                masterClient3 = this.CreateMasterClientAndAuthenticate(this.Player3);
                masterClient4 = this.CreateMasterClientAndAuthenticate(this.Player3 != null ? "Player4" : null);
                masterClient5 = this.CreateMasterClientAndAuthenticate(this.Player3 != null ? "Player5" : null);

                if (string.IsNullOrEmpty(this.Player1) && masterClient1.Token == null)
                {
                    Assert.Ignore("This test does not work correctly for old clients without userId and token");
                }

                var joinResponse1 = masterClient1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address);
                masterClient1.JoinGame(joinRequest);

                masterClient1.WaitForEvent(EventCode.Join);

                // client 2: try to join a game which exists and is created on the game server
                var joinResponse2 = masterClient2.JoinGame(joinRequest);
                Assert.AreEqual(joinResponse1.Address, joinResponse2.Address);

                this.ConnectAndAuthenticate(masterClient2, joinResponse2.Address);
                masterClient2.JoinGame(joinRequest);

                // client 3: try to join a game which exists and is created on the game server
                joinResponse2 = masterClient3.JoinGame(joinRequest);
                Assert.AreEqual(joinResponse1.Address, joinResponse2.Address);

                this.ConnectAndAuthenticate(masterClient3, joinResponse2.Address);
                masterClient3.JoinGame(joinRequest);

                // client 4: try to join a game which exists and is created on the game server
                masterClient4.JoinGame(joinRequest);
                this.ConnectAndAuthenticate(masterClient4, joinResponse2.Address);
                masterClient4.JoinGame(joinRequest);

                masterClient5.JoinGame(joinRequest, ErrorCode.GameFull);

                Thread.Sleep(1200);

                masterClient1.Disconnect();

                masterClient4.WaitForEvent(EventCode.Leave, 400);
                // wait little longer than TestPlayerTtl to get final leave
                masterClient4.WaitForEvent(EventCode.Leave, TestPlayerTtl + 200);

                Thread.Sleep(200);
                joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                };

                this.ConnectAndAuthenticate(masterClient1, this.MasterAddress, masterClient1.UserId,
                    reuseToken: string.IsNullOrEmpty(this.Player1));
                joinResponse1 = masterClient1.JoinGame(joinRequest);
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address, masterClient1.UserId);
                masterClient1.JoinGame(joinRequest);
            }
            finally
            {
                DisposeClients(masterClient1);
                DisposeClients(masterClient2);
                DisposeClients(masterClient3);
                DisposeClients(masterClient4);
                DisposeClients(masterClient5);
            }
        }

        [Test]
        public void RejoinOrJoinFailsIfGameNotExistTest()
        {
            UnifiedTestClient masterClient1 = null;

            try
            {
                var roomName = MethodBase.GetCurrentMethod().Name;
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.RejoinOrJoin,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                var createResponse = masterClient1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, createResponse.Address);

                masterClient1.JoinGame(joinRequest, ErrorCode.GameDoesNotExist);
            }
            finally
            {
                DisposeClients(masterClient1);
            }
        }

        [Test]
        public void RejoinOnlyFailsIfGameNotExistTest()
        {
            UnifiedTestClient masterClient1 = null;

            try
            {
                var roomName = MethodBase.GetCurrentMethod().Name;
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.RejoinOnly,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                var createResponse = masterClient1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, createResponse.Address);

                masterClient1.JoinGame(joinRequest, ErrorCode.GameDoesNotExist);
            }
            finally
            {
                DisposeClients(masterClient1);
            }
        }

        [Test]
        public void JoinOnlyFailsIfGameNotExistTest()
        {
            UnifiedTestClient masterClient1 = null;

            try
            {
                var roomName = MethodBase.GetCurrentMethod().Name;
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.JoinOnly,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                masterClient1.JoinGame(joinRequest, ErrorCode.GameDoesNotExist);
            }
            finally
            {
                DisposeClients(masterClient1);
            }
        }

        [Test]
        public void RejoinExceedsMaxPlayerTest()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;
            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                var GameName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);

                var request = new CreateGameRequest
                {
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    GameId = GameName,
                    PlayerTTL = -1,
                    GameProperties = new Hashtable
                    {
                        {GameParameter.MaxPlayers, 1}
                    }
                };

                var response = client1.CreateGame(request);

                this.ConnectAndAuthenticate(client1, response.Address);

                client1.CreateGame(request);


                var joinRequest = new JoinGameRequest
                {
                    GameId = GameName,
                    JoinMode = (byte)JoinMode.JoinOrRejoin,
                };

                var joinResponse = client2.JoinGame(joinRequest);
                this.ConnectAndAuthenticate(client2, joinResponse.Address);

                client2.JoinGame(joinRequest, ErrorCode.GameFull);

                this.ConnectAndAuthenticate(client2, this.MasterAddress);

                joinRequest.JoinMode = (byte)JoinMode.RejoinOnly;
                joinResponse = client2.JoinGame(joinRequest);
                this.ConnectAndAuthenticate(client2, joinResponse.Address);
                client2.JoinGame(joinRequest, (short)Photon.Common.ErrorCode.JoinFailedWithRejoinerNotFound);
            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [Test]
        public void CreateGameWhenJoinedTest()
        {
            UnifiedTestClient client1 = null;
            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                var GameName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);

                var request = new CreateGameRequest
                {
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    GameId = GameName,
                    GameProperties = new Hashtable
                    {
                        {GameParameter.MaxPlayers, 1}
                    }
                };

                var response = client1.CreateGame(request);

                this.ConnectAndAuthenticate(client1, response.Address);

                client1.CreateGame(request);
                client1.CreateGame(request, ErrorCode.OperationNotAllowedInCurrentState);
            }
            finally
            {
                DisposeClients(client1);
            }
        }

        [TestCase(OperationCode.CreateGame)]
        [TestCase(OperationCode.JoinGame)]
        public void DisconnectAfterTooManyConcurrentJoinRequestsTest(byte operation)
        {
            UnifiedTestClient client1 = null;
            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                this.connectPolicy.UseSendDelayForOfflineTests = false;
                for (int i = 0; i < 10; ++i)
                {
                    var request = new OperationRequest
                    {
                        OperationCode = operation,
                        Parameters = new Dictionary<byte, object>
                        {
                            [ParameterCode.RoomName] = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name),
                            [ParameterCode.JoinMode] = JoinMode.CreateIfNotExists,
                        }
                    };
                    client1.SendRequest(request);
                }

                Thread.Sleep(SocketServer.PeerBase.DefaultDisconnectInterval + 500);
                Assert.That(client1.Connected, Is.False);
            }
            finally
            {
                this.connectPolicy.UseSendDelayForOfflineTests = true;
                DisposeClients(client1);
            }
        }

        [TestCase(OperationCode.CreateGame)]
        [TestCase(OperationCode.JoinGame)]
        public void DisconnectAfterTooManyTotalJoinRequestsTest(byte operation)
        {
            UnifiedTestClient client1 = null;
            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                this.connectPolicy.UseSendDelayForOfflineTests = false;

                for (int i = 0; i < 10; ++i)
                {
                    var request = new OperationRequest
                    {
                        OperationCode = operation,
                        Parameters = new Dictionary<byte, object>
                        {
                            [ParameterCode.RoomName] = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name),
                            [ParameterCode.JoinMode] = JoinMode.CreateIfNotExists,
                        }
                    };
                    client1.SendRequest(request);

                    if (!client1.TryWaitForOperationResponse(this.WaitTimeout, out _))
                    {
                        break;
                    }
                }

                //Thread.Sleep(3500);
                Assert.That(client1.Connected, Is.False);
            }
            finally
            {
                this.connectPolicy.UseSendDelayForOfflineTests = true;
                DisposeClients(client1);
            }
        }

        [Test]
        public void DisconnectAfterTooManyConcurrentJoinRandomRequestsTest()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;
            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                var cgResp = client2.CreateGame(this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name));

                this.ConnectAndAuthenticate(client2, cgResp.Address);

                client2.CreateGame(cgResp.GameId);

                Thread.Sleep(100);

                this.connectPolicy.UseSendDelayForOfflineTests = false;
                for (int i = 0; i < 10; ++i)
                {
                    var request = new OperationRequest
                    {
                        OperationCode = OperationCode.JoinRandomGame,
                        Parameters = new Dictionary<byte, object>()
                    };
                    client1.SendRequest(request);
                }

                Thread.Sleep(SocketServer.PeerBase.DefaultDisconnectInterval + 500);
                Assert.That(client1.Connected, Is.False);
            }
            finally
            {
                this.connectPolicy.UseSendDelayForOfflineTests = true;
                DisposeClients(client1, client2);
            }
        }

        [Test]
        public void DisconnectAfterTooManyTotalJoinRandomRequestsTest()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;
            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                var cgResp = client2.CreateGame(this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name));

                this.ConnectAndAuthenticate(client2, cgResp.Address);

                client2.CreateGame(cgResp.GameId);

                Thread.Sleep(100);
                this.connectPolicy.UseSendDelayForOfflineTests = false;
                for (int i = 0; i < 10; ++i)
                {
                    var request = new OperationRequest
                    {
                        OperationCode = OperationCode.JoinRandomGame,
                        Parameters = new Dictionary<byte, object>()
                    };
                    client1.SendRequest(request);

                    if (!client1.TryWaitForOperationResponse(this.WaitTimeout, out _))
                    {
                        break;
                    }
                }

                Assert.That(client1.Connected, Is.False);
            }
            finally
            {
                this.connectPolicy.UseSendDelayForOfflineTests = true;
                DisposeClients(client1, client2);
            }
        }

        [Test]
        public void LimitCreateGameRequestsTest()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;
            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                var GameName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);

                var r = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        [ParameterCode.RoomName] = GameName,
                    }
                };

                client1.SendRequestAndWaitForResponse(r);

                var request = new CreateGameRequest
                {
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    GameId = GameName,
                };
                client2.CreateGame(request, ErrorCode.GameIdAlreadyExists);

                // we failed in previous case. So, it should be allowed to send second create game request
                request.GameId = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);
                client2.CreateGame(request);
            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [Test]
        public void JoinGameWhenJoinedTest()
        {
            UnifiedTestClient client1 = null;
            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                var GameName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);

                var request = new CreateGameRequest
                {
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    GameId = GameName,
                    GameProperties = new Hashtable
                    {
                        {GameParameter.MaxPlayers, 1}
                    }
                };

                var response = client1.CreateGame(request);

                this.ConnectAndAuthenticate(client1, response.Address);

                client1.CreateGame(request);
                client1.JoinGame(request, ErrorCode.OperationNotAllowedInCurrentState);
            }
            finally
            {
                DisposeClients(client1);
            }
        }

        [Test]
        public void EmptyRoomBug()
        {
            UnifiedTestClient client1 = null;
            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                var GameName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);

                var request = new CreateGameRequest
                {
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    GameId = GameName,
                    GameProperties = new Hashtable
                    {
                        {GameParameter.MaxPlayers, 2},
                    },
                    AddUsers = new[] {"Player2"}
                };

                var response = client1.CreateGame(request);

                this.ConnectAndAuthenticate(client1, response.Address);

                client1.CreateGame(request);
                Thread.Sleep(100);

                var r = new OperationRequest
                {
                    OperationCode = OperationCode.Leave,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.IsInactive, new[]{"xxx"}},
                        {ParameterCode.EventForward, new[]{"xxx"}}
                    }
                };
                client1.SendRequestAndWaitForResponse(r, ErrorCode.InvalidOperation);

                Thread.Sleep(SocketServer.PeerBase.DefaultDisconnectInterval + 100);

                Assert.That(client1.Connected, Is.False);
            }
            finally
            {
                DisposeClients(client1);
            }
        }

        [Test]
        public void ReJoinModeTest()
        {
            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName("ReJoinModeTest_");
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    PlayerTTL = 2500,
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                var joinResponse1 = masterClient1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address, masterClient1.UserId);
                masterClient1.JoinGame(joinRequest);

                var joinEvent = masterClient1.WaitForEvent(EventCode.Join);

                // client 2: try to join a game which exists and is created on the game server
                var joinResponse2 = masterClient2.JoinGame(joinRequest);
                Assert.AreEqual(joinResponse1.Address, joinResponse2.Address);

                this.ConnectAndAuthenticate(masterClient2, joinResponse2.Address, masterClient2.UserId);
                masterClient2.JoinGame(joinRequest);

                masterClient1.LeaveGame(true);

                Thread.Sleep(200);

                joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.RejoinOnly,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    PlayerTTL = 2500,
                    ActorNr = (int)joinEvent[ParameterCode.ActorNr],
                };

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address, masterClient1.UserId);
                masterClient1.JoinGame(joinRequest);

                // leave and wait while client will be disconnected from game
                masterClient1.LeaveGame(true);
                Thread.Sleep(2700);

                // and try to rejoin this game again. should fail
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address, masterClient1.UserId);
                masterClient1.JoinGame(joinRequest, (short)Photon.Common.ErrorCode.JoinFailedWithRejoinerNotFound);

                // change join mode and rejoin again. should succeed
                joinRequest.JoinMode = JoinModeConstants.RejoinOrJoin;

                joinRequest.ActorNr = 0;
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address, masterClient1.UserId);
                masterClient1.JoinGame(joinRequest);
            }
            finally
            {
                DisposeClients(masterClient1);
                DisposeClients(masterClient2);
            }
        }

        [Test]
        [Ignore("Something wrong here")]
        public void ReJoinToNonExistingGameTest()
        {
            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.RejoinOnly,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    PlayerTTL = 1000,
                    Plugins = new []{"Webhooks"}
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                var joinResponse1 = masterClient1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address, masterClient1.UserId);
                masterClient1.JoinGame(joinRequest);

                var joinEvent = masterClient1.WaitForEvent(EventCode.Join);

                // client 2: try to join a game which exists and is created on the game server
                var joinResponse2 = masterClient2.JoinGame(joinRequest);
                Assert.AreEqual(joinResponse1.Address, joinResponse2.Address);

                this.ConnectAndAuthenticate(masterClient2, joinResponse2.Address, masterClient2.UserId);
                masterClient2.JoinGame(joinRequest);

                masterClient1.LeaveGame(true);

                Thread.Sleep(200);

                joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.RejoinOnly,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    PlayerTTL = 1000,
                    ActorNr = (int)joinEvent[ParameterCode.ActorNr],
                };

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address, masterClient1.UserId);
                masterClient1.JoinGame(joinRequest);

                // leave and wait while client will be disconnected from game
                masterClient1.LeaveGame(true);
                Thread.Sleep(1200);

                // and try to rejoin this game again. should fail
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address, masterClient1.UserId);
                masterClient1.JoinGame(joinRequest, (short)Photon.Common.ErrorCode.JoinFailedWithRejoinerNotFound);

                // change join mode and rejoin again. should succeed
                joinRequest.JoinMode = JoinModeConstants.RejoinOrJoin;

                joinRequest.ActorNr = 0;
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address, masterClient1.UserId);
                masterClient1.JoinGame(joinRequest);
            }
            finally
            {
                DisposeClients(masterClient1);
                DisposeClients(masterClient2);
            }
        }

        [Test]
        public void ReJoinModeTest2()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;
            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                var GameName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);

                var request = new CreateGameRequest
                {
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    GameId = GameName,
                    PlayerTTL = -1,
                    GameProperties = new Hashtable
                    {
                        {GameParameter.MaxPlayers, 2}
                    }
                };

                var response = client1.CreateGame(request);

                this.ConnectAndAuthenticate(client1, response.Address);

                client1.CreateGame(request);


                var joinRequest = new JoinGameRequest
                {
                    GameId = GameName,
                    JoinMode = (byte)JoinMode.RejoinOnly,
                };

                var joinResponse = client2.JoinGame(joinRequest);
                this.ConnectAndAuthenticate(client2, joinResponse.Address);

                client2.JoinGame(joinRequest, (short)Photon.Common.ErrorCode.JoinFailedWithRejoinerNotFound);

                this.ConnectAndAuthenticate(client2, this.MasterAddress);
                joinResponse = client2.JoinGame(joinRequest);
                this.ConnectAndAuthenticate(client2, joinResponse.Address);

                joinRequest.JoinMode = (byte)JoinMode.JoinOrRejoin;
                client2.JoinGame(joinRequest);
            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [TestCase(JoinMode.Default)]
        [TestCase(JoinMode.CreateIfNotExists)]
        [TestCase(JoinMode.JoinOrRejoin)]
        [TestCase(JoinMode.RejoinOnly)]
        public void JoinDuringCloseTest(JoinMode joinMode)
        {
            if (!this.UsePlugins)
            {
                Assert.Ignore("test needs plugin support");
            }
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;
            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                var GameName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);

                var request = new CreateGameRequest
                {
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    GameId = GameName,
                    GameProperties = new Hashtable
                    {
                        {GameParameter.MaxPlayers, 2}
                    },
                    Plugins = new[] { "LongOnClosePlugin"}
                };

                var response = client1.CreateGame(request);

                this.ConnectAndAuthenticate(client1, response.Address);

                client1.CreateGame(request);

                Thread.Sleep(100);

                client1.LeaveGame();

                var joinRequest = new JoinGameRequest
                {
                    GameId = GameName,
                    JoinMode = (byte)joinMode,
                };

                var joinResponse = client2.JoinGame(joinRequest);
                this.ConnectAndAuthenticate(client2, joinResponse.Address);

                var errorCode = joinMode == JoinMode.CreateIfNotExists ? 0 : ErrorCode.GameDoesNotExist;
                client2.JoinGame(joinRequest, (short)errorCode);
            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [TestCase(JoinMode.Default)]
        [TestCase(JoinMode.CreateIfNotExists)]
        [TestCase(JoinMode.JoinOrRejoin)]
        [TestCase(JoinMode.RejoinOnly)]
        public void JoinDuringCloseWithPersistenceTest(JoinMode joinMode)
        {
            if (!this.UsePlugins)
            {
                Assert.Ignore("test needs plugin support");
            }
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;
            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                var GameName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);

                var request = new CreateGameRequest
                {
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    GameId = GameName,
                    GameProperties = new Hashtable
                    {
                        {GameParameter.MaxPlayers, 2}
                    },
                    Plugins = new[] { "LongOnClosePluginWithPersistence" }
                };

                var response = client1.CreateGame(request);

                this.ConnectAndAuthenticate(client1, response.Address);

                client1.CreateGame(request);

                Thread.Sleep(100);

                client1.LeaveGame();

                var joinRequest = new JoinGameRequest
                {
                    GameId = GameName,
                    JoinMode = (byte)joinMode,
                };

                var joinResponse = client2.JoinGame(joinRequest);
                this.ConnectAndAuthenticate(client2, joinResponse.Address);

                var errorCode = joinMode == JoinMode.RejoinOnly ? ErrorCode.JoinFailedWithRejoinerNotFound : 0;
                client2.JoinGame(joinRequest, (short)errorCode);
            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [Test]
        public void ForceRejoinTest()
        {
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("this tests requires userId to be set");
            }

            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;
            UnifiedTestClient client3 = null;
            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                client3 = this.CreateMasterClientAndAuthenticate(this.Player2);

                var GameName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);

                var request = new CreateGameRequest
                {
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    GameId = GameName,
                    PlayerTTL = 100000,
                    GameProperties = new Hashtable
                    {
                        {GameParameter.MaxPlayers, 2}
                    }
                };

                var response = client1.CreateGame(request);

                this.ConnectAndAuthenticate(client1, response.Address);

                client1.CreateGame(request);

                Thread.Sleep(100);
                var joinRequest = new JoinGameRequest
                {
                    GameId = GameName,
                    JoinMode = (byte)JoinMode.JoinOrRejoin,
                };

                var joinResponse = client2.JoinGame(joinRequest);
                this.ConnectAndAuthenticate(client2, joinResponse.Address);

                client2.JoinGame(joinRequest);

                joinRequest = new JoinGameRequest
                {
                    GameId = GameName,
                    JoinMode = (byte)JoinMode.RejoinOnly,
                    ForceRejoin = true,
                    ActorNr = 2,
                };

                joinResponse = client3.JoinGame(joinRequest);
                this.ConnectAndAuthenticate(client3, joinResponse.Address);

                client3.JoinGame(joinRequest);

                Thread.Sleep(1300);// it takes time for web socket client to disconnect
                Assert.IsFalse(client2.Connected);
            }
            finally
            {
                DisposeClients(client1, client2, client3);
            }
        }

        [Test]
        public void CacheClearTest()
        {
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires userId to be set");
            }

            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;
            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                var GameName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);

                var request = new CreateGameRequest
                {
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    GameId = GameName,
                    PlayerTTL = -1,
                    GameProperties = new Hashtable
                    {
                        {GameParameter.MaxPlayers, 2}
                    }
                };

                var response = client1.CreateGame(request);

                this.ConnectAndAuthenticate(client1, response.Address);

                client1.CreateGame(request);

                Thread.Sleep(100);

                var joinRequest = new JoinGameRequest
                {
                    GameId = GameName,
                    JoinMode = (byte)JoinMode.RejoinOnly,
                };

                var joinResponse = client2.JoinGame(joinRequest);
                this.ConnectAndAuthenticate(client2, joinResponse.Address);

                client2.JoinGame(joinRequest, ErrorCode.JoinFailedWithRejoinerNotFound);
                

                this.ConnectAndAuthenticate(client2, joinResponse.Address);
                joinRequest.JoinMode = (byte)JoinMode.JoinOrRejoin;
                client2.JoinGame(joinRequest);

                Thread.Sleep(100);
                client1.LeaveGame();
                client2.LeaveGame();
            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [Test]
        public void WrongGameRemovalOnMasterTest()
        {
            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;

            if (this.connectPolicy.IsOnline)
            {
                Assert.Ignore("This is an offline test");
            }

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                var joinResponse1 = masterClient1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address, masterClient1.UserId);
                masterClient1.JoinGame(joinRequest);

                var joinEvent = masterClient1.WaitForEvent(EventCode.Join);

                masterClient1.Disconnect();

                Thread.Sleep(joinRequest.PlayerTTL + 350);

                // client 2: try to join a game which exists and is created on the game server
                int x = 0;
                while (x++ < 10)
                {
                    masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                    var joinResponse2 = masterClient2.JoinGame(joinRequest);

                    this.ConnectAndAuthenticate(masterClient2, joinResponse2.Address, masterClient2.UserId);
                    masterClient2.JoinGame(joinRequest);
                    if (joinResponse2.Address != joinResponse1.Address)
                    {
                        break;
                    }
                    DisposeClients(masterClient2);
                }

                if (x >= 10)
                {
                    Assert.Inconclusive("Can not create game on different game server");
                }

                Thread.Sleep(200);

                var joinRequest2 = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.RejoinOnly,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    PlayerTTL = 1000,
                    ActorNr = (int)joinEvent[ParameterCode.ActorNr],
                };

                // client 1: connects to GS and tries to join not existing game on the game server 
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address, masterClient1.UserId);
                masterClient1.JoinGame(joinRequest2, ErrorCode.GameDoesNotExist);

                Thread.Sleep(100);

                var joinRequest3 = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.JoinOnly,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                };
                // and try to rejoin this game again. should not fail
                this.ConnectAndAuthenticate(masterClient1, this.MasterAddress);
                masterClient1.JoinGame(joinRequest3, (short)Photon.Common.ErrorCode.Ok);

            }
            finally
            {
                DisposeClients(masterClient1);
                DisposeClients(masterClient2);
            }
        }

        [Test]
        public void RejoinWhileInGameZeroPlayerTTL()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;
            UnifiedTestClient client3 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                };

                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                var joinResponse1 = client1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(client1, joinResponse1.Address);
                client1.JoinGame(joinRequest);

                var joinEvent = client1.WaitForEvent(EventCode.Join);


                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                var joinResponse2 = client2.JoinGame(joinRequest);

                this.ConnectAndAuthenticate(client2, joinResponse2.Address, client2.UserId);
                client2.JoinGame(joinRequest);

                Thread.Sleep(200);

                // we simulate case when player reconnects but original connection still exits
                client3 = this.CreateMasterClientAndAuthenticate(this.Player2);

                var joinRequest2 = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.RejoinOnly,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    ActorNr = (int)joinEvent[ParameterCode.ActorNr],
                };

                client3.Token = client2.Token;
                this.ConnectAndAuthenticate(client3, joinResponse2.Address);
                client3.JoinGame(joinRequest2, ErrorCode.OperationNotAllowedInCurrentState);

                client3.Disconnect();

                this.ConnectAndAuthenticate(client3, joinResponse2.Address);
                joinRequest2.JoinMode = JoinModeConstants.RejoinOrJoin;
                client3.JoinGame(joinRequest2, ErrorCode.OperationNotAllowedInCurrentState);

                DisposeClients(client3);

                // just new player
                client3 = this.CreateMasterClientAndAuthenticate(this.Player3);

                client3.JoinGame(joinRequest2);
                this.ConnectAndAuthenticate(client3, joinResponse2.Address);
                client3.JoinGame(joinRequest2);
            }
            finally
            {
                DisposeClients(client1, client2, client3);
            }
        }

        #endregion

        #region Expiration of Player Ttl Tests

        [Test]
        public void PlayerTtlTimeExpiredWhileGameInStorageTest()
        {
            if (!this.UsePlugins)
            {
                Assert.Ignore("test needs plugin support");
            }
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("this tests requires userId to be set");
            }

            UnifiedTestClient masterClient = null;
            UnifiedTestClient masterClient2 = null;

            const int playerTtl = 5000;
            try
            {
                var roomName = this.GenerateRandomizedRoomName("PlayerTtlTimeTest_");
                var myRoomOptions = new RoomOptions
                {
                    PlayerTtl = playerTtl,
                    MaxPlayers = 4,
                    IsOpen = true,
                    IsVisible = true,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1)
                };

                masterClient = this.CreateMasterClientAndAuthenticate(this.Player1);
                var response = masterClient.CreateGame(roomName);
                this.ConnectAndAuthenticate(masterClient, response.Address, this.Player1);

                masterClient.CreateRoom(roomName, myRoomOptions, TypedLobby.Default, null, true, "SaveLoadStateTestPlugin");
                Thread.Sleep(300); // wait while game is created on game server

                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                };
                var jgResponse = masterClient2.JoinGame(joinRequest);

                this.ConnectAndAuthenticate(masterClient2, jgResponse.Address, this.Player2);

                Thread.Sleep(300);
                masterClient2.JoinGame(joinRequest);

                var joinEvent2 = masterClient2.WaitForEvent(EventCode.Join);
                masterClient2.LeaveGame(true); // leave game, but stay inactive there

                var masterClient2Token = masterClient2.Token;
                Thread.Sleep(3000);

                DisposeClients(masterClient2);
                masterClient.LeaveGame(true);

                Thread.Sleep(2000);

                this.ConnectAndAuthenticate(masterClient, response.Address, this.Player1);

                joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.RejoinOnly,
                    Plugins = new[] { "SaveLoadStateTestPlugin" },
                };

                masterClient.JoinGame(joinRequest);
                Thread.Sleep(100);

                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                masterClient2.Token = masterClient2Token;
                this.ConnectAndAuthenticate(masterClient2, response.Address, this.Player2);
                Thread.Sleep(300);

                joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.RejoinOnly,
                    ActorNr = (int) joinEvent2[ParameterCode.ActorNr],
                };

                masterClient2.JoinGame(joinRequest, (short)Photon.Common.ErrorCode.JoinFailedWithRejoinerNotFound);
            }
            finally
            {
                DisposeClients(masterClient);
                DisposeClients(masterClient2);
            }
        }

        [Test]
        public void PlayerTtlTimeExpiredAfterReloadingTest()
        {
            if (!this.UsePlugins)
            {
                Assert.Ignore("test needs plugin support");
            }
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("this tests requires userId to be set");
            }

            UnifiedTestClient masterClient = null;
            UnifiedTestClient masterClient2 = null;

            const int playerTtl = 5000;
            try
            {
                var roomName = this.GenerateRandomizedRoomName("PlayerTtlTimeTest_");
                var myRoomOptions = new RoomOptions
                {
                    PlayerTtl = playerTtl,
                    MaxPlayers = 4,
                    IsOpen = true,
                    IsVisible = true,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1)
                };

                masterClient = this.CreateMasterClientAndAuthenticate(this.Player1);
                var response = masterClient.CreateGame(roomName);
                this.ConnectAndAuthenticate(masterClient, response.Address, this.Player1);

                masterClient.CreateRoom(roomName, myRoomOptions, TypedLobby.Default, null, true, "SaveLoadStateTestPlugin");
                Thread.Sleep(300); // wait while game is created on game server

                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                };
                var jgResponse = masterClient2.JoinGame(joinRequest);

                this.ConnectAndAuthenticate(masterClient2, jgResponse.Address, this.Player2);

                Thread.Sleep(300);
                masterClient2.JoinGame(joinRequest);

                var joinEvent2 = masterClient2.WaitForEvent(EventCode.Join);
                masterClient2.LeaveGame(true); // leave game, but stay inactive there

                Thread.Sleep(1000);

                DisposeClients(masterClient2);
                masterClient.LeaveGame(true);

                Thread.Sleep(1000);

                this.ConnectAndAuthenticate(masterClient, response.Address, this.Player1);

                joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.RejoinOnly,
                    Plugins = new[] { "SaveLoadStateTestPlugin" },
                };

                masterClient.JoinGame(joinRequest);
                Thread.Sleep(3000);

                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                masterClient2.JoinGame(roomName);
                this.ConnectAndAuthenticate(masterClient2, response.Address, this.Player2);
                Thread.Sleep(300);

                joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.RejoinOnly,
                    ActorNr = (int) joinEvent2[ParameterCode.ActorNr],
                };

                masterClient2.JoinGame(joinRequest, (short)Photon.Common.ErrorCode.JoinFailedWithRejoinerNotFound);
            }
            finally
            {
                DisposeClients(masterClient);
                DisposeClients(masterClient2);
            }
        }

        [Test]
        public void PlayerTtlTimeNotExpiredTest()
        {
            if (!this.UsePlugins)
            {
                Assert.Ignore("test needs plugin support");
            }

            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("this tests requires userId to be set");
            }

            UnifiedTestClient masterClient = null;
            UnifiedTestClient masterClient2 = null;

            const int playerTtl = 9000;
            try
            {
                var roomName = this.GenerateRandomizedRoomName("PlayerTtlTimeTest_");
                var myRoomOptions = new RoomOptions
                {
                    PlayerTtl = playerTtl,
                    MaxPlayers = 4,
                    IsOpen = true,
                    IsVisible = true,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1)
                };

                masterClient = this.CreateMasterClientAndAuthenticate(this.Player1);
                var response = masterClient.CreateGame(roomName);
                this.ConnectAndAuthenticate(masterClient, response.Address, this.Player1);

                masterClient.CreateRoom(roomName, myRoomOptions, TypedLobby.Default, null, true, "SaveLoadStateTestPlugin");
                Thread.Sleep(300); // wait while game is created on game server

                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                };
                var jgResponse = masterClient2.JoinGame(joinRequest);

                this.ConnectAndAuthenticate(masterClient2, jgResponse.Address, this.Player2);

                Thread.Sleep(300);
                masterClient2.JoinGame(joinRequest);

                masterClient2.WaitForEvent(EventCode.Join);
                masterClient2.LeaveGame(true); // leave game, but stay inactive there

                var masterClient2Token = masterClient2.Token;
                Thread.Sleep(1000);

                DisposeClients(masterClient2);
                masterClient.LeaveGame(true);

                Thread.Sleep(1000);

                this.ConnectAndAuthenticate(masterClient, response.Address);

                joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.RejoinOnly,
                    Plugins = new[] { "SaveLoadStateTestPlugin" },
                };

                masterClient.JoinGame(joinRequest);
                Thread.Sleep(100);

                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                masterClient2.Token = masterClient2Token;

                this.ConnectAndAuthenticate(masterClient2, response.Address, this.Player2);
                Thread.Sleep(300);

                joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.RejoinOnly,
                };

                masterClient2.JoinGame(joinRequest);
            }
            finally
            {
                DisposeClients(masterClient);
                DisposeClients(masterClient2);
            }
        }

        [Test]
        public void PlayerTtlTimeExpiredForFirstPlayerBeforeReloadingTest()
        {
            if (!this.UsePlugins)
            {
                Assert.Ignore("test needs plugin support");
            }
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("this tests requires userId to be set");
            }

            UnifiedTestClient masterClient = null;
            UnifiedTestClient masterClient2 = null;

            const int playerTtl = 3000;
            try
            {
                var roomName = this.GenerateRandomizedRoomName("PlayerTtlTimeTest_");
                var myRoomOptions = new RoomOptions
                {
                    PlayerTtl = playerTtl,
                    MaxPlayers = 4,
                    IsOpen = true,
                    IsVisible = true,
                    EmptyRoomTtl = 3000,
                    CheckUserOnJoin = true
                };

                masterClient = this.CreateMasterClientAndAuthenticate(this.Player1);
                var response = masterClient.CreateGame(roomName);
                this.ConnectAndAuthenticate(masterClient, response.Address, this.Player1);

                masterClient.CreateRoom(roomName, myRoomOptions, TypedLobby.Default, null, true, "SaveLoadStateTestPlugin");
                Thread.Sleep(300); // wait while game is created on game server

                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                };
                var jgResponse = masterClient2.JoinGame(joinRequest);

                this.ConnectAndAuthenticate(masterClient2, jgResponse.Address, this.Player2);

                Thread.Sleep(300);
                masterClient2.JoinGame(joinRequest);

                var joinEvent2 = masterClient2.WaitForEvent(EventCode.Join);
                masterClient2.LeaveGame(true); // leave game, but stay inactive there

                // we do this trick to pass around token check
                var masterClient2Token = masterClient2.Token;

                DisposeClients(masterClient2);
                masterClient.LeaveGame(true);

                Thread.Sleep(3600);

                this.ConnectAndAuthenticate(masterClient, response.Address, this.Player1);

                var createRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    PlayerTTL = int.MaxValue,
                    ActorNr = 1,
                    Plugins = new[] { "SaveLoadStateTestPlugin" }
                };

                masterClient.CreateGame(createRequest, (short)Photon.Common.ErrorCode.JoinFailedWithRejoinerNotFound); 

                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                masterClient2.Token = masterClient2Token;

                this.ConnectAndAuthenticate(masterClient2, response.Address, this.Player2);

                joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.RejoinOnly,
                    ActorNr = (int) joinEvent2[ParameterCode.ActorNr],
                    Plugins = new[] { "SaveLoadStateTestPlugin" }
                };

                masterClient2.JoinGame(joinRequest, (short)Photon.Common.ErrorCode.JoinFailedWithRejoinerNotFound);
            }
            finally
            {
                DisposeClients(masterClient);
                DisposeClients(masterClient2);
            }
        }

        #endregion

        #region Bann tests

        [Test]
        public void BanPlayerTest()
        {
            if (!this.UsePlugins)
            {
                Assert.Ignore("test needs plugin support");
            }

            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires user id to be set");
            }
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;

            var gameName = MethodBase.GetCurrentMethod().Name;

            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                var createGame = new CreateGameRequest
                {
                    GameId = gameName,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    Plugins = new[] { "BanTestPlugin"},
                };

                var response = client1.CreateGame(createGame);

                this.ConnectAndAuthenticate(client1, response.Address);

                client1.CreateGame(createGame);

                Thread.Sleep(300);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                var joinGame = new JoinGameRequest
                {
                    GameId = gameName,
                };

                var joinGameResponse = client2.JoinGame(joinGame);

                this.ConnectAndAuthenticate(client2, joinGameResponse.Address);

                client2.JoinGame(joinGame);

                //all joined lets ban

                var raiseEventData = new Hashtable
                {
                    {0, true},
                    {1, 2}
                };

                var operation = new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Data, raiseEventData}
                    }
                };

                client1.SendRequest(operation);

                Thread.Sleep(SocketServer.PeerBase.DefaultDisconnectInterval + 500);
                // ha ha ha, we banned him
                Assert.IsFalse(client2.Connected);


                //client2 tries rejoin immediately and ... fails
                this.ConnectAndAuthenticate(client2, joinGameResponse.Address);
                joinGame.JoinMode = (byte)JoinMode.JoinOrRejoin;
                client2.JoinGame(joinGame, (short)Photon.Common.ErrorCode.JoinFailedFoundExcludedUserId);

                //reconnect to master and rejoin through master and ... fails
                this.ConnectAndAuthenticate(client2, this.MasterAddress);

                client2.JoinGame(joinGame, (short)Photon.Common.ErrorCode.JoinFailedFoundExcludedUserId);

                // try to create game
                client2.CreateGame(createGame, ErrorCode.GameIdAlreadyExists);

                joinGame.JoinMode = (byte)JoinMode.CreateIfNotExists;
                client2.JoinGame(joinGame, (short)Photon.Common.ErrorCode.JoinFailedFoundExcludedUserId);

                client2.JoinRandomGame(null, 0, null, MatchmakingMode.RandomMatching, string.Empty, AppLobbyType.Default, null, ErrorCode.NoRandomMatchFound);
            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [Test]
        public void GlobalBanPlayerTest()
        {
            if (!this.UsePlugins)
            {
                Assert.Ignore("test needs plugin support");
            }

            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires user id to be set");
            }
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;

            var banUserId = Guid.NewGuid().ToString();

            var gameName = MethodBase.GetCurrentMethod().Name;

            // I'm setting here auth once policy to get encryption data in token.
            // we do not put encryption data into token if non auth once request is used
            var oldPolicy = this.authPolicy;
            this.authPolicy = AuthPolicy.UseAuthOnce;
            try
            {
                var authParams = new Dictionary<byte, object>
                {
                    {ParameterCode.EncryptionMode, (byte)EncryptionMode.PayloadEncryption}
                };
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1, authParams);

                var createGame = new CreateGameRequest
                {
                    GameId = gameName,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    Plugins = new[] { "BanTestPlugin" },
                };

                var response = client1.CreateGame(createGame);

                this.ConnectAndAuthenticate(client1, response.Address);

                client1.CreateGame(createGame);

                Thread.Sleep(300);
                authParams = new Dictionary<byte, object>
                {
                    {ParameterCode.EncryptionMode, (byte)EncryptionMode.PayloadEncryption}
                };
                client2 = this.CreateMasterClientAndAuthenticate(banUserId, authParams);

                var joinGame = new JoinGameRequest
                {
                    GameId = gameName,
                };

                var joinGameResponse = client2.JoinGame(joinGame);

                this.ConnectAndAuthenticate(client2, joinGameResponse.Address);

                client2.JoinGame(joinGame);

                //all joined, trigger global ban
                var raiseEventData = new Hashtable
                {
                    {0, false},
                    {1, 2},
                    {2, true},
                };

                var operation = new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Data, raiseEventData}
                    }
                };

                client1.SendRequest(operation);

                Thread.Sleep(SocketServer.PeerBase.DefaultDisconnectInterval + 500);
                //client2 was banned
                Assert.IsFalse(client2.Connected);

                //client2 tries rejoin immediately and fails
                this.ConnectAndAuthenticate(client2, joinGameResponse.Address);
                joinGame.JoinMode = (byte)JoinMode.JoinOrRejoin;
                client2.JoinGame(joinGame, (short)Photon.Common.ErrorCode.JoinFailedFoundExcludedUserId);

                //reconnect to master and decline token authentication (valid token in init request)
                this.ConnectToServer(client2, this.MasterAddress, null, client2.Token);
                var authResponse = client2.WaitForOperationResponse();
                Assert.AreEqual((byte)OperationCode.AuthenticateOnce, authResponse.OperationCode);
                Assert.AreEqual((short)Photon.Common.ErrorCode.UserBlocked, authResponse.ReturnCode);

                //reconnect to master and decline token authentication (valid token in authenticate operation)
                this.ConnectToServer(client2, this.MasterAddress);
                client2.Authenticate(client2.UserId, new Dictionary<byte, object>{{ParameterCode.Token, client2.Token}}, (short) Photon.Common.ErrorCode.UserBlocked);
            }
            finally
            {
                DisposeClients(client1, client2);

                this.authPolicy = oldPolicy;
            }
        }
        #endregion

        #region WebRpc

        [Test]
        public void WebRpcCall()
        {
            if (this.connectPolicy.IsOnline)
            {
                Assert.Ignore("This is an offline test");
            }

            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires user id to be set");
            }

            UnifiedTestClient client1 = null;

            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                var webRpcRequest = new OperationRequest
                {
                    OperationCode = OperationCode.WebRpc,
                    Parameters = new Dictionary<byte, object>
                    {
                        {(byte)ParameterKey.UriPath, "GetGameList?"},
                        {(byte)ParameterKey.RpcCallParams, new Dictionary<string, object>
                            {
                                {"AppId", this.connectPolicy.ApplicationId},
                                {"AppVersion", this.connectPolicy.ApplicationVersion},
                                {"Region", this.connectPolicy.Region},
                                {"UserId", this.Player1}
                            }
                        },
                    }
                };

                client1.SendRequestAndWaitForResponse(webRpcRequest);
            }
            finally
            {
                DisposeClients(client1);
            }
        }

        [Test]
        public void WebRpcCallWrongUriPath()
        {
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires user id to be set");
            }

            UnifiedTestClient client1 = null;

            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                var webRpcRequest = new OperationRequest
                {
                    OperationCode = OperationCode.WebRpc,
                    Parameters = new Dictionary<byte, object>
                    {
                        {(byte)ParameterKey.UriPath, "WrongUriPath?"},
                        {(byte)ParameterKey.RpcCallParams, new Dictionary<string, object>
                            {
                                {"AppId", this.connectPolicy.ApplicationId},
                                {"AppVersion", this.connectPolicy.ApplicationVersion},
                                {"Region", this.connectPolicy.Region},
                                {"UserId", this.Player1}
                            }
                        },
                    }
                };

                var result = client1.SendRequestAndWaitForResponse(webRpcRequest, ErrorCode.ExternalHttpCallFailed);
                Assert.IsNotNull(result);
                Assert.IsTrue(result.DebugMessage.Contains("ProtocolError"), result.DebugMessage);
            }
            finally
            {
                DisposeClients(client1);
            }
        }


        [Test]
        public void RequestToGameSparks()
        {
            //var url = "http://preview.gamesparks.net/callback/286806FRSf6P/lBdmFptAC0V2wjxYFqGYlUScSYA7W3P9/?path=GameProperties?";
            var url = "http://requestb.in/u6u9ebu6";
            var queue = new HttpRequestQueue();
            queue.Enqueue(url, new byte [10], this.ResponseCallBack, null);
            Thread.Sleep(3000);
        }


        private void ResponseCallBack(HttpRequestQueueResultCode result, AsyncHttpRequest response, object state)
        {
            Console.WriteLine("xxxx and yyyy");
        }

        #endregion

        #region Groups

        [Test]
        public void Groups_AddManyTimesToSameGroup()
        {
            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    PlayerTTL = int.MaxValue,
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                var joinResponse1 = masterClient1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address, masterClient1.UserId);
                masterClient1.JoinGame(joinRequest);

                masterClient1.WaitForEvent(EventCode.Join);

                // client 2: try to join a game which exists and is created on the game server
                var joinResponse2 = masterClient2.JoinGame(joinRequest);
                Assert.AreEqual(joinResponse1.Address, joinResponse2.Address);

                this.ConnectAndAuthenticate(masterClient2, joinResponse2.Address, masterClient2.UserId);
                masterClient2.JoinGame(joinRequest);

                masterClient1.SendRequest(new OperationRequest
                {
                    OperationCode = OperationCode.ChangeGroups,
                    Parameters = new Dictionary<byte, object>
                    {
                        { (byte)ParameterKey.GroupsForAdd, new byte[] {1, 1} }
                    }
                });

                Thread.Sleep(100);
                masterClient2.SendRequest(new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        { (byte)ParameterKey.Group, (byte)1},
                        {(byte)ParameterKey.Code, (byte)2 }
                    }
                });

                Assert.That(masterClient1.TryWaitForEvent(2, this.WaitTimeout, out _), Is.True);
                Assert.That(masterClient1.TryWaitForEvent(2, this.WaitTimeout, out _), Is.False);
            }
            finally
            {
                DisposeClients(masterClient1);
                DisposeClients(masterClient2);
            }
        }

        [Test]
        public void Groups_RemoveFromGroup()
        {
            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    PlayerTTL = int.MaxValue,
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                var joinResponse1 = masterClient1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address, masterClient1.UserId);
                masterClient1.JoinGame(joinRequest);

                masterClient1.WaitForEvent(EventCode.Join);

                // client 2: try to join a game which exists and is created on the game server
                var joinResponse2 = masterClient2.JoinGame(joinRequest);
                Assert.AreEqual(joinResponse1.Address, joinResponse2.Address);

                this.ConnectAndAuthenticate(masterClient2, joinResponse2.Address, masterClient2.UserId);
                masterClient2.JoinGame(joinRequest);

                masterClient1.SendRequest(new OperationRequest
                {
                    OperationCode = OperationCode.ChangeGroups,
                    Parameters = new Dictionary<byte, object>
                    {
                        { (byte)ParameterKey.GroupsForAdd, new byte[] {1} }
                    }
                });

                Thread.Sleep(100);

                masterClient2.SendRequest(new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        { (byte)ParameterKey.Group, (byte)1},
                        {(byte)ParameterKey.Code, (byte)2 }
                    }
                });

                Assert.That(masterClient1.TryWaitForEvent(2, this.WaitTimeout, out _), Is.True);

                Thread.Sleep(100);
                // check removing
                masterClient1.SendRequest(new OperationRequest
                {
                    OperationCode = OperationCode.ChangeGroups,
                    Parameters = new Dictionary<byte, object>
                    {
                        { (byte)ParameterKey.GroupsForRemove, new byte[] {1} }
                    }
                });

                Thread.Sleep(50);
                masterClient2.SendRequest(new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        { (byte)ParameterKey.Group, (byte)1},
                        {(byte)ParameterKey.Code, (byte)2 }
                    }
                });

                Assert.That(masterClient1.TryWaitForEvent(2, this.WaitTimeout, out _), Is.False);
            }
            finally
            {
                DisposeClients(masterClient1);
                DisposeClients(masterClient2);
            }
        }

        [Test]
        public void Groups_AddRemoveToFromAllGroups()
        {
            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    PlayerTTL = int.MaxValue,
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                var joinResponse1 = masterClient1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address, masterClient1.UserId);
                masterClient1.JoinGame(joinRequest);

                masterClient1.WaitForEvent(EventCode.Join);

                // client 2: try to join a game which exists and is created on the game server
                var joinResponse2 = masterClient2.JoinGame(joinRequest);
                Assert.AreEqual(joinResponse1.Address, joinResponse2.Address);

                this.ConnectAndAuthenticate(masterClient2, joinResponse2.Address, masterClient2.UserId);
                masterClient2.JoinGame(joinRequest);

                masterClient1.SendRequest(new OperationRequest
                {
                    OperationCode = OperationCode.ChangeGroups,
                    Parameters = new Dictionary<byte, object>
                    {
                        { (byte)ParameterKey.GroupsForAdd, new byte[] {1, 2, 3} }
                    }
                });

                Thread.Sleep(300);
                //add to all groups
                masterClient2.SendRequest(new OperationRequest
                {
                    OperationCode = OperationCode.ChangeGroups,
                    Parameters = new Dictionary<byte, object>
                    {
                        { (byte)ParameterKey.GroupsForAdd, new byte[] {} }
                    }
                });

                Thread.Sleep(300);
                for (byte i = 1; i < 4; i++)
                {
                    masterClient1.SendRequest(new OperationRequest
                    {
                        OperationCode = OperationCode.RaiseEvent,
                        Parameters = new Dictionary<byte, object>
                        {
                            { (byte)ParameterKey.Group, i},
                            {(byte)ParameterKey.Code, (byte)2 }
                        }
                    });

                    Assert.That(masterClient2.TryWaitForEvent(2, this.WaitTimeout, out _), Is.True, "Failed on iteration {0}", i);
                }

                //remove from all groups
                masterClient2.SendRequest(new OperationRequest
                {
                    OperationCode = OperationCode.ChangeGroups,
                    Parameters = new Dictionary<byte, object>
                    {
                        { (byte)ParameterKey.GroupsForRemove, new byte[] {} }
                    }
                });

                Thread.Sleep(400);

                for (byte i = 1; i < 4; i++)
                {
                    masterClient1.SendRequest(new OperationRequest
                    {
                        OperationCode = OperationCode.RaiseEvent,
                        Parameters = new Dictionary<byte, object>
                        {
                            { (byte)ParameterKey.Group, i},
                            {(byte)ParameterKey.Code, (byte)2 }
                        }
                    });

                    Assert.That(masterClient2.TryWaitForEvent(2, this.WaitTimeout, out _), Is.False);
                }
            }
            finally
            {
                DisposeClients(masterClient1);
                DisposeClients(masterClient2);
            }
        }

        #endregion

        [Test]
        public void MultipleEmptyGames()
        {
            UnifiedTestClient client1 = null;

            try
            {
                string roomName = Guid.NewGuid().ToString();

                // create room 
                client1 = this.CreateGameOnGameServer(this.Player1, roomName, null, 0, false, true, 0, null, null);

                var gameProperties = new Hashtable { { GamePropertyKey.IsOpen, true }, { GamePropertyKey.MaxPlayers, 0 } };
                var createRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    GameProperties = gameProperties,
                };

                createRequest.GameId = Guid.NewGuid().ToString();
                client1.CreateGame(createRequest, (short)Photon.Common.ErrorCode.OperationDenied);

                Thread.Sleep(5100);

                Assert.That(client1.Connected, Is.False);
            }
            finally
            {
                DisposeClients(client1);
            }
        }

        protected virtual string GetApplicationId()
        {
            return "Some appId";
        }

        [Test]
        public void GetRegionsNameServerTest()
        {
            var client1 = (UnifiedTestClient)this.CreateTestClient();
            client1.UserId = this.Player1;

            try
            {
                var appId = this.GetApplicationId();

                var connPolicy = (LBConnectPolicyBase)this.connectPolicy;
                this.ConnectToServer(client1, connPolicy.NameServerAddress);

                var response = client1.SendRequestAndWaitForResponse(new OperationRequest
                {
                    OperationCode =  OperationCode.GetRegions,
                    Parameters = new Dictionary<byte, object>
                        {{(byte) ParameterKey.ApplicationId, appId}}
                });

                Assert.That(response, Is.Not.Null);
            }
            finally
            {
                DisposeClients(client1);
            }
        }

        [Test]
        public void SendInvalidOperationToGame()
        {
            UnifiedTestClient client1 = null;
            var gameName = MethodBase.GetCurrentMethod().Name;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(gameName);
                var createRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1)
                };

                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                var response = client1.CreateGame(createRequest);
                this.ConnectAndAuthenticate(client1, response.Address, this.Player1);
                client1.CreateGame(createRequest);
                Thread.Sleep(300); // wait while game is created on game server

                client1.SendRequestAndWaitForResponse(
                    new OperationRequest
                    {
                        OperationCode = OperationCode.SetProperties
                    }, ErrorCode.InvalidOperation);

                Thread.Sleep(5500);

                Assert.That(client1.Connected, Is.True);
            }
            finally
            {
                DisposeClients(client1);
            }
        }

        [Test]
        public void SendUnknownOperationToGame()
        {
            UnifiedTestClient client1 = null;
            var gameName = MethodBase.GetCurrentMethod().Name;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(gameName);
                var createRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1)
                };

                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                var response = client1.CreateGame(createRequest);
                this.ConnectAndAuthenticate(client1, response.Address, this.Player1);
                client1.CreateGame(createRequest);
                Thread.Sleep(300); // wait while game is created on game server

                client1.SendRequestAndWaitForResponse(
                    new OperationRequest
                    {
                        OperationCode = 216
                    }, ErrorCode.InvalidOperation);

                Thread.Sleep(5500);

                Assert.That(client1.Connected, Is.True);
            }
            finally
            {
                DisposeClients(client1);
            }
        }

        #region Helpers

        protected JoinGameResponse ConnectClientToGame(UnifiedTestClient client, string roomName, int actorNr = 0, string nickName = "")
        {
            var joinRequest = new JoinGameRequest
            {
                GameId = roomName,
            };
            if (!string.IsNullOrEmpty(nickName))
            {
                joinRequest.ActorProperties = new Hashtable
                {
                    {(byte) 255, nickName}
                };
                joinRequest.BroadcastActorProperties = true;
            }

            if (actorNr > 0)
            {
                joinRequest.ActorNr = actorNr;
            }
            // request to master
            var jgResponse = client.JoinGame(joinRequest);

            // connect to GS
            this.ConnectAndAuthenticate(client, jgResponse.Address, client.UserId);

            // request to GS
            return client.JoinGame(joinRequest);
        }

        private static void FillEventsCache(UnifiedTestClient masterClient)
        {
            // put events in slice == 0
            masterClient.SendRequest(new OperationRequest
            {
                OperationCode = OperationCode.RaiseEvent,
                Parameters = new Dictionary<byte, object>
                {
                    {ParameterCode.Code, (byte) 1},
                    {ParameterCode.Cache, (byte)EventCaching.AddToRoomCache},
                }
            });

            // increment slice
            masterClient.SendRequest(new OperationRequest
            {
                OperationCode = OperationCode.RaiseEvent,
                Parameters = new Dictionary<byte, object>
                {
                    {ParameterCode.Cache, (byte)EventCaching.SliceIncreaseIndex},
                    {ParameterCode.Code, (byte) 2},
                }
            });

            // put events to slice == 1
            masterClient.SendRequest(new OperationRequest
            {
                OperationCode = OperationCode.RaiseEvent,
                Parameters = new Dictionary<byte, object>
                {
                    {ParameterCode.Code, (byte) 3},
                    {ParameterCode.Cache, (byte) EventCaching.AddToRoomCache},
                }
            });
        }

        private void WaitUntilEmptyGameList(int timeout = 5000)
        {
            UnifiedTestClient client = null;
            var time = 0;

            while (time < timeout)
            {
                Hashtable gameList = new Hashtable();
                try
                {
                    client = this.CreateMasterClientAndAuthenticate("GameCheckUser");
                    client.JoinLobby();

                    var ev = client.WaitForEvent((byte) Events.EventCode.GameList);
                    Assert.AreEqual((byte) Events.EventCode.GameList, ev.Code);
                    gameList = (Hashtable) ev.Parameters[ParameterCode.GameList];

                }
                catch(TimeoutException )
                { }
                finally
                {
                    DisposeClients(client);
                }

                int openGames = 0;
                foreach (DictionaryEntry item in gameList)
                {
                    var gameProperties = (Hashtable)item.Value;
                    if (!gameProperties.ContainsKey(GamePropertyKey.Removed))
                    {
                        openGames++;
                    }
                }

                if (openGames == 0)
                {
                    Console.WriteLine("----End of Game list check----");
                    return;
                }
                Thread.Sleep(100);
                time += 150;
            }
            Console.WriteLine("----End of Game list check----");

            Assert.Fail("Timeout {0} ms expired. Server still has games", timeout);

        }

        private void CheckGameListCount(int expectedGameCount, Hashtable gameList = null)
        {
            if (gameList == null)
            {
                UnifiedTestClient client = null;

                try
                {
                    client = this.CreateMasterClientAndAuthenticate(this.Player1);
                    client.JoinLobby();

                    var ev = client.WaitForEvent((byte)Events.EventCode.GameList);
                    Assert.AreEqual((byte)Events.EventCode.GameList, ev.Code);
                    gameList = (Hashtable)ev.Parameters[ParameterCode.GameList];
                }
                finally
                {
                    DisposeClients(client);
                }
            }

            int openGames = 0;
            foreach (DictionaryEntry item in gameList)
            {
                var gameProperties = (Hashtable)item.Value;
                if (!gameProperties.ContainsKey(GamePropertyKey.Removed))
                {
                    openGames++;
                }
            }

            if (expectedGameCount > 0 && openGames == 0)
            {
                Assert.Fail("Expected {0} games listed in lobby, but got: 0", expectedGameCount);
            }

            if (openGames != expectedGameCount)
            {
                var gameNames = new string[gameList.Count];
                gameList.Keys.CopyTo(gameNames, 0);
                var msg = $"Expected {expectedGameCount} open games, but got {openGames}: {string.Join(",", gameNames)}";
                Assert.Fail(msg);
            }
        }

        private void CreateRoomOnGameServer(UnifiedTestClient masterClient, string roomName)
        {
            this.CreateRoomOnGameServer(masterClient, true, true, 0, roomName);
        }

        private void CreateRoomOnGameServer(
            UnifiedTestClient masterClient,
            bool isVisible,
            bool isOpen,
            byte maxPlayers,
            string roomName)
        {

            var createGameResponse = masterClient.CreateGame(roomName, isVisible, isOpen, maxPlayers);

            this.ConnectAndAuthenticate(masterClient, createGameResponse.Address, masterClient.UserId);
            masterClient.CreateGame(roomName, true, true, maxPlayers);

            // get own join event: 
            var ev = masterClient.WaitForEvent();
            Assert.AreEqual(EventCode.Join, ev.Code);
            Assert.AreEqual(1, ev.Parameters[ParameterCode.ActorNr]);
        }

        protected UnifiedTestClient CreateGameOnGameServer(
            string userName,
            string roomName,
            string lobbyName,
            byte lobbyType,
            bool? isVisible,
            bool? isOpen,
            byte? maxPlayer,
            Hashtable gameProperties,
            string[] lobbyProperties, int RoomTTL = 0, bool checkUserOnJoin = false)
        {
            var createRequest = new CreateGameRequest
            {
                GameId = roomName,
                GameProperties = gameProperties,
                LobbyName = lobbyName,
                LobbyType = lobbyType,
                EmptyRoomLiveTime = RoomTTL,
                CheckUserOnJoin = checkUserOnJoin,
            };

            if (createRequest.GameProperties == null)
            {
                createRequest.GameProperties = new Hashtable();
            }

            if (isVisible.HasValue)
            {
                createRequest.GameProperties[GamePropertyKey.IsVisible] = isVisible.Value;
            }

            if (isOpen.HasValue)
            {
                createRequest.GameProperties[GamePropertyKey.IsOpen] = isOpen.Value;
            }

            if (maxPlayer.HasValue)
            {
                createRequest.GameProperties[GamePropertyKey.MaxPlayers] = maxPlayer.Value;
            }

            if (lobbyProperties != null)
            {
                createRequest.GameProperties[GamePropertyKey.PropsListedInLobby] = lobbyProperties;
            }


            return this.CreateGameOnGameServer(userName, createRequest);
        }

        protected UnifiedTestClient CreateGameOnGameServer(string userName, CreateGameRequest createRequest)
        {
            UnifiedTestClient client = null;
            var gameCreated = false;

            try
            {
                client = this.CreateMasterClientAndAuthenticate(userName);
                var response = client.CreateGame(createRequest);

                this.ConnectAndAuthenticate(client, response.Address);
                client.CreateGame(createRequest);
                gameCreated = true;

                // in order to give server some time to update data about game on master server
                Thread.Sleep(100);
            }
            finally
            {
                if (!gameCreated)
                {
                    DisposeClients(client);
                }
            }

            return client;
        }

        protected string GenerateRandomizedRoomName(string roomName)
        {
            return (string.IsNullOrEmpty(this.GameNamePrefix) ? string.Empty : this.GameNamePrefix + "_") + this.GenerateRandomString(roomName);
        }

        private static T GetParameter<T>(ParameterDictionary parameterDict, byte parameterCode, string parameterName = null)
        {
            string paramText;
            if (string.IsNullOrEmpty(parameterName))
            {
                paramText = parameterCode.ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                paramText = $"{parameterName} ({parameterCode})";
            }

            if (parameterDict.TryGetValue(parameterCode, out object value) == false)
            {

                Assert.Fail("{0} parameter is missing", paramText);
            }

            Assert.IsInstanceOf<T>(value, "{0} parameter has wrong type.", paramText);
            return (T)value;
        }

        private void VerifyLobbyStatisticsFullList(GetLobbyStatsResponse response, string[] expectedLobbyNames, byte[] expectedLobbyTypes, int[] expectedPeerCount, int[] expectedGameCount)
        {
            // verify that all parameters are set when getting all lobby stats
            Assert.IsNotNull(response.LobbyNames, "LobbyNames missing");
            Assert.IsNotNull(response.LobbyTypes, "LobbyTypes missing");
            Assert.IsNotNull(response.LobbyNames, "PeerCount missing");
            Assert.IsNotNull(response.LobbyTypes, "GameCount missing");

            // verify that count of all parameters are equal
            Assert.AreEqual(response.LobbyNames.Length, response.LobbyTypes.Length, "LobbyTypes count does not match LobbyNames count");
            Assert.AreEqual(response.LobbyNames.Length, response.PeerCount.Length, "PeerCount count does not match LobbyNames count");
            Assert.AreEqual(response.LobbyNames.Length, response.GameCount.Length, "GameCount count does not match LobbyNames count");

            // try to find expected lobbies
            for (int i = 0; i < expectedLobbyNames.Length; i++)
            {
                int lobbyIndex = -1;
                for (int j = 0; j < response.LobbyNames.Length; j++)
                {
                    if (response.LobbyNames[j] == expectedLobbyNames[i] && response.LobbyTypes[j] == expectedLobbyTypes[i])
                    {
                        lobbyIndex = j;
                        break;
                    }
                }

                Assert.GreaterOrEqual(lobbyIndex, 0, "Lobby not found in statistics: name={0}, type={1}", expectedLobbyNames[i], expectedLobbyTypes[i]);
                Assert.AreEqual(expectedPeerCount[i], response.PeerCount[lobbyIndex], "Unexpected peer count");
                Assert.AreEqual(expectedGameCount[i], response.GameCount[lobbyIndex], "Unexpected game count");
            }
        }

        private void VerifyLobbyStatisticsList(GetLobbyStatsResponse response, int[] expectedPeerCount, int[] expectedGameCount)
        {
            // verify that all parameters are set when getting all lobby stats
            Assert.IsNull(response.LobbyNames, "LobbyNames are unexpected ");
            Assert.IsNull(response.LobbyTypes, "LobbyTypes are unexpected ");
            Assert.IsNotNull(response.PeerCount, "PeerCount missing");
            Assert.IsNotNull(response.GameCount, "GameCount missing");

            // verify that count of all parameters are equal
            Assert.AreEqual(expectedPeerCount, response.PeerCount, "Unexpected PeerCounts");
            Assert.AreEqual(expectedGameCount, response.GameCount, "Unexpected GameCounts");
        }

        private static bool RepetitiveCheck(Func<bool, bool> checkFunc, int times)
        {
            var i = 0;
            while (i++ <= times && !checkFunc(i == times))
            {
                if (i == times)
                {
                    return false;
                }
            }
            return true;
        }

        private static bool CheckAppStatEvent(UnifiedTestClient client, int masterPeerCount, int peerCount, int gameCount, bool finalCheck)
        {
            var appStatEvent = client.WaitForEvent(EventCode.AppStats, 12000);
            Assert.AreEqual(EventCode.AppStats, appStatEvent.Code, "Event Code");

            if (peerCount != (int)appStatEvent.Parameters[ParameterCode.PeerCount])
            {
                if (finalCheck)
                {
                    Assert.Fail("Wrong peer count on GS. Expected={0}, got:{1}",
                        masterPeerCount, appStatEvent.Parameters[ParameterCode.PeerCount]);
                }
                return false;
            }

            if (masterPeerCount != (int)appStatEvent.Parameters[ParameterCode.MasterPeerCount])
            {
                if (finalCheck)
                {
                    Assert.Fail("Wrong peer count on Master. Expected={0}, got:{1}",
                        masterPeerCount, appStatEvent.Parameters[ParameterCode.MasterPeerCount]);
                }
                return false;
            }

            if (gameCount != (int)appStatEvent.Parameters[ParameterCode.GameCount])
            {
                if (finalCheck)
                {
                    Assert.Fail("Wrong game count. Expected={0}, got:{1}",
                        masterPeerCount, appStatEvent.Parameters[ParameterCode.MasterPeerCount]);
                }
                return false;
            }

            return true;
        }

        static void Assert_IsOneOf(int[] expectedValues, int actual, string message)
        {
            if (expectedValues.Any(expected => expected == actual))
            {
                return;
            }

            Assert.Fail("{2} Expected one of '{0}', but got {1}", string.Join(",", expectedValues), actual, message);
        }

        #endregion
    }
}