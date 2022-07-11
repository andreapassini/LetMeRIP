using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using ExitGames.Client.Photon;
using Photon.Realtime;
using NUnit.Framework;
using Photon.Hive.Operations;
using Photon.Hive.Plugin;
using Photon.LoadBalancing.Operations;
using Photon.LoadBalancing.UnifiedClient;
using ErrorCode = Photon.Realtime.ErrorCode;
using EventCode = Photon.Realtime.EventCode;
using OperationCode = Photon.Realtime.OperationCode;
using ParameterCode = Photon.Realtime.ParameterCode;

namespace Photon.LoadBalancing.UnitTests.UnifiedTests
{
    public abstract partial class LBApiTestsImpl
    {
        private const string SlotsLobbyName = "SlotsLobby";
        #region Slot Reservation Tests

        [Test]
        public void Slots_SimpleSlotReservationTest()
        {
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires userId to be set");
            }

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;
            UnifiedTestClient masterClient3 = null;
            UnifiedTestClient masterClient4 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_");
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    PlayerTTL = int.MaxValue,
                    GameProperties = new Hashtable
                    {
                        {GamePropertyKey.MaxPlayers, 3}
                    },
                    AddUsers = new []{ this.Player2, this.Player3}
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                masterClient3 = this.CreateMasterClientAndAuthenticate(this.Player3);
                masterClient4 = this.CreateMasterClientAndAuthenticate("Player4");

                var joinResponse1 = masterClient1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address);
                masterClient1.JoinGame(joinRequest);

                masterClient1.WaitForEvent(EventCode.Join);

                Thread.Sleep(100);
                var lobbyStatsResponse = masterClient4.GetLobbyStats(null, null);
                Assert.AreEqual(3, lobbyStatsResponse.PeerCount[0]);

                var joinRequest2 = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.JoinOnly,
                    CheckUserOnJoin = true,
                };

                joinResponse1 = masterClient2.JoinGame(joinRequest2);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient2, joinResponse1.Address);
                masterClient2.JoinGame(joinRequest2);

                masterClient4.JoinGame(joinRequest2, ErrorCode.GameFull);

                joinRequest2.JoinMode = JoinModeConstants.RejoinOrJoin;
                this.ConnectAndAuthenticate(masterClient4, this.MasterAddress);
                joinResponse1 = masterClient4.JoinGame(joinRequest2);
                this.ConnectAndAuthenticate(masterClient4, joinResponse1.Address);
                masterClient4.JoinGame(joinRequest2, ErrorCode.GameFull);

                joinRequest2.JoinMode = JoinModeConstants.CreateIfNotExists;
                this.ConnectAndAuthenticate(masterClient4, this.MasterAddress);
                masterClient4.JoinGame(joinRequest2, ErrorCode.GameFull);

                joinRequest2.JoinMode = JoinModeConstants.JoinOnly;
                masterClient4.JoinGame(joinRequest2, ErrorCode.GameFull);

                this.ConnectAndAuthenticate(masterClient4, this.MasterAddress);
                masterClient4.JoinRandomGame(new JoinRandomGameRequest(), ErrorCode.NoRandomMatchFound);
            }
            finally 
            {
                DisposeClients(masterClient1, masterClient2, masterClient3, masterClient4);
            }
        }

        [Test]
        public void Slots_CreateGameInvalidSlotsCountTest()
        {
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires userId to be set");
            }
            UnifiedTestClient masterClient1 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_");
                var createRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = true,
                    PlayerTTL = int.MaxValue,
                    GameProperties = new Hashtable
                    {
                        {GamePropertyKey.MaxPlayers, 2}
                    },
                    AddUsers = new[] { this.Player2, this.Player3 }
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                masterClient1.CreateGame(createRequest, ErrorCode.InvalidOperation);
            }
            finally
            {
                DisposeClients(masterClient1);
            }
        }

        [Test]
        public void Slots_CreateGameInvalidSlotsCountOnGSTest()
        {
            if (this.IsOnline)
            {
                Assert.Ignore("this test works only in offline mode");
            }

            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires userId to be set");
            }
            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_");
                var createRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = true,
                    PlayerTTL = int.MaxValue,
                    EmptyRoomLiveTime = 60000,
                    GameProperties = new Hashtable
                    {
                        {GamePropertyKey.MaxPlayers, 2}
                    },
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                var response = masterClient1.CreateGame(createRequest);

                this.ConnectAndAuthenticate(masterClient1, response.Address);

                this.UpdateTokensGSAndGame(masterClient2, "localhost", roomName);
                this.ConnectAndAuthenticate(masterClient2, response.Address);

                createRequest.AddUsers = new[] {this.Player2, this.Player3};

                masterClient1.CreateGame(createRequest, ErrorCode.InvalidOperation);

                masterClient2.JoinGame(roomName, ErrorCode.GameDoesNotExist);

            }
            finally
            {
                DisposeClients(masterClient1, masterClient2);
            }
        }

        /// <summary>
        /// We create game. Join it, and than join and set expected users list, which contains already joined users
        /// </summary>
        [Test]
        public void Slots_JoinGameAndSetExpectedForExistingUsersTest()
        {
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires userId to be set");
            }

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;
            UnifiedTestClient masterClient3 = null;
            UnifiedTestClient masterClient4 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_");
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    PlayerTTL = int.MaxValue,
                    GameProperties = new Hashtable
                    {
                        {GamePropertyKey.MaxPlayers, 3}
                    },
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                masterClient3 = this.CreateMasterClientAndAuthenticate(this.Player3);
                masterClient4 = this.CreateMasterClientAndAuthenticate("Player4");

                var joinResponse1 = masterClient1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address);
                masterClient1.JoinGame(joinRequest);

                masterClient1.WaitForEvent(EventCode.Join);

                Thread.Sleep(200);

                var joinRequest2 = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.JoinOnly,
                    CheckUserOnJoin = true,
                };

                joinResponse1 = masterClient2.JoinGame(joinRequest2);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient2, joinResponse1.Address);
                masterClient2.JoinGame(joinRequest2);

                joinRequest2 = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.JoinOnly,
                    CheckUserOnJoin = true,
                    AddUsers = new []{this.Player1, this.Player2, this.Player3}
                };

                joinResponse1 = masterClient3.JoinGame(joinRequest2);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient3, joinResponse1.Address);
                masterClient3.JoinGame(joinRequest2);
            }
            finally
            {
                DisposeClients(masterClient1, masterClient2, masterClient3, masterClient4);
            }
        }

        [Test]
        public void Slots_CreateGameAndJoinNoMaxUserLimitTest()
        {
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires userId to be set");
            }

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_");
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    PlayerTTL = int.MaxValue,
                    AddUsers = new[] { this.Player2, this.Player3 }
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate("Player7");

                var response = masterClient1.JoinGame(joinRequest);

                this.ConnectAndAuthenticate(masterClient1, response.Address);
                masterClient1.JoinGame(joinRequest);

                joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    AddUsers = new[] { "Player4", "Player5" }
                };

                response = masterClient2.JoinGame(joinRequest);

                this.ConnectAndAuthenticate(masterClient2, response.Address);
                masterClient2.JoinGame(joinRequest);

            }
            finally
            {
                DisposeClients(masterClient1, masterClient2);
            }
        }

        // we check that Slots are prefered souces of expected useres
        [Test]
        public void Slots_CreateGameUsingBothTest()
        {
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires userId to be set");
            }

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;
            UnifiedTestClient masterClient3 = null;
            UnifiedTestClient masterClient4 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_");
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    PlayerTTL = int.MaxValue,
                    GameProperties = new Hashtable
                    {
                        {GamePropertyKey.MaxPlayers, 3},
                        {GameParameter.ExpectedUsers, new[] { this.Player2, this.Player3 }}
                    },
                    AddUsers = new[] { this.Player2 }
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                masterClient3 = this.CreateMasterClientAndAuthenticate(this.Player3);
                masterClient4 = this.CreateMasterClientAndAuthenticate("Player4");

                var joinResponse1 = masterClient1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address);
                masterClient1.JoinGame(joinRequest);

                masterClient1.WaitForEvent(EventCode.Join);

                Thread.Sleep(100);
                var lobbyStatsResponse = masterClient4.GetLobbyStats(null, null);
                Assert.AreEqual(2, lobbyStatsResponse.PeerCount[0]);

                var joinRequest2 = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.JoinOnly,
                    CheckUserOnJoin = true,
                };

                joinResponse1 = masterClient2.JoinGame(joinRequest2);

                // client 2: connect to GS and try to join game where it is expected
                this.ConnectAndAuthenticate(masterClient2, joinResponse1.Address);
                masterClient2.JoinGame(joinRequest2);

                var setPropertiesRequest = new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,

                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.ExpectedValues, new Hashtable{{(byte) GameParameter.ExpectedUsers, new[] {this.Player2}}}},
                        {ParameterCode.Properties, new Hashtable{{(byte) GameParameter.ExpectedUsers, new [] {this.Player3}}}}
                    }
                };

                Thread.Sleep(10);
                masterClient2.SendRequestAndWaitForResponse(setPropertiesRequest);

            }
            finally
            {
                DisposeClients(masterClient1, masterClient2, masterClient3, masterClient4);
            }
        }

        [Test]
        public void Slots_CreateGameWithAllSlotsSetTest()
        {
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires userId to be set");
            }

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;
            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_");
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = true,
                    PlayerTTL = int.MaxValue,
                    GameProperties = new Hashtable
                    {
                        {GamePropertyKey.MaxPlayers, 2}
                    },
                    AddUsers = new[] { this.Player1, this.Player2 }
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                var joinResponse1 = masterClient1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address);
                masterClient1.JoinGame(joinRequest);

                masterClient1.WaitForEvent(EventCode.Join);

                var joinResponse2 = masterClient2.JoinGame(joinRequest);
                this.ConnectAndAuthenticate(masterClient2, joinResponse2.Address);
                masterClient2.JoinGame(joinRequest);

            }
            finally
            {
                DisposeClients(masterClient1, masterClient2);
            }
        }

        // we check that Slots are correctly set if we use properties
        [Test]
        public void Slots_CreateGameUsingPropertiesTest()
        {
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires userId to be set");
            }

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient4 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_");
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    PlayerTTL = int.MaxValue,
                    GameProperties = new Hashtable
                    {
                        {GamePropertyKey.MaxPlayers, 3},
                        {GameParameter.ExpectedUsers, new[] { this.Player2, this.Player3 }}
                    },
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient4 = this.CreateMasterClientAndAuthenticate("Player4");

                var joinResponse1 = masterClient1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address);
                masterClient1.JoinGame(joinRequest);

                masterClient1.WaitForEvent(EventCode.Join);

                Thread.Sleep(100);
                var lobbyStatsResponse = masterClient4.GetLobbyStats(null, null);
                Assert.AreEqual(3, lobbyStatsResponse.PeerCount[0]);
            }
            finally
            {
                DisposeClients(masterClient1,masterClient4);
            }
        }

        // we check that Slots are correctly set if we use properties
        [Test]
        public void Slots_SetSlotsUsingPropertiesAfterCreationTest()
        {
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires userId to be set");
            }

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient4 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_");
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    PlayerTTL = int.MaxValue,
                    GameProperties = new Hashtable
                    {
                        {GamePropertyKey.MaxPlayers, 3},
                    },
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient4 = this.CreateMasterClientAndAuthenticate("Player4");

                var joinResponse1 = masterClient1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address);
                masterClient1.JoinGame(joinRequest);

                masterClient1.WaitForEvent(EventCode.Join);

                var setPropertiesRequest = new OperationRequest()
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Properties, new Hashtable { {GameParameter.ExpectedUsers, new[] { this.Player2, this.Player3 }}} },
                        {ParameterCode.ExpectedValues, new Hashtable { {GameParameter.ExpectedUsers, null }} }
                    }
                };

                masterClient1.SendRequest(setPropertiesRequest);

                masterClient1.TryWaitForOperationResponse(1000, out _);

                Thread.Sleep(500);
                var lobbyStatsResponse = masterClient4.GetLobbyStats(null, null);
                Assert.AreEqual(3, lobbyStatsResponse.PeerCount[0]);
            }
            finally
            {
                DisposeClients(masterClient1,masterClient4);
            }
        }

        [Test]
        public void Slots_EmptySlotNameTest()
        {
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires userId to be set");
            }

            UnifiedTestClient masterClient1 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_");
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = true,
                    PlayerTTL = int.MaxValue,
                    GameProperties = new Hashtable
                    {
                        {GamePropertyKey.MaxPlayers, 2}
                    },
                    AddUsers = new[] { string.Empty }
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                masterClient1.JoinGame(joinRequest, ErrorCode.InvalidOperation);
            }
            finally
            {
                DisposeClients(masterClient1);
            }
        }

        [Test]
        public void Slots_LobbyStatsTest()
        {
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires userId to be set");
            }

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;
            UnifiedTestClient masterClient3 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_");
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    PlayerTTL = int.MaxValue,
                    GameProperties = new Hashtable
                    {
                        {GamePropertyKey.MaxPlayers, 3}
                    },
                    AddUsers = new[] { this.Player2, this.Player3 }
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                masterClient3 = this.CreateMasterClientAndAuthenticate(this.Player3);

                var joinResponse1 = masterClient1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address);
                masterClient1.JoinGame(joinRequest);

                masterClient1.WaitForEvent(EventCode.Join);

                Thread.Sleep(100);
                var lobbyStatsResponse = masterClient3.GetLobbyStats(null, null);
                Assert.AreEqual(3, lobbyStatsResponse.PeerCount[0]);

                var joinRequest2 = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.JoinOnly,
                    CheckUserOnJoin = true,
                };

                joinResponse1 = masterClient2.JoinGame(joinRequest2);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient2, joinResponse1.Address);
                masterClient2.JoinGame(joinRequest2);

                Thread.Sleep(100);
                lobbyStatsResponse = masterClient3.GetLobbyStats(null, null);
                Assert.AreEqual(3, lobbyStatsResponse.PeerCount[0]);
            }
            finally
            {
                DisposeClients(masterClient1, masterClient2, masterClient3);
            }
        }

        [Test]
        public void Slots_SlotReservationOnJoinTest()
        {
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires userId to be set");
            }

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;
            UnifiedTestClient masterClient3 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_");
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    PlayerTTL = int.MaxValue,
                    GameProperties = new Hashtable
                    {
                        {GamePropertyKey.MaxPlayers, 3}
                    },
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                masterClient3 = this.CreateMasterClientAndAuthenticate(this.Player3);

                var joinResponse1 = masterClient1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address);
                masterClient1.JoinGame(joinRequest);

                masterClient1.WaitForEvent(EventCode.Join);

                Thread.Sleep(100);
                var lobbyStatsResponse = masterClient3.GetLobbyStats(null, null);
                Assert.AreEqual(1, lobbyStatsResponse.PeerCount[0]);

                var joinRequest2 = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = true,
                    AddUsers = new[] { this.Player3 }
                };

                joinResponse1 = masterClient2.JoinGame(joinRequest2);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient2, joinResponse1.Address);
                masterClient2.JoinGame(joinRequest2);

                masterClient1.WaitForEvent(EventCode.PropertiesChanged);

                Thread.Sleep(100);
                lobbyStatsResponse = masterClient3.GetLobbyStats(null, null);
                Assert.AreEqual(3, lobbyStatsResponse.PeerCount[0]);
            }
            finally
            {
                DisposeClients(masterClient1, masterClient2, masterClient3);
            }
        }

        [Test]
        public void Slots_SlotReservationOnJoinUsingPropertiesWithDuplicationsTest()
        {
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires userId to be set");
            }

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;
            UnifiedTestClient masterClient3 = null;
            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_");
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    PlayerTTL = int.MaxValue,
                    GameProperties = new Hashtable
                    {
                        {GamePropertyKey.MaxPlayers, 3}
                    },
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                masterClient3 = this.CreateMasterClientAndAuthenticate(this.Player3);

                var joinResponse1 = masterClient1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address);
                masterClient1.JoinGame(joinRequest);

                masterClient1.WaitForEvent(EventCode.Join);

                Thread.Sleep(100);
                var lobbyStatsResponse = masterClient3.GetLobbyStats(null, null);
                Assert.AreEqual(1, lobbyStatsResponse.PeerCount[0]);

                var joinRequest2 = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = true,
                };

                joinResponse1 = masterClient2.JoinGame(joinRequest2);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient2, joinResponse1.Address);

                joinRequest2 = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = true,
                    GameProperties = new Hashtable
                    {
                        {GameParameter.ExpectedUsers, new[] { this.Player2, this.Player2, this.Player3, this.Player3 }}
                    },
                };

                masterClient2.JoinGame(joinRequest2);
                masterClient1.WaitForEvent(EventCode.PropertiesChanged);

                Thread.Sleep(300);
                lobbyStatsResponse = masterClient3.GetLobbyStats(null, null);
                Assert.AreEqual(3, lobbyStatsResponse.PeerCount[0]);
            }
            finally
            {
                DisposeClients(masterClient1, masterClient2, masterClient3);
            }
        }

        [Test]
        public void Slots_SlotReservationOnJoinUsingPropertiesWithEmptySlotsTest()
        {
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires userId to be set");
            }

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;
            UnifiedTestClient masterClient3 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_");
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    PlayerTTL = int.MaxValue,
                    GameProperties = new Hashtable
                    {
                        {GamePropertyKey.MaxPlayers, 30}
                    },
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                masterClient3 = this.CreateMasterClientAndAuthenticate(this.Player3);

                var joinResponse1 = masterClient1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address);
                masterClient1.JoinGame(joinRequest);

                masterClient1.WaitForEvent(EventCode.Join);

                Thread.Sleep(100);
                var lobbyStatsResponse = masterClient3.GetLobbyStats(null, null);
                Assert.AreEqual(1, lobbyStatsResponse.PeerCount[0]);

                var joinRequest2 = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = true,
                };

                joinResponse1 = masterClient2.JoinGame(joinRequest2);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient2, joinResponse1.Address);

                joinRequest2 = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = true,
                    GameProperties = new Hashtable
                    {
                        {GameParameter.ExpectedUsers, new[] { "", "", "", "" }}
                    },
                };

                masterClient2.JoinGame(joinRequest2, ErrorCode.InvalidOperation);

                DisposeClients(masterClient2);

                masterClient2 = this.CreateMasterClientAndAuthenticate("Player4");
                joinRequest2 = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = true,
                };

                joinResponse1 = masterClient2.JoinGame(joinRequest2);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient2, joinResponse1.Address);

                joinRequest2 = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = true,
                    GameProperties = new Hashtable
                    {
                        {GameParameter.ExpectedUsers, new[] { this.Player2, this.Player2, this.Player3, this.Player3 }}
                    },
                };

                masterClient2.JoinGame(joinRequest2);
                masterClient1.WaitForEvent(EventCode.PropertiesChanged);

                Thread.Sleep(300);
                lobbyStatsResponse = masterClient3.GetLobbyStats(null, null);
                // here 4 because Player2 is not removed yet from master. it will be removed later by timeout
                Assert.AreEqual(4, lobbyStatsResponse.PeerCount[0]);
            }
            finally
            {
                DisposeClients(masterClient1, masterClient2, masterClient3);
            }
        }


        [Test]
        public void Slots_TooManySlotsOnJoinTest()
        {
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires userId to be set");
            }

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;
            UnifiedTestClient masterClient3 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_");
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    PlayerTTL = int.MaxValue,
                    GameProperties = new Hashtable
                    {
                        {GamePropertyKey.MaxPlayers, 3}
                    },
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                masterClient3 = this.CreateMasterClientAndAuthenticate(this.Player3);

                var joinResponse1 = masterClient1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address);
                masterClient1.JoinGame(joinRequest);

                masterClient1.WaitForEvent(EventCode.Join);

                Thread.Sleep(100);
                var lobbyStatsResponse = masterClient3.GetLobbyStats(null, null);
                Assert.AreEqual(1, lobbyStatsResponse.PeerCount[0]);

                var joinRequest2 = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = true,
                    AddUsers = new[] { this.Player3, "Player4", "Player5" }
                };

                masterClient2.JoinGame(joinRequest2, 32742);


                Thread.Sleep(100);
                lobbyStatsResponse = masterClient3.GetLobbyStats(null, null);
                Assert.AreEqual(1, lobbyStatsResponse.PeerCount[0]);
            }
            finally
            {
                DisposeClients(masterClient1, masterClient2, masterClient3);
            }
        }

        [Test]
        public void Slots_NoPlaceToReserveSlots()
        {
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires userId to be set");
            }

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;
            UnifiedTestClient masterClient3 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_");
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    PlayerTTL = int.MaxValue,
                    GameProperties = new Hashtable
                    {
                        {GamePropertyKey.MaxPlayers, 2}
                    },
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                masterClient3 = this.CreateMasterClientAndAuthenticate(this.Player3);

                var joinResponse1 = masterClient1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address);
                masterClient1.JoinGame(joinRequest);

                masterClient1.WaitForEvent(EventCode.Join);

                Thread.Sleep(300);
                var lobbyStatsResponse = masterClient3.GetLobbyStats(null, null);
                Assert.AreEqual(1, lobbyStatsResponse.PeerCount[0]);

                var joinRequest2 = new JoinGameRequest
                {
                    GameId = roomName,
                    CheckUserOnJoin = true,
                    AddUsers = new[] { this.Player3, "Player4" }
                };

                masterClient2.JoinGame(joinRequest2, 32742);

                Thread.Sleep(100);
                lobbyStatsResponse = masterClient3.GetLobbyStats(null, null);
                Assert.AreEqual(1, lobbyStatsResponse.PeerCount[0]);
            }
            finally
            {
                DisposeClients(masterClient1, masterClient2, masterClient3);
            }
        }

        /// <summary>
        /// in this test we reserve slot for our self too. I mean player who joins game reserves slot for him self too
        /// </summary>
        [Test]
        public void Slots_NoPlaceToReserveSlots2()
        {
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires userId to be set");
            }

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;
            UnifiedTestClient masterClient3 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_");
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    PlayerTTL = int.MaxValue,
                    GameProperties = new Hashtable
                    {
                        {GamePropertyKey.MaxPlayers, 2}
                    },
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                masterClient3 = this.CreateMasterClientAndAuthenticate(this.Player3);

                var joinResponse1 = masterClient1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address);
                masterClient1.JoinGame(joinRequest);

                masterClient1.WaitForEvent(EventCode.Join);

                Thread.Sleep(300);
                var lobbyStatsResponse = masterClient3.GetLobbyStats(null, null);
                Assert.AreEqual(1, lobbyStatsResponse.PeerCount[0]);

                masterClient2.JoinGame(roomName);
                this.ConnectAndAuthenticate(masterClient2, joinResponse1.Address);

                var joinRequest2 = new JoinGameRequest
                {
                    GameId = roomName,
                    CheckUserOnJoin = true,
                    AddUsers = new[] { this.Player2, "Player4" }
                };

                masterClient2.JoinGame(joinRequest2, ErrorCode.SlotError);

                Thread.Sleep(100);
                lobbyStatsResponse = masterClient3.GetLobbyStats(null, null);
                Assert.AreEqual(1, lobbyStatsResponse.PeerCount[0]);

                DisposeClients(masterClient2);

                masterClient2 = this.CreateMasterClientAndAuthenticate("Player8");
                masterClient2.JoinGame(joinRequest);
                this.ConnectAndAuthenticate(masterClient2, joinResponse1.Address);
                masterClient2.JoinGame(new JoinGameRequest
                                        {
                                            GameId = roomName,
                                            CheckUserOnJoin = true,
                                        });


            }
            finally
            {
                DisposeClients(masterClient1, masterClient2, masterClient3);
            }
        }

        /// <summary>
        /// in this test we reserve slot for our self too. I mean player who joins game reserves slot for him self too
        /// </summary>
        [Test]
        public void Slots_NoPlaceToReserveSlots3()
        {
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires userId to be set");
            }

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;
            UnifiedTestClient masterClient3 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_");
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    PlayerTTL = int.MaxValue,
                    GameProperties = new Hashtable
                    {
                        {GamePropertyKey.MaxPlayers, 2}
                    },
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                masterClient3 = this.CreateMasterClientAndAuthenticate(this.Player3);

                var joinResponse1 = masterClient1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address);
                masterClient1.JoinGame(joinRequest);

                masterClient1.WaitForEvent(EventCode.Join);

                Thread.Sleep(300);
                var lobbyStatsResponse = masterClient3.GetLobbyStats(null, null);
                Assert.AreEqual(1, lobbyStatsResponse.PeerCount[0]);


                var joinRequest2 = new JoinRandomGameRequest
                {
                    AddUsers = new[] { this.Player2, "Player4" }
                };

                masterClient2.JoinRandomGame(joinRequest2, ErrorCode.NoRandomMatchFound);

                Thread.Sleep(100);
                lobbyStatsResponse = masterClient3.GetLobbyStats(null, null);
                Assert.AreEqual(1, lobbyStatsResponse.PeerCount[0]);

                DisposeClients(masterClient2);

                masterClient2 = this.CreateMasterClientAndAuthenticate("Player8");
                masterClient2.JoinGame(roomName);
                this.ConnectAndAuthenticate(masterClient2, joinResponse1.Address);
                masterClient2.JoinGame(new JoinGameRequest
                                        {
                                            GameId = roomName,
                                            CheckUserOnJoin = true,
                                        });


            }
            finally
            {
                DisposeClients(masterClient1, masterClient2, masterClient3);
            }
        }

        /// <summary>
        /// in this test we reserve slot for our self too. I mean player who joins game reserves slot for him self too
        /// </summary>
        [Test]
        public void Slots_NoPlaceToReserveSlots4()
        {
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires userId to be set");
            }

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;
            UnifiedTestClient masterClient3 = null;
            UnifiedTestClient masterClient4 = null;
            UnifiedTestClient masterClient5 = null;
            UnifiedTestClient masterClient6 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_");
                var lobbyName = "default";
                var lobbyType = (byte) LobbyType.SqlLobby;

                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    PlayerTTL = int.MaxValue,
                    GameProperties = new Hashtable
                    {
                        {GamePropertyKey.MaxPlayers, 6}
                    },
                    //AddUsers =  new [] {this.Player1, this.Player2, this.Player3},
                    LobbyName = lobbyName,
                    LobbyType = lobbyType,
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                masterClient3 = this.CreateMasterClientAndAuthenticate(this.Player3);

                var joinResponse1 = masterClient1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address);
                masterClient1.JoinGame(joinRequest);

                masterClient1.WaitForEvent(EventCode.Join);

                masterClient2.JoinGame(joinRequest);
                masterClient3.JoinGame(joinRequest);

                this.ConnectAndAuthenticate(masterClient2, joinResponse1.Address);
                this.ConnectAndAuthenticate(masterClient3, joinResponse1.Address);

                masterClient2.JoinGame(joinRequest);
                masterClient3.JoinGame(joinRequest);

                masterClient2.WaitForEvent(EventCode.Join);
                masterClient3.WaitForEvent(EventCode.Join);

                var joinRequest2 = new JoinRandomGameRequest
                {
                    LobbyName = lobbyName,
                    LobbyType = lobbyType,
                    AddUsers = new[] { "Player4", "Player5" }
                };

                masterClient4 = this.CreateMasterClientAndAuthenticate("Player4");
                masterClient5 = this.CreateMasterClientAndAuthenticate("Player5");
                masterClient6 = this.CreateMasterClientAndAuthenticate("Player6");

                var joinRandomResponse1 = masterClient4.JoinRandomGame(joinRequest2, ErrorCode.Ok);

                var joinRequest3 = new JoinRandomGameRequest
                {
                    LobbyName = lobbyName,
                    LobbyType = lobbyType,
                    AddUsers = new[] { "Player6", "Player7" }
                };

                var joinRandomResponse2 = masterClient6.JoinRandomGame(joinRequest2, ErrorCode.Ok);

                this.ConnectAndAuthenticate(masterClient4, joinRandomResponse1.Address);

                masterClient4.JoinGame(new JoinGameRequest
                {
                    GameId = joinRandomResponse1.GameId,
                    AddUsers = joinRequest2.AddUsers,
                });

                this.ConnectAndAuthenticate(masterClient6, joinRandomResponse2.Address);

                masterClient6.JoinGame(new JoinGameRequest
                {
                    GameId = joinRandomResponse1.GameId,
                    AddUsers = joinRequest3.AddUsers,
                }, ErrorCode.SlotError);

                masterClient5.JoinGame(new JoinGameRequest
                {
                    GameId = joinRandomResponse1.GameId,
                    AddUsers = joinRequest2.AddUsers,
                });
                this.ConnectAndAuthenticate(masterClient5, joinRandomResponse1.Address);
                masterClient5.JoinGame(new JoinGameRequest
                {
                    GameId = joinRandomResponse1.GameId,
                    AddUsers = joinRequest2.AddUsers,
                });

                DisposeClients(masterClient6);
                masterClient6 = this.CreateMasterClientAndAuthenticate("Player8");
                masterClient6.JoinGame(roomName);
                this.ConnectAndAuthenticate(masterClient6, joinResponse1.Address);
                masterClient6.JoinGame(roomName);
            }
            finally
            {
                DisposeClients(masterClient1, masterClient2, masterClient3, masterClient4, masterClient5, masterClient6);
            }
        }

        /// <summary>
        /// in this test we reserve slot for our self too. I mean player who joins game reserves slot for him self too
        /// </summary>
        [Test]
        public void Slots_NoPlaceToReserveSlotsNitendoStory([Values(LobbyType.Default, LobbyType.SqlLobby)]LobbyType lobby)
        {
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires userId to be set");
            }

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;
            UnifiedTestClient masterClient3 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_");
                var lobbyName = "default";
                var lobbyType = (byte) lobby;

                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    PlayerTTL = int.MaxValue,
                    GameProperties = new Hashtable
                    {
                        {GamePropertyKey.MaxPlayers, 8}
                    },
                    //AddUsers =  new [] {this.Player1, this.Player2, this.Player3},
                    LobbyName = lobbyName,
                    LobbyType = lobbyType,
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                masterClient3 = this.CreateMasterClientAndAuthenticate(this.Player3);

                var joinResponse1 = masterClient1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address);
                masterClient1.JoinGame(joinRequest);

                masterClient1.WaitForEvent(EventCode.Join);

                var joinRandomRequest = new JoinRandomGameRequest
                {
                    LobbyName = lobbyName,
                    LobbyType = lobbyType,
                    AddUsers = new[] { "Player6", "Player7", "Player8" }
                };

                var joinRandomResponse = masterClient2.JoinRandomGame(joinRandomRequest, ErrorCode.Ok);

                this.ConnectAndAuthenticate(masterClient2, joinRandomResponse.Address);

                var joinGameRequest2 = new JoinGameRequest
                {
                    GameId = joinRandomResponse.GameId,
                    AddUsers = new[] { "Player6", "Player7", "Player8" }
                };

                masterClient2.JoinGame(joinGameRequest2);

                masterClient2.WaitEvent(EventCode.Join);

                Thread.Sleep(100);// make sure that data are updated on master

                var joinRandomRequest2 = new JoinRandomGameRequest
                {
                    LobbyName = lobbyName,
                    LobbyType = lobbyType,
                    AddUsers = new[] { "Player9", "Player10", "Player11" }
                };

                masterClient3.JoinRandomGame(joinRandomRequest2, ErrorCode.NoRandomMatchFound);

            }
            finally
            {
                DisposeClients(masterClient1, masterClient2, masterClient3);
            }
        }

        [Test]
        public void Slots_DifferentJoinModeTest()
        {
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires userId to be set");
            }

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;
            UnifiedTestClient masterClient3 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_");
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    PlayerTTL = int.MaxValue,
                    GameProperties = new Hashtable
                    {
                        {GamePropertyKey.MaxPlayers, 3}
                    },
                    AddUsers = new[] { this.Player2, this.Player3 }
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                masterClient3 = this.CreateMasterClientAndAuthenticate(this.Player3);

                var joinResponse1 = masterClient1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address);
                masterClient1.JoinGame(joinRequest);

                masterClient1.WaitForEvent(EventCode.Join);

                var joinRequest2 = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = true,
                };

                Thread.Sleep(100);
                masterClient2.JoinGame(joinRequest2);

                joinRequest2.JoinMode = JoinModeConstants.RejoinOrJoin;

                joinResponse1 = masterClient2.JoinGame(joinRequest2);

                this.ConnectAndAuthenticate(masterClient2, joinResponse1.Address);
                masterClient2.JoinGame(joinRequest2, ErrorCode.OperationNotAllowedInCurrentState);

                joinRequest2.JoinMode = JoinModeConstants.RejoinOnly;

                joinResponse1 = masterClient3.JoinGame(joinRequest2);

                this.ConnectAndAuthenticate(masterClient3, joinResponse1.Address);
                masterClient3.JoinGame(joinRequest2, (short)Photon.Common.ErrorCode.JoinFailedWithRejoinerNotFound);

            }
            finally
            {
                DisposeClients(masterClient1, masterClient2, masterClient3);
            }
        }

        [Test]
        public void Slots_RepeatingNamesInDifferentRequestsTest()
        {
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires userId to be set");
            }

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;
            UnifiedTestClient masterClient3 = null;
            UnifiedTestClient masterClient4 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_");
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    PlayerTTL = int.MaxValue,
                    GameProperties = new Hashtable
                    {
                        {GamePropertyKey.MaxPlayers, 6}
                    },
                    AddUsers = new[] { this.Player2, this.Player3 }
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                masterClient3 = this.CreateMasterClientAndAuthenticate(this.Player3);
                masterClient4 = this.CreateMasterClientAndAuthenticate(this.Player3 != null ? "Player4" : null);

                var joinResponse1 = masterClient1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address);
                masterClient1.JoinGame(joinRequest);

                masterClient1.WaitForEvent(EventCode.Join);

                Thread.Sleep(100);
                var lobbyStatsResponse = masterClient4.GetLobbyStats(null, null);
                Assert.AreEqual(3, lobbyStatsResponse.PeerCount[0]);

                var joinRequest2 = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.JoinOnly,
                    CheckUserOnJoin = true,
                    AddUsers = new []{this.Player3, "Player4"}
                };

                joinResponse1 = masterClient2.JoinGame(joinRequest2);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient2, joinResponse1.Address);
                masterClient2.JoinGame(joinRequest2);

                Thread.Sleep(100);
                lobbyStatsResponse = masterClient4.GetLobbyStats(null, null);
                Assert.AreEqual(4, lobbyStatsResponse.PeerCount[0]);
            }
            finally
            {
                DisposeClients(masterClient1, masterClient2, masterClient3, masterClient4);
            }
        }

        [TestCase(0)]
        [TestCase(4)]
        public void Slots_SetPropertiesTest(int maxPlayers)
        {
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires userId to be set");
            }

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;
            UnifiedTestClient masterClient3 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_");
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    PlayerTTL = int.MaxValue,
                    GameProperties = new Hashtable
                    {
                        {GamePropertyKey.MaxPlayers, maxPlayers}
                    },

                    AddUsers = new []{ this.Player2, this.Player3, "Player4"}
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                masterClient3 = this.CreateMasterClientAndAuthenticate(this.Player3);

                var joinResponse1 = masterClient1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address);
                masterClient1.JoinGame(joinRequest);

                masterClient1.WaitForEvent(EventCode.Join);

                Thread.Sleep(100);
                var lobbyStatsResponse = masterClient3.GetLobbyStats(null, null);
                Assert.AreEqual(4, lobbyStatsResponse.PeerCount[0]);

                joinRequest.JoinMode = JoinModeConstants.JoinOnly;
                joinRequest.AddUsers = null;
                masterClient2.JoinGame(joinRequest);

                this.ConnectAndAuthenticate(masterClient2, joinResponse1.Address);
                masterClient2.JoinGame(joinRequest);

                var setPropertiesRequest = new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,

                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.ExpectedValues, new Hashtable{{(byte) GameParameter.ExpectedUsers, new[] {this.Player2, this.Player3, "Player4"}}}},
                        {ParameterCode.Properties, new Hashtable{{(byte) GameParameter.ExpectedUsers, new [] {this.Player2}}}}
                    }
                };

                Thread.Sleep(10);
                masterClient2.SendRequestAndWaitForResponse(setPropertiesRequest);

                masterClient1.WaitForEvent(EventCode.PropertiesChanged);

                Thread.Sleep(300);
                lobbyStatsResponse = masterClient3.GetLobbyStats(null, null);
                Assert.AreEqual(2, lobbyStatsResponse.PeerCount[0]);
            }
            finally
            {
                DisposeClients(masterClient1, masterClient2, masterClient3);
            }
        }

        [Test]
        public void Slots_SetPropertiesEmptyRepeatingSlotsTest()
        {
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires userId to be set");
            }

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;
            UnifiedTestClient masterClient3 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_");
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    PlayerTTL = int.MaxValue,
                    AddUsers = new string[] {}
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                masterClient3 = this.CreateMasterClientAndAuthenticate(this.Player3);

                var joinResponse1 = masterClient1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address);
                masterClient1.JoinGame(joinRequest);

                masterClient1.WaitForEvent(EventCode.Join);

                Thread.Sleep(100);
                var lobbyStatsResponse = masterClient3.GetLobbyStats(null, null);
                Assert.AreEqual(1, lobbyStatsResponse.PeerCount[0]);

                joinRequest.JoinMode = JoinModeConstants.JoinOnly;
                joinRequest.AddUsers = null;
                masterClient2.JoinGame(joinRequest);

                this.ConnectAndAuthenticate(masterClient2, joinResponse1.Address);
                masterClient2.JoinGame(joinRequest);

                var setPropertiesRequest = new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,

                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.ExpectedValues, new Hashtable{{(byte) GameParameter.ExpectedUsers, null }}},
                        {ParameterCode.Properties, new Hashtable{{(byte) GameParameter.ExpectedUsers, new [] {"", ""}}}}
                    }
                };

                masterClient2.SendRequestAndWaitForResponse(setPropertiesRequest, ErrorCode.InvalidOperation);

                setPropertiesRequest = new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,

                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.ExpectedValues, new Hashtable{{(byte) GameParameter.ExpectedUsers, null }}},
                        {ParameterCode.Properties, new Hashtable{{(byte) GameParameter.ExpectedUsers, new [] {this.Player3}}}}
                    }
                };

                masterClient2.SendRequestAndWaitForResponse(setPropertiesRequest);


                Thread.Sleep(10);
                masterClient1.WaitForEvent(EventCode.PropertiesChanged);

                Thread.Sleep(300);
                lobbyStatsResponse = masterClient3.GetLobbyStats(null, null);
                Assert.AreEqual(3, lobbyStatsResponse.PeerCount[0]);
            }
            finally
            {
                DisposeClients(masterClient1, masterClient2, masterClient3);
            }
        }

        /// <summary>
        /// in this test we set "test1" as expected during game creation
        /// </summary>
        [TestCase(null)]
        [TestCase("test1")]
        [TestCase("test2")]
        public void Slots_SetPropertiesRepeatingSlotsTest(string expectedUser)
        {
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires userId to be set");
            }

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;
            UnifiedTestClient masterClient3 = null;
            UnifiedTestClient masterClient4 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_");
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    PlayerTTL = int.MaxValue,
                    AddUsers = expectedUser == null ? null : new [] {expectedUser}
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                masterClient3 = this.CreateMasterClientAndAuthenticate(this.Player3);

                var joinResponse1 = masterClient1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address);
                masterClient1.JoinGame(joinRequest);

                masterClient1.WaitForEvent(EventCode.Join);

                Thread.Sleep(100);
                var lobbyStatsResponse = masterClient3.GetLobbyStats(null, null);
                Assert.AreEqual(expectedUser == null ? 1 : 2, lobbyStatsResponse.PeerCount[0]);

                joinRequest.JoinMode = JoinModeConstants.JoinOnly;
                joinRequest.AddUsers = null;
                masterClient2.JoinGame(joinRequest);

                this.ConnectAndAuthenticate(masterClient2, joinResponse1.Address);
                masterClient2.JoinGame(joinRequest);

                var setPropertiesRequest = new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,

                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.ExpectedValues, new Hashtable{{(byte) GameParameter.ExpectedUsers, expectedUser == null ? null : new []{expectedUser} }}},
                        {ParameterCode.Properties, new Hashtable{{(byte) GameParameter.ExpectedUsers, new [] {"test1", "test1", "test1" } }}}
                    }
                };

                masterClient2.SendRequestAndWaitForResponse(setPropertiesRequest);

                Thread.Sleep(10);
                var propertiesUpdateEvent = masterClient1.WaitForEvent(EventCode.PropertiesChanged);

                var propertiesHash = (Hashtable)propertiesUpdateEvent[251];
                Assert.That(propertiesHash, Is.Not.Null);

                var slots = (string[]) propertiesHash[GameParameters.ExpectedUsers];

                Assert.That(slots.Length, Is.EqualTo(1));
                Assert.That(slots[0], Is.EqualTo("test1"));

                masterClient4 = this.CreateMasterClientAndAuthenticate("Player4");
                masterClient4.JoinGame(roomName);
                this.ConnectAndAuthenticate(masterClient4, joinResponse1.Address);
                var response = masterClient4.JoinGame(joinRequest);
                slots = (string[])response.GameProperties[GameParameters.ExpectedUsers];
                Assert.That(slots.Length, Is.EqualTo(1));
                Assert.That(slots[0], Is.EqualTo("test1"));

                Thread.Sleep(300);
                lobbyStatsResponse = masterClient3.GetLobbyStats(null, null);
                Assert.That(lobbyStatsResponse.PeerCount[0], Is.EqualTo(4));
            }
            finally
            {
                DisposeClients(masterClient1, masterClient2, masterClient3, masterClient4);
            }
        }

        [TestCase("SetLobbyProperties")]
        [TestCase("NoLobbyProperties")]
        public void Slots_ClearSlotsUsingPropertiesTest(string testCase)
        {
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires userId to be set");
            }

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;

            var setLobbyProperties = testCase == "SetLobbyProperties";

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_");
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    PlayerTTL = int.MaxValue,
                    
                    GameProperties = new Hashtable
                    {
                        {GamePropertyKey.MaxPlayers, 4},
                    },

                    AddUsers = new[] { this.Player2, this.Player3, "Player4" }
                };

                if (setLobbyProperties)
                {
                    joinRequest.GameProperties.Add(GameParameter.LobbyProperties, new[] {"test"});
                }

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                var joinResponse1 = masterClient1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address);
                masterClient1.JoinGame(joinRequest);

                masterClient1.WaitForEvent(EventCode.Join);

                Thread.Sleep(100);
                var lobbyStatsResponse = masterClient2.GetLobbyStats(null, null);
                Assert.AreEqual(4, lobbyStatsResponse.PeerCount[0]);

                var setPropertiesRequest = new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,

                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.ExpectedValues, new Hashtable{{(byte) GameParameter.ExpectedUsers, new[] {this.Player2, this.Player3, "Player4"}}}},
                        {ParameterCode.Properties, new Hashtable{{(byte) GameParameter.ExpectedUsers, null }}}
                    }
                };

                Thread.Sleep(10);
                masterClient1.SendRequestAndWaitForResponse(setPropertiesRequest);

                masterClient1.WaitForEvent(EventCode.PropertiesChanged);

                Thread.Sleep(300);
                lobbyStatsResponse = masterClient2.GetLobbyStats(null, null);
                Assert.AreEqual(1, lobbyStatsResponse.PeerCount[0]);

                setPropertiesRequest = new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,

                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.ExpectedValues, new Hashtable{{(byte) GameParameter.ExpectedUsers, null}}},
                        {ParameterCode.Properties, new Hashtable{{(byte) GameParameter.ExpectedUsers, new[] {this.Player2} }}}
                    }
                };

                masterClient1.SendRequestAndWaitForResponse(setPropertiesRequest);

                masterClient1.WaitForEvent(EventCode.PropertiesChanged);

                Thread.Sleep(300);
                lobbyStatsResponse = masterClient2.GetLobbyStats(null, null);
                Assert.AreEqual(2, lobbyStatsResponse.PeerCount[0]);

            }
            finally
            {
                DisposeClients(masterClient1,  masterClient2);
            }
        }

        [Test]
        public void Slots_SetPropertiesFailedCASTest()
        {
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires userId to be set");
            }

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;
            UnifiedTestClient masterClient3 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_");
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    PlayerTTL = int.MaxValue,
                    GameProperties = new Hashtable
                    {
                        {GamePropertyKey.MaxPlayers, 3}
                    },

                    AddUsers = new[] { this.Player2, this.Player3 }
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                masterClient3 = this.CreateMasterClientAndAuthenticate(this.Player3);

                var joinResponse1 = masterClient1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address);
                masterClient1.JoinGame(joinRequest);

                masterClient1.WaitForEvent(EventCode.Join);

                Thread.Sleep(100);
                var lobbyStatsResponse = masterClient3.GetLobbyStats(null, null);
                Assert.AreEqual(3, lobbyStatsResponse.PeerCount[0]);

                joinRequest.JoinMode = JoinModeConstants.JoinOnly;
                joinRequest.AddUsers = null;
                masterClient2.JoinGame(joinRequest);

                this.ConnectAndAuthenticate(masterClient2, joinResponse1.Address);
                masterClient2.JoinGame(joinRequest);

                var setPropertiesRequest = new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,

                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.ExpectedValues, new Hashtable{{(byte) GameParameter.ExpectedUsers, new[] {this.Player2}}}},
                        {ParameterCode.Properties, new Hashtable{{(byte) GameParameter.ExpectedUsers, new [] {this.Player2}}}}
                    }
                };

                Thread.Sleep(10);
                masterClient2.SendRequestAndWaitForResponse(setPropertiesRequest, ErrorCode.InvalidOperation);

                Assert.IsFalse(masterClient1.TryWaitForEvent(EventCode.PropertiesChanged, 1000, out _));

                Thread.Sleep(100);
                lobbyStatsResponse = masterClient3.GetLobbyStats(null, null);
                Assert.AreEqual(3, lobbyStatsResponse.PeerCount[0]);
            }
            finally
            {
                DisposeClients(masterClient1, masterClient2, masterClient3);
            }
        }

        [Test]
        public void Slots_SetPropertiesCheckFailTest()
        {
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires userId to be set");
            }

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;
            UnifiedTestClient masterClient3 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_");
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    PlayerTTL = int.MaxValue,
                    GameProperties = new Hashtable
                    {
                        {GamePropertyKey.MaxPlayers, 3}
                    },

                    AddUsers = new[] { this.Player2, this.Player3 }
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                masterClient3 = this.CreateMasterClientAndAuthenticate(this.Player3);

                var joinResponse1 = masterClient1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address);
                masterClient1.JoinGame(joinRequest);

                masterClient1.WaitForEvent(EventCode.Join);

                Thread.Sleep(100);
                var lobbyStatsResponse = masterClient3.GetLobbyStats(null, null);
                Assert.AreEqual(3, lobbyStatsResponse.PeerCount[0]);

                joinRequest.JoinMode = JoinModeConstants.JoinOnly;
                joinRequest.AddUsers = null;
                masterClient2.JoinGame(joinRequest);

                this.ConnectAndAuthenticate(masterClient2, joinResponse1.Address);
                masterClient2.JoinGame(joinRequest);

                var setPropertiesRequest = new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,

                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.ExpectedValues, new Hashtable{{(byte) GameParameter.ExpectedUsers, new[] {this.Player2, this.Player3}}}},
                        {ParameterCode.Properties, new Hashtable{{(byte) GameParameter.ExpectedUsers, new [] {this.Player2, this.Player3, "Player4"}}}}
                    }
                };

                Thread.Sleep(10);
                masterClient2.SendRequestAndWaitForResponse(setPropertiesRequest, ErrorCode.InvalidOperation);

                Assert.IsFalse(masterClient1.TryWaitForEvent(EventCode.PropertiesChanged, 1000, out _));

                var response = masterClient2.SendRequestAndWaitForResponse(new OperationRequest
                {
                    OperationCode = OperationCode.GetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Properties, PropertyTypeFlag.Game},
                    }
                });

                var properties = (Hashtable)response[ParameterCode.GameProperties];

                var expectedUsers = (string[]) properties[(byte) GameParameter.ExpectedUsers];

                Assert.That(expectedUsers.Length, Is.EqualTo(2));

                Thread.Sleep(100);
                lobbyStatsResponse = masterClient3.GetLobbyStats(null, null);
                Assert.AreEqual(3, lobbyStatsResponse.PeerCount[0]);
            }
            finally
            {
                DisposeClients(masterClient1, masterClient2, masterClient3);
            }
        }

        [TestCase(LobbyType.Default)]
        [TestCase(LobbyType.SqlLobby)]
        public void Slots_JoinRandomGameWithSlotsTest(LobbyType lobbyType)
        {
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires userId to be set");
            }

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;
            UnifiedTestClient masterClient3 = null;
            UnifiedTestClient masterClient4 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_");
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    PlayerTTL = int.MaxValue,
                    GameProperties = new Hashtable
                    {
                        {GamePropertyKey.MaxPlayers, 6}
                    },
                    AddUsers = new[] { this.Player2, this.Player3 },
                    LobbyType = (byte)lobbyType,
                    LobbyName = SlotsLobbyName,
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                masterClient3 = this.CreateMasterClientAndAuthenticate(this.Player3);
                masterClient4 = this.CreateMasterClientAndAuthenticate("Player4");

                var joinResponse1 = masterClient1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address);
                masterClient1.JoinGame(joinRequest);

                masterClient1.WaitForEvent(EventCode.Join);

                Thread.Sleep(100);
                var lobbyStatsResponse = masterClient4.GetLobbyStats(new[]{SlotsLobbyName}, new[] {(byte)lobbyType});
                Assert.AreEqual(3, lobbyStatsResponse.PeerCount[0]);

                var joinRequest2 = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.JoinOnly,
                    CheckUserOnJoin = true,
                };

                joinResponse1 = masterClient2.JoinGame(joinRequest2);

                this.ConnectAndAuthenticate(masterClient2, joinResponse1.Address);
                masterClient2.JoinGame(joinRequest2);

                joinResponse1 = masterClient3.JoinGame(joinRequest2);

                this.ConnectAndAuthenticate(masterClient3, joinResponse1.Address);
                masterClient3.JoinGame(joinRequest2);

                masterClient4.JoinRandomGame(new JoinRandomGameRequest
                {
                    AddUsers = new[] { "Player4", "Player6", "Player7" },
                    LobbyType = (byte)lobbyType,
                    LobbyName = SlotsLobbyName,
                }, ErrorCode.Ok);


                masterClient4.JoinRandomGame(new JoinRandomGameRequest// we have MaxPlayer = 6. only 3 slots free. Player4 + expected == 7 players
                {
                    AddUsers = new[] { "Player5", "Player6", "Player7" },
                    LobbyType = (byte)lobbyType,
                    LobbyName = SlotsLobbyName,
                }, ErrorCode.NoRandomMatchFound);

                masterClient4.JoinRandomGame(new JoinRandomGameRequest
                {
                    AddUsers = new[] { "Player5", "Player6", "Player7", "Player8" },
                    LobbyType = (byte)lobbyType,
                    LobbyName = SlotsLobbyName,
                }, ErrorCode.NoRandomMatchFound);
            }
            finally
            {
                DisposeClients(masterClient1, masterClient2, masterClient3, masterClient4);
            }
        }

        [TestCase(LobbyType.Default)]
        [TestCase(LobbyType.SqlLobby)]
        public void Slots_JoinRandomGameWithSlotsMaxPlayers2ThirdExpectedTest(LobbyType lobbyType)
        {
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires userId to be set");
            }

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_");
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    PlayerTTL = int.MaxValue,
                    GameProperties = new Hashtable
                    {
                        {GamePropertyKey.MaxPlayers, 2}
                    },
                    LobbyType = (byte)lobbyType,
                    LobbyName = SlotsLobbyName,
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                var joinResponse1 = masterClient1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address);
                masterClient1.JoinGame(joinRequest);

                masterClient1.WaitForEvent(EventCode.Join);

                masterClient2.JoinRandomGame(new JoinRandomGameRequest
                {
                    AddUsers = new[] { this.Player3 },
                    LobbyType = (byte)lobbyType,
                    LobbyName = SlotsLobbyName,
                }, ErrorCode.NoRandomMatchFound);

            }
            finally
            {
                DisposeClients(masterClient1, masterClient2);
            }
        }

        [TestCase(LobbyType.Default)]
        [TestCase(LobbyType.SqlLobby)]
        public void Slots_JoinRandomGameWithSlotsMaxPlayers2FirstExpectedTest(LobbyType lobbyType)
        {
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires userId to be set");
            }

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_");
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    PlayerTTL = int.MaxValue,
                    GameProperties = new Hashtable
                    {
                        {GamePropertyKey.MaxPlayers, 2}
                    },
                    LobbyType = (byte)lobbyType,
                    LobbyName = SlotsLobbyName,
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                var joinResponse1 = masterClient1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address);
                masterClient1.JoinGame(joinRequest);

                masterClient1.WaitForEvent(EventCode.Join);

                Thread.Sleep(300);

                masterClient2.JoinRandomGame(new JoinRandomGameRequest
                {
                    AddUsers = new[] { this.Player2 },
                    LobbyType = (byte)lobbyType,
                    LobbyName = SlotsLobbyName,
                }, ErrorCode.Ok);

            }
            finally
            {
                DisposeClients(masterClient1, masterClient2);
            }
        }

        [TestCase(LobbyType.Default)]
        [TestCase(LobbyType.SqlLobby)]
        public void Slots_JoinRandomGameWithTooManySlotsTest(LobbyType lobbyType)
        {
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires userId to be set");
            }

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_");
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    PlayerTTL = int.MaxValue,
                    GameProperties = new Hashtable
                    {
                        {GamePropertyKey.MaxPlayers, 2}
                    },
                    LobbyType = (byte)lobbyType,
                    LobbyName = SlotsLobbyName,
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                var joinResponse1 = masterClient1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address);
                masterClient1.JoinGame(joinRequest);

                masterClient1.WaitForEvent(EventCode.Join);

                masterClient2.JoinRandomGame(new JoinRandomGameRequest
                {
                    AddUsers = new[] { "Player1", "Player2", "Player3" },
                    LobbyType = (byte)lobbyType,
                    LobbyName = SlotsLobbyName,
                }, ErrorCode.NoRandomMatchFound);  // not sure about error ErrorCode.NoRandomMatchFound or ErrorCode.InvalidOperation

                masterClient2.JoinRandomGame(new JoinRandomGameRequest
                {
                    AddUsers = new[] { "Player3" },
                    LobbyType = (byte)lobbyType,
                    LobbyName = SlotsLobbyName,
                }, ErrorCode.NoRandomMatchFound);
            }
            finally
            {
                DisposeClients(masterClient1, masterClient2);
            }
        }

        [TestCase(LobbyType.Default)]
        [TestCase(LobbyType.SqlLobby)]
        public void Slots_JoinRandomGameWithSlotForJoinedOrJoinerTest(LobbyType lobbyType)
        {
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires userId to be set");
            }

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_");
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    PlayerTTL = int.MaxValue,
                    GameProperties = new Hashtable
                    {
                        {GamePropertyKey.MaxPlayers, 2}
                    },
                    LobbyType = (byte)lobbyType,
                    LobbyName = SlotsLobbyName,
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                var joinResponse1 = masterClient1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address);
                masterClient1.JoinGame(joinRequest);

                masterClient1.WaitForEvent(EventCode.Join);

                //masterClient2.JoinRandomGame(new JoinRandomGameRequest
                //{
                //    AddUsers = new[] { "Player1" }
                //}, ErrorCode.Ok);

                //masterClient2.JoinRandomGame(new JoinRandomGameRequest
                //{
                //    AddUsers = new[] { "Player2" }
                //}, ErrorCode.Ok);

                Thread.Sleep(200);

                masterClient2.JoinRandomGame(new JoinRandomGameRequest
                {
                    AddUsers = new[] { "Player1", "Player2" },
                    LobbyType = (byte)lobbyType,
                    LobbyName = SlotsLobbyName,
                }, ErrorCode.Ok);
            }
            finally
            {
                DisposeClients(masterClient1, masterClient2);
            }
        }

        [TestCase(LobbyType.Default)]
        [TestCase(LobbyType.SqlLobby)]
        public void Slots_JoinRandomGameWithEmptySlotTest(LobbyType lobbyType)
        {
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires userId to be set");
            }

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_");
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    PlayerTTL = int.MaxValue,
                    GameProperties = new Hashtable
                    {
                        {GamePropertyKey.MaxPlayers, 2}
                    },
                    LobbyType = (byte)lobbyType,
                    LobbyName = SlotsLobbyName,
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                var joinResponse1 = masterClient1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address);
                masterClient1.JoinGame(joinRequest);

                masterClient1.WaitForEvent(EventCode.Join);

                Thread.Sleep(200);

                masterClient2.JoinRandomGame(new JoinRandomGameRequest
                {
                    AddUsers = new[] { string.Empty },
                    LobbyType = (byte)lobbyType,
                    LobbyName = SlotsLobbyName,
                }, ErrorCode.NoRandomMatchFound); // not sure about error ErrorCode.SlotError or ErrorCode.InvalidOperation


                masterClient2.JoinRandomGame(new JoinRandomGameRequest
                {
                    AddUsers = new string[0],
                    LobbyType = (byte)lobbyType,
                    LobbyName = SlotsLobbyName,
                }, ErrorCode.Ok); // not sure about error ErrorCode.SlotError or ErrorCode.InvalidOperation
            }
            finally
            {
                DisposeClients(masterClient1, masterClient2);
            }
        }

        [Test]
        public void Slots_SlotReservationForActiveUserTest()
        {
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires userId to be set");
            }

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;
            UnifiedTestClient masterClient3 = null;
            UnifiedTestClient masterClient4 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_");
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    PlayerTTL = int.MaxValue,
                    GameProperties = new Hashtable
                    {
                        {GamePropertyKey.MaxPlayers, 4}
                    },
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                masterClient3 = this.CreateMasterClientAndAuthenticate(this.Player3);
                masterClient4 = this.CreateMasterClientAndAuthenticate("Player4");

                var joinResponse1 = masterClient1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address);
                masterClient1.JoinGame(joinRequest);

                masterClient1.WaitForEvent(EventCode.Join);

                Thread.Sleep(200);

                var joinRequest2 = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.JoinOnly,
                    CheckUserOnJoin = true,
                };

                joinResponse1 = masterClient2.JoinGame(joinRequest2);

                this.ConnectAndAuthenticate(masterClient2, joinResponse1.Address);
                masterClient2.JoinGame(joinRequest2);

                joinResponse1 = masterClient3.JoinGame(joinRequest2);

                this.ConnectAndAuthenticate(masterClient3, joinResponse1.Address);
                masterClient3.JoinGame(joinRequest2);

                joinRequest2.AddUsers = new[] {this.Player2};
                joinResponse1 = masterClient4.JoinGame(joinRequest2);
                this.ConnectAndAuthenticate(masterClient4, joinResponse1.Address);
                masterClient4.JoinGame(joinRequest2);
            }
            finally
            {
                DisposeClients(masterClient1, masterClient2, masterClient3, masterClient4);
            }
        }

        [Test]
        public void Slots_RejoinOrJoinOnlyModeTest()
        {
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires userId to be set");
            }

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;
            UnifiedTestClient masterClient3 = null;
            UnifiedTestClient masterClient4 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_");
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    PlayerTTL = int.MaxValue,
                    GameProperties = new Hashtable
                    {
                        {GamePropertyKey.MaxPlayers, 0}
                    },
                    AddUsers = new[] { this.Player2, this.Player3 }
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                masterClient3 = this.CreateMasterClientAndAuthenticate(this.Player3);
                masterClient4 = this.CreateMasterClientAndAuthenticate("Player4");

                var joinResponse1 = masterClient1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address);
                masterClient1.JoinGame(joinRequest);

                masterClient1.WaitForEvent(EventCode.Join);

                Thread.Sleep(100);
                var lobbyStatsResponse = masterClient4.GetLobbyStats(null, null);
                Assert.AreEqual(3, lobbyStatsResponse.PeerCount[0]);

                var joinRequest2 = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.RejoinOrJoin,
                    CheckUserOnJoin = true,
                };

                masterClient2.JoinGame(joinRequest2);

                this.ConnectAndAuthenticate(masterClient2, joinResponse1.Address);
                masterClient2.JoinGame(joinRequest2, ErrorCode.OperationNotAllowedInCurrentState);

                Thread.Sleep(100);
                lobbyStatsResponse = masterClient4.GetLobbyStats(null, null);
                Assert.AreEqual(3, lobbyStatsResponse.PeerCount[0]);
            }
            finally
            {
                DisposeClients(masterClient1, masterClient2, masterClient3, masterClient4);
            }
        }

        [Test]
        public void Slots_EveryTeamMateReservesSlotsTest()
        {
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires userId to be set");
            }

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;
            UnifiedTestClient masterClient3 = null;
            UnifiedTestClient masterClient4 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_");
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = true,
                    PlayerTTL = int.MaxValue,
                    GameProperties = new Hashtable
                    {
                        {GamePropertyKey.MaxPlayers, 3}
                    },
                    AddUsers = new[] { this.Player2, this.Player3 }
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                masterClient3 = this.CreateMasterClientAndAuthenticate(this.Player3);
                masterClient4 = this.CreateMasterClientAndAuthenticate("Player4");

                var joinResponse1 = masterClient1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address);
                masterClient1.JoinGame(joinRequest);

                masterClient1.WaitForEvent(EventCode.Join);

                Thread.Sleep(100);
                var lobbyStatsResponse = masterClient4.GetLobbyStats(null, null);
                Assert.AreEqual(3, lobbyStatsResponse.PeerCount[0]);

                var joinRequest2 = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.JoinOnly,
                    CheckUserOnJoin = true,
                    AddUsers = new[] { this.Player1, this.Player3 }
                };

                joinResponse1 = masterClient2.JoinGame(joinRequest2);

                Thread.Sleep(100);
                lobbyStatsResponse = masterClient4.GetLobbyStats(null, null);
                Assert.AreEqual(3, lobbyStatsResponse.PeerCount[0]);

                this.ConnectAndAuthenticate(masterClient2, joinResponse1.Address);
                masterClient2.JoinGame(joinRequest2);

                Thread.Sleep(100);
                lobbyStatsResponse = masterClient4.GetLobbyStats(null, null);
                Assert.AreEqual(3, lobbyStatsResponse.PeerCount[0]);

                joinResponse1 = masterClient3.JoinGame(joinRequest2);

                this.ConnectAndAuthenticate(masterClient3, joinResponse1.Address);
                masterClient3.JoinGame(joinRequest2);

                Thread.Sleep(100);
                lobbyStatsResponse = masterClient4.GetLobbyStats(null, null);
                Assert.AreEqual(3, lobbyStatsResponse.PeerCount[0]);
            }
            finally
            {
                DisposeClients(masterClient1, masterClient2, masterClient3, masterClient4);
            }
        }

        [Test]
        public void Slots_EveryTeamMateReservesSlotsMaxPlayer2Test()
        {
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires userId to be set");
            }

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;
            UnifiedTestClient masterClient4 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_");
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = true,
                    PlayerTTL = int.MaxValue,
                    GameProperties = new Hashtable
                    {
                        {GamePropertyKey.MaxPlayers, 2}
                    },
                    AddUsers = new[] { this.Player1 }
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                masterClient4 = this.CreateMasterClientAndAuthenticate("Player4");

                var joinResponse1 = masterClient1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address);
                masterClient1.JoinGame(joinRequest);

                masterClient1.WaitForEvent(EventCode.Join);

                Thread.Sleep(100);
                var lobbyStatsResponse = masterClient4.GetLobbyStats(null, null);
                Assert.AreEqual(1, lobbyStatsResponse.PeerCount[0]);

                var joinRequest2 = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.JoinOnly,
                    CheckUserOnJoin = true,
                    AddUsers = new[] { this.Player2 }
                };

                joinResponse1 = masterClient2.JoinGame(joinRequest2);

                Thread.Sleep(100);
                lobbyStatsResponse = masterClient4.GetLobbyStats(null, null);
                Assert.AreEqual(2, lobbyStatsResponse.PeerCount[0]);

                this.ConnectAndAuthenticate(masterClient2, joinResponse1.Address);
                masterClient2.JoinGame(joinRequest2);

                Thread.Sleep(100);
                lobbyStatsResponse = masterClient4.GetLobbyStats(null, null);
                Assert.AreEqual(2, lobbyStatsResponse.PeerCount[0]);
            }
            finally
            {
                DisposeClients(masterClient1, masterClient2, masterClient4);
            }
        }

        [Test]
        public void Slots_UserRemovesHisSlotLobbyStatsTest()
        {
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires userId to be set");
            }

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;
            UnifiedTestClient masterClient3 = null;

            const int playersCount = 10;
            var clients = new UnifiedTestClient[playersCount];

            var expectedUsersList = new string[playersCount];

            for (int i = 0; i < playersCount; ++i)
            {
                expectedUsersList[i] = "Player_" + i;
            }
            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_");
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    PlayerTTL = 0,
                    GameProperties = new Hashtable
                    {
                        {GamePropertyKey.MaxPlayers, playersCount}
                    },
                    AddUsers = expectedUsersList
                };

                masterClient3 = this.CreateMasterClientAndAuthenticate(this.Player3);

                for (int i = 0; i < playersCount; ++i)
                {
                    clients[i] = this.CreateMasterClientAndAuthenticate("Player_" + i);
                }

                masterClient1 = clients[0];

                var joinResponse1 = masterClient1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address);
                masterClient1.JoinGame(joinRequest);

                masterClient1.WaitForEvent(EventCode.Join);

                Thread.Sleep(100);
                var lobbyStatsResponse = masterClient3.GetLobbyStats(null, null);
                Assert.AreEqual(playersCount, lobbyStatsResponse.PeerCount[0]);

                // we join player and remove him from expected users list
                // PlayerCount should be same

                var newExpectedPlayersList = new List<string>(expectedUsersList);
                for (int i = 1; i < playersCount; ++i)
                {
                    var newExpectedPlayersArray = newExpectedPlayersList.ToArray();
                    masterClient2 = clients[i];
                    var joinRequest2 = new JoinGameRequest
                    {
                        GameId = roomName,
                        JoinMode = JoinModeConstants.JoinOnly,
                        CheckUserOnJoin = true,
                    };

                    joinResponse1 = masterClient2.JoinGame(joinRequest2);


                    // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                    this.ConnectAndAuthenticate(masterClient2, joinResponse1.Address);
                    masterClient2.JoinGame(joinRequest2);

                    Thread.Sleep(100);
                    lobbyStatsResponse = masterClient3.GetLobbyStats(null, null);
                    Assert.AreEqual(playersCount, lobbyStatsResponse.PeerCount[0]);

                    // remove player from expected users list
                    newExpectedPlayersList.Remove(masterClient2.UserId);

                    var setPropertiesRequest = new OperationRequest
                    {
                        OperationCode = OperationCode.SetProperties,

                        Parameters = new Dictionary<byte, object>
                        {
                            {ParameterCode.ExpectedValues, new Hashtable{{(byte) GameParameter.ExpectedUsers, newExpectedPlayersArray}}},
                            {ParameterCode.Properties, new Hashtable{{(byte) GameParameter.ExpectedUsers, newExpectedPlayersList.ToArray()}}}
                        }
                    };

                    masterClient2.SendRequestAndWaitForResponse(setPropertiesRequest);
                    masterClient1.WaitForEvent(EventCode.PropertiesChanged);

                    // wait for update on master
                    Thread.Sleep(100);
                    lobbyStatsResponse = masterClient3.GetLobbyStats(null, null);
                    Assert.AreEqual(playersCount, lobbyStatsResponse.PeerCount[0]);
                }

                for (int i = 1; i < playersCount; ++i)
                {
                    clients[i].Disconnect();
                }

                Thread.Sleep(100);
                // to remove last one added in previus cycle
                newExpectedPlayersList.Remove(masterClient2.UserId);
                var setPropertiesRequest1 = new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,

                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.ExpectedValues, new Hashtable{{(byte) GameParameter.ExpectedUsers, newExpectedPlayersList.ToArray()}}},
                        {ParameterCode.Properties, new Hashtable{{(byte) GameParameter.ExpectedUsers, null}}}
                    }
                };

                masterClient1.SendRequestAndWaitForResponse(setPropertiesRequest1);
                Thread.Sleep(150);
                masterClient1.Disconnect();

                Thread.Sleep(1000);
                lobbyStatsResponse = masterClient3.GetLobbyStats(null, null);
                Assert.AreEqual(0, lobbyStatsResponse.PeerCount[0]);

                // to get updated logs
                Thread.Sleep(100);
            }
            finally
            {
                DisposeClients(masterClient1, masterClient2, masterClient3);
                foreach (var client in clients)
                {
                    DisposeClients(client);
                }
            }
        }

        /// <summary>
        /// this issue was found by hamza
        /// </summary>
        [Test]
        public void Slots_RepeatingUsersUsingProperties()
        {
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires userId to be set");
            }

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;
            UnifiedTestClient masterClient3 = null;
            UnifiedTestClient masterClient4 = null;
            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_");
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = true,
                    PlayerTTL = int.MaxValue,
                    GameProperties = new Hashtable
                    {
                        {GamePropertyKey.MaxPlayers, 2}
                    },
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                masterClient3 = this.CreateMasterClientAndAuthenticate(this.Player3);
                masterClient4 = this.CreateMasterClientAndAuthenticate("Player4");

                var joinResponse1 = masterClient1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address);
                masterClient1.JoinGame(joinRequest);

                masterClient1.WaitForEvent(EventCode.Join);

                Thread.Sleep(100);
                var lobbyStatsResponse = masterClient4.GetLobbyStats(null, null);
                Assert.AreEqual(1, lobbyStatsResponse.PeerCount[0]);


                var setPropertiesRequest = new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,

                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.ExpectedValues, new Hashtable{{(byte) GameParameter.ExpectedUsers, null}}},
                        {ParameterCode.Properties, new Hashtable{{(byte) GameParameter.ExpectedUsers, new [] {this.Player1, this.Player2, this.Player2}}}}
                    }
                };

                masterClient1.SendRequestAndWaitForResponse(setPropertiesRequest);
            }
            finally
            {
                DisposeClients(masterClient1, masterClient2, masterClient3, masterClient4);
            }
        }

        /// <summary>
        /// this issue was found by hamza
        /// </summary>
        [Test]
        public void Slots_RepeatingUsersUsingPropertiesFullSetOfChecks()
        {
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires userId to be set");
            }

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;
            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_");
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = true,
                    PlayerTTL = int.MaxValue,
                    EmptyRoomLiveTime = 3000,
                    GameProperties = new Hashtable
                    {
                        {GamePropertyKey.MaxPlayers, 2}
                    },
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                var joinResponse1 = masterClient1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address);
                masterClient1.JoinGame(joinRequest);

                masterClient1.WaitForEvent(EventCode.Join);

                Thread.Sleep(100);
                var lobbyStatsResponse = masterClient2.GetLobbyStats(null, null);
                Assert.AreEqual(1, lobbyStatsResponse.PeerCount[0]);
                
                SetExpectedUsers(masterClient1, new[] { this.Player1 });
                
                SetExpectedUsers(masterClient1, new[] { this.Player2 }, new[] { this.Player1 });

                SetExpectedUsers(masterClient1, new[] { this.Player1, this.Player1 }, new[] { this.Player2 });
                SetExpectedUsers(masterClient1, new[] { this.Player1, this.Player2 }, new[] { this.Player1 });
                SetExpectedUsers(masterClient1, new[] { this.Player2, this.Player2 }, new[] { this.Player1, this.Player2 });
                SetExpectedUsers(masterClient1, new[] { this.Player1, this.Player1, this.Player1 }, new[] { this.Player2 });
                SetExpectedUsers(masterClient1, new[] { this.Player1, this.Player1, this.Player2 }, new[] { this.Player1 });
                SetExpectedUsers(masterClient1, new[] { this.Player1, this.Player2, this.Player2 }, new[] { this.Player1, this.Player2 });
                SetExpectedUsers(masterClient1, new[] { this.Player2, this.Player2, this.Player2 }, new[] { this.Player1, this.Player2 });

                SetExpectedUsers(masterClient1, new[] { this.Player1, this.Player2, this.Player3, "Player4", "Player5" }, new[] { this.Player2 }, expectedResult:ErrorCode.InvalidOperation);
                SetExpectedUsers(masterClient1, new[] { this.Player2, this.Player2, this.Player3 }, new[] { this.Player2 }, expectedResult:ErrorCode.InvalidOperation);

                SetExpectedUsers(masterClient1, new[] { this.Player1, this.Player1, this.Player3 }, new[] { this.Player2 });

                SetExpectedUsers(masterClient1, new[] { this.Player1, this.Player1, this.Player2, this.Player2 }, new[] { this.Player1, this.Player3 });

                var setMaxPlayers = new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,

                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Properties, new Hashtable{{(byte) GameParameter.MaxPlayers, 3}}}
                    }
                };

                masterClient1.SendRequestAndWaitForResponse(setMaxPlayers);

                masterClient1.LeaveGame(true);

                masterClient1.Disconnect();

                Thread.Sleep(100);

                joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.RejoinOrJoin,
                    CheckUserOnJoin = true,
                    PlayerTTL = int.MaxValue,
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                joinResponse1 = masterClient1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address);
                masterClient1.JoinGame(joinRequest);


                SetExpectedUsers(masterClient1, new[] { this.Player1 }, new[] { this.Player1, this.Player2 });
                SetExpectedUsers(masterClient1, new[] { this.Player2 }, new[] { this.Player1 });
                SetExpectedUsers(masterClient1, new[] { this.Player1, this.Player1 }, new[] { this.Player2 });
                SetExpectedUsers(masterClient1, new[] { this.Player2, this.Player2 }, new[] { this.Player1 });
                SetExpectedUsers(masterClient1, new[] { this.Player1, this.Player1, this.Player1 }, new[] { this.Player2 });
                SetExpectedUsers(masterClient1, new[] { this.Player1, this.Player1, this.Player2 }, new[] { this.Player1 });
                SetExpectedUsers(masterClient1, new[] { this.Player1, this.Player2, this.Player2 }, new[] { this.Player1, this.Player2 });
                SetExpectedUsers(masterClient1, new[] { this.Player2, this.Player2, this.Player2 }, new[] { this.Player1, this.Player2 });
                SetExpectedUsers(masterClient1, new[] { this.Player1, this.Player2, this.Player3 }, new[] { this.Player2 });
                SetExpectedUsers(masterClient1, new[] { this.Player1, this.Player1, this.Player3 }, new[] { this.Player1, this.Player2, this.Player3 });

                SetExpectedUsers(masterClient1, new string[0], new[] { this.Player1, this.Player3 });


                masterClient1.LeaveGame();
            }
            finally
            {
                DisposeClients(masterClient1, masterClient2);
            }
        }

        /// <summary>
        /// Case provide by hamza we want change maxplayers and exepcted users in one call
        /// we also set big number of expected users and then 
        /// </summary>
        [Test]
        public void Slots_SetPropertiesWithMaxPlayerAndExpectedUsers()
        {
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires userId to be set");
            }

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;
            UnifiedTestClient masterClient3 = null;
            UnifiedTestClient masterClient4 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_");
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = true,
                    PlayerTTL = int.MaxValue,
                    GameProperties = new Hashtable
                    {
                        {GamePropertyKey.MaxPlayers, 2}
                    },
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                masterClient3 = this.CreateMasterClientAndAuthenticate(this.Player3);
                masterClient4 = this.CreateMasterClientAndAuthenticate("Player4");

                var joinResponse1 = masterClient1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address);
                masterClient1.JoinGame(joinRequest);

                masterClient1.WaitForEvent(EventCode.Join);

                Thread.Sleep(100);
                var lobbyStatsResponse = masterClient4.GetLobbyStats(null, null);
                Assert.AreEqual(1, lobbyStatsResponse.PeerCount[0]);


                var setPropertiesRequest = new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,

                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.ExpectedValues, new Hashtable{{(byte) GameParameter.ExpectedUsers, null}}},
                        {
                            ParameterCode.Properties, new Hashtable
                            {
                                {(byte) GameParameter.ExpectedUsers, new [] {this.Player1}},
                                {(byte) GameParameter.MaxPlayers, (byte)1}
                            }
                        }
                    }
                };

                masterClient1.SendRequestAndWaitForResponse(setPropertiesRequest);

                setPropertiesRequest = new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,

                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.ExpectedValues, new Hashtable{{(byte) GameParameter.ExpectedUsers, new [] {this.Player1}}}},
                        {
                            ParameterCode.Properties, new Hashtable
                            {
                                {(byte) GameParameter.ExpectedUsers, new [] {this.Player1, this.Player2, this.Player3}},
                                {(byte) GameParameter.MaxPlayers, (byte)3}
                            }
                        }
                    }
                };

                masterClient1.SendRequestAndWaitForResponse(setPropertiesRequest);

                setPropertiesRequest = new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,

                    Parameters = new Dictionary<byte, object>
                    {
                        {
                            ParameterCode.Properties, new Hashtable
                            {
                                {(byte) GameParameter.MaxPlayers, (byte)2}
                            }
                        }
                    }
                };

                masterClient1.SendRequestAndWaitForResponse(setPropertiesRequest);
            }
            finally
            {
                DisposeClients(masterClient1, masterClient2, masterClient3, masterClient4);
            }
        }

        [Test]
        public void Slots_SetPropertiesWithMaxPlayersAndExpectedUsers_WithInactiveActors()
        {
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires userId to be set");
            }

            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_");
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = true,
                    PlayerTTL = -1
                };

                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                var joinResponse = client1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(client1, joinResponse.Address);
                client1.JoinGame(joinRequest);

                client1.WaitForEvent(EventCode.Join);
                
                joinResponse = client2.JoinGame(joinRequest);

                // client 2: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(client2, joinResponse.Address);
                client2.JoinGame(joinRequest);

                client2.WaitForEvent(EventCode.Join);

                client1.LeaveGame(true);

                EventData leaveEvent = client2.WaitForEvent(EventCode.Leave);

                Assert.AreEqual(true, leaveEvent[(byte)ParameterKey.IsInactive]);
                
                SetExpectedUsers(client2, new[] { this.Player1 });
                SetExpectedUsers(client2, new[] { this.Player1, this.Player2 }, new[] { this.Player1 });
                SetExpectedUsers(client2, new[] { this.Player1, this.Player2, this.Player2 }, new[] { this.Player1, this.Player2 }, 2);
                SetExpectedUsers(client2, new[] { this.Player1, this.Player2, this.Player1, this.Player2 }, new[] { this.Player1, this.Player2 }, 2);
                SetExpectedUsers(client2, new[] { this.Player3 }, new[] { this.Player1, this.Player2 }, 3);
                SetExpectedUsers(client2, new[] { this.Player3, this.Player3 }, new[] { this.Player3 }, 3);
                SetExpectedUsers(client2, new[] { this.Player2, this.Player3 }, new[] { this.Player3 }, 3);
                SetExpectedUsers(client2, new[] { this.Player1, this.Player2, this.Player3 }, new[] { this.Player2, this.Player3 }, 3);
                SetExpectedUsers(client2, new[] { this.Player1, this.Player2, this.Player3, "Player4" }, new [] { this.Player1, this.Player2, this.Player3 }, 3, ErrorCode.InvalidOperation);
                SetExpectedUsers(client2, new[] { this.Player1, this.Player2, this.Player3 }, new[] { this.Player1, this.Player2, this.Player3 }, 2, ErrorCode.InvalidOperation);
                SetExpectedUsers(client2, new[] { this.Player1, this.Player2 }, new[] { this.Player1, this.Player2, this.Player3 }, 2);
                SetExpectedUsers(client2, new[] { this.Player1, this.Player2, this.Player2 }, new[] { this.Player1, this.Player2 }, 2);
                
                var rejoinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.RejoinOnly
                };
                
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                joinResponse = client1.JoinGame(rejoinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(client1, joinResponse.Address);
                joinResponse = client1.JoinGame(rejoinRequest);

                Assert.AreEqual(joinResponse.GameProperties[(byte)GameParameter.ExpectedUsers], new[] { this.Player1, this.Player2 });
                Assert.AreEqual(joinResponse.GameProperties[(byte)GameParameter.MaxPlayers], (byte)2);
            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [Test]
        public void Slots_SetExpectedUsersOnlyWithCAS()
        {
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires userId to be set");
            }

            UnifiedTestClient client1 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_");
                Hashtable properties = new Hashtable
                {
                    {"k0", "v0" }
                };
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = true,
                    EmptyRoomLiveTime = 5000,
                    PlayerTTL = -1, 
                    properties = properties
                };

                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                var joinResponse = client1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(client1, joinResponse.Address);
                client1.JoinGame(joinRequest);

                client1.WaitForEvent(EventCode.Join);

                string[] expectedUsersToSet = { this.Player1 };
                properties.Remove("k0");
                properties.Add((byte) GameParameter.ExpectedUsers, expectedUsersToSet);
                OperationRequest setPropertiesRequest = new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {
                            ParameterCode.Properties, properties
                        }
                    }
                };
                client1.SendRequestAndWaitForResponse(setPropertiesRequest, ErrorCode.InvalidOperation);

                Hashtable expectedProperties = new Hashtable();
                setPropertiesRequest.Parameters.Add(ParameterCode.ExpectedValues, expectedProperties);

                client1.SendRequestAndWaitForResponse(setPropertiesRequest, ErrorCode.InvalidOperation);
                
                expectedProperties.Add("k", "v"); // does not exist on server
                client1.SendRequestAndWaitForResponse(setPropertiesRequest, ErrorCode.InvalidOperation);
                
                expectedProperties.Add((byte) GameParameter.ExpectedUsers, new string[0]);
                client1.SendRequestAndWaitForResponse(setPropertiesRequest, ErrorCode.InvalidOperation);

                expectedProperties.Remove("k");
                client1.SendRequestAndWaitForResponse(setPropertiesRequest, ErrorCode.InvalidOperation);

                expectedProperties.Add("k0", "vX"); // wrong value on server
                client1.SendRequestAndWaitForResponse(setPropertiesRequest, ErrorCode.InvalidOperation);

                expectedProperties[(byte) GameParameter.ExpectedUsers] = null;
                client1.SendRequestAndWaitForResponse(setPropertiesRequest, ErrorCode.InvalidOperation);

                expectedProperties.Remove("k0");
                client1.SendRequestAndWaitForResponse(setPropertiesRequest);

                EventData propertiesChangedEvent = client1.WaitForEvent(EventCode.PropertiesChanged);
                Hashtable receivedProperties = propertiesChangedEvent[ParameterCode.Properties] as Hashtable;
                string[] receivedArray = receivedProperties[(byte) GameParameter.ExpectedUsers] as string[];
                Assert.AreEqual(expectedUsersToSet, receivedArray);

                expectedUsersToSet = new[]{ this.Player2 };
                expectedProperties[(byte) GameParameter.ExpectedUsers] = expectedUsersToSet;
                client1.SendRequestAndWaitForResponse(setPropertiesRequest, ErrorCode.InvalidOperation);

                expectedUsersToSet = new[]{ this.Player1, this.Player2 };
                expectedProperties[(byte) GameParameter.ExpectedUsers] = expectedUsersToSet;
                client1.SendRequestAndWaitForResponse(setPropertiesRequest, ErrorCode.InvalidOperation);

                expectedUsersToSet = new[]{ this.Player1, this.Player2, "Player3" };
                expectedProperties[(byte) GameParameter.ExpectedUsers] = expectedUsersToSet;
                client1.SendRequestAndWaitForResponse(setPropertiesRequest, ErrorCode.InvalidOperation);

                expectedUsersToSet = new[]{ "Player3" };
                expectedProperties[(byte) GameParameter.ExpectedUsers] = expectedUsersToSet;
                client1.SendRequestAndWaitForResponse(setPropertiesRequest, ErrorCode.InvalidOperation);

                expectedUsersToSet = new[]{ this.Player1, this.Player2 };
                properties[(byte) GameParameter.ExpectedUsers] = expectedUsersToSet;
                expectedProperties[(byte) GameParameter.ExpectedUsers] = receivedArray;

                client1.SendRequestAndWaitForResponse(setPropertiesRequest);

                propertiesChangedEvent = client1.WaitForEvent(EventCode.PropertiesChanged);
                receivedProperties = propertiesChangedEvent[ParameterCode.Properties] as Hashtable;
                receivedArray = receivedProperties[(byte) GameParameter.ExpectedUsers] as string[];
                Assert.AreEqual(expectedUsersToSet, receivedArray);

                client1.LeaveGame(true);
                
                var rejoinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.RejoinOnly
                };
                
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                joinResponse = client1.JoinGame(rejoinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(client1, joinResponse.Address);
                joinResponse = client1.JoinGame(rejoinRequest);

                Assert.AreEqual(joinResponse.GameProperties[(byte)GameParameter.ExpectedUsers], expectedUsersToSet);
            }
            finally
            {
                DisposeClients(client1);
            }
        }
        
        [Test]
        public void Slots_CheckMayAddSlotsStress()
        {
            // Possible combinations
            // MaxPlayers:
            // * MaxPlayers unchanged
            // * MaxPlayers =< roomSize (roomSize = actorActorsCount + inactiveActorsCount + expectedUsersNOT_JOINED_YET)
            // * MaxPlayers > roomSize
            // ExpectedUsers:
            // * add / remove:
            //   - joined active actor
            //   - joined inactive actor
            //   - excluded actor* (this is advanced, let's not test it now)
            //   - new actor

           if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires userId to be set");
            }

            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_");
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.CreateIfNotExists,
                    CheckUserOnJoin = true,
                    PlayerTTL = -1
                };

                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                var joinResponse = client1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(client1, joinResponse.Address);
                client1.JoinGame(joinRequest);

                client1.WaitForEvent(EventCode.Join);
                
                joinResponse = client2.JoinGame(joinRequest);

                // client 2: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(client2, joinResponse.Address);
                client2.JoinGame(joinRequest);

                client2.WaitForEvent(EventCode.Join);

                client1.LeaveGame(true);

                EventData leaveEvent = client2.WaitForEvent(EventCode.Leave);

                Assert.AreEqual(true, leaveEvent[(byte)ParameterKey.IsInactive]);

                string[] current = {this.Player1, this.Player2, this.Player3};
                string[] toSet = current;
                SetExpectedUsers(client2, toSet); // N = 3, R = 0 => A = 1, I = 1, Y = 1, E = 3, S = 3, M = 0
                // if M > 0 { M >= S = A + I + Y }
                toSet = new[] {"Player4", "Player5"};
                SetExpectedUsers(client2, toSet, current, 3, ErrorCode.InvalidOperation); // N = 2, R = 3
                
                toSet = new[] {this.Player3};
                SetExpectedUsers(client2, toSet, current, 2, ErrorCode.InvalidOperation);

                toSet = new[] {this.Player1};
                SetExpectedUsers(client2, toSet, current, 1, ErrorCode.InvalidOperation);

                toSet = new[] {this.Player2};
                SetExpectedUsers(client2, toSet, current, 1, ErrorCode.InvalidOperation);

                var rejoinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModeConstants.RejoinOnly
                };
                
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                joinResponse = client1.JoinGame(rejoinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(client1, joinResponse.Address);
                joinResponse = client1.JoinGame(rejoinRequest);

                Assert.AreEqual(joinResponse.GameProperties[(byte)GameParameter.ExpectedUsers], current);
                //Assert.AreEqual(joinResponse.GameProperties[(byte)GameParameter.MaxPlayers], (byte)2);
            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        private void SetExpectedUsers(UnifiedTestClient client, string[] newArray, string[] currentArray = null, byte? newMaxPlayers = null, short expectedResult = 0)
        {
            Hashtable properties = new Hashtable
            {
                {(byte) GameParameter.ExpectedUsers, newArray}
            };
            if (newMaxPlayers.HasValue)
            {
                properties.Add((byte)GameParameter.MaxPlayers, newMaxPlayers.Value);
            }
            OperationRequest setPropertiesRequest = new OperationRequest
            {
                OperationCode = OperationCode.SetProperties,
                Parameters = new Dictionary<byte, object>
                {
                    {ParameterCode.ExpectedValues, new Hashtable{{(byte) GameParameter.ExpectedUsers, currentArray}}},
                    {
                        ParameterCode.Properties, properties
                    }
                }
            };
            client.SendRequestAndWaitForResponse(setPropertiesRequest, expectedResult);
            if (expectedResult == ErrorCode.Ok)
            {
                string[] arrayToBeReceived = new HashSet<string>(newArray).ToArray(); // remove duplicates, preserve order
                EventData propertiesChangedEvent = client.WaitForEvent(EventCode.PropertiesChanged);
                Hashtable receivedProperties = propertiesChangedEvent[ParameterCode.Properties] as Hashtable;
                string[] receivedArray = receivedProperties[(byte) GameParameter.ExpectedUsers] as string[];
                Assert.AreEqual(arrayToBeReceived, receivedArray);
                if (newMaxPlayers.HasValue)
                {
                    byte receivedValue = (byte)receivedProperties[(byte) GameParameter.MaxPlayers];
                    Assert.AreEqual(newMaxPlayers.Value, receivedValue);
                }
            }
        }
        #endregion
    }
}
