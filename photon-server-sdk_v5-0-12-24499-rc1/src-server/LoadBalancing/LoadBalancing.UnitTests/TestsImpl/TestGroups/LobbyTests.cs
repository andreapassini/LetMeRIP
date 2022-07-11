using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using ExitGames.Client.Photon;
using Photon.Realtime;
using NUnit.Framework;
using Photon.Common.Authentication;
using Photon.Hive.Common.Lobby;
using Photon.Hive.Operations;
using Photon.LoadBalancing.Operations;
using Photon.LoadBalancing.UnifiedClient;
using Photon.LoadBalancing.UnitTests.UnifiedServer;
using Photon.UnitTest.Utils.Basic;
using ErrorCode = Photon.Realtime.ErrorCode;
using EventCode = Photon.Realtime.EventCode;
using Hashtable = System.Collections.Hashtable;
using OperationCode = Photon.Realtime.OperationCode;
using ParameterCode = Photon.Realtime.ParameterCode;

namespace Photon.LoadBalancing.UnitTests.UnifiedTests
{
    public abstract partial class LBApiTestsImpl
    {
        #region Lobby Tests

        [Test]
        public void SqlLobbyMatchmaking()
        {
            UnifiedTestClient masterClient = null;
            UnifiedTestClient[] gameClients = null;

            try
            {
                const string lobbyName = "SqlLobby1";
                const byte lobbyType = 2;

                gameClients = new UnifiedTestClient[3];

                for (int i = 0; i < gameClients.Length; i++)
                {
                    var gameProperties = new Hashtable();
                    switch (i)
                    {
                        case 1:
                            gameProperties.Add("C0", 10);
                            break;

                        case 2:
                            gameProperties.Add("C0", "Map1");
                            break;
                    }

                    var roomName = "SqlLobbyMatchmaking" + i;
                    gameClients[i] = this.CreateGameOnGameServer("GameClient" + i, roomName, lobbyName, lobbyType, true, true, 0, gameProperties,
                        null);
                }


                masterClient = this.CreateMasterClientAndAuthenticate("Tester");

                // client didn't joined lobby so all requests without 
                // a lobby specified should not return a match
                masterClient.JoinRandomGame(null, null, ErrorCode.NoRandomMatchFound);
                masterClient.JoinRandomGame(null, "C0=10", ErrorCode.NoRandomMatchFound);

                masterClient.Dispose();
                masterClient = this.CreateMasterClientAndAuthenticate("Tester");
                // specifing the lobbyname and type should give some matches
                masterClient.JoinLobby(lobbyName, lobbyType);
                masterClient.JoinRandomGame(null, null, ErrorCode.Ok, lobbyName, lobbyType);
                masterClient.JoinRandomGame(null, "C0=1", ErrorCode.NoRandomMatchFound, lobbyName, lobbyType);
                masterClient.JoinRandomGame(null, "C0<10", ErrorCode.NoRandomMatchFound, lobbyName, lobbyType);

                masterClient.Dispose();
                masterClient = this.CreateMasterClientAndAuthenticate("Tester");
                masterClient.JoinRandomGame(null, "C0>10", ErrorCode.Ok, lobbyName, lobbyType, "SqlLobbyMatchmaking2");
                masterClient.JoinRandomGame(null, "C0=10", ErrorCode.Ok, lobbyName, lobbyType, "SqlLobbyMatchmaking1");
                masterClient.Dispose();
                masterClient = this.CreateMasterClientAndAuthenticate("Tester");

                masterClient.JoinRandomGame(null, "C0>0", ErrorCode.Ok, lobbyName, lobbyType, "SqlLobbyMatchmaking1");
                masterClient.JoinRandomGame(null, "C0<20", ErrorCode.Ok, lobbyName, lobbyType, "SqlLobbyMatchmaking1");

                masterClient.Dispose();
                masterClient = this.CreateMasterClientAndAuthenticate("Tester");

                masterClient.JoinRandomGame(null, "C0='Map2'", ErrorCode.NoRandomMatchFound, lobbyName, lobbyType);
                masterClient.JoinRandomGame(null, "C0='Map1'", ErrorCode.Ok, lobbyName, lobbyType, "SqlLobbyMatchmaking2");

                masterClient.Dispose();
                masterClient = this.CreateMasterClientAndAuthenticate("Tester");

                masterClient.JoinRandomGame(null, "C0='Map1'", ErrorCode.Ok, lobbyName, lobbyType, "SqlLobbyMatchmaking2");

                // join client to lobby. Matches could be found without 
                // specifying the lobby
                masterClient.JoinLobby(lobbyName, lobbyType);
                masterClient.JoinRandomGame(null, null, ErrorCode.Ok);

                masterClient.Dispose();
                masterClient = this.CreateMasterClientAndAuthenticate("Tester");

                masterClient.JoinRandomGame(null, "C0=1", ErrorCode.NoRandomMatchFound);
                masterClient.JoinRandomGame(null, "C0<10", ErrorCode.NoRandomMatchFound);

                masterClient.Dispose();
                masterClient = this.CreateMasterClientAndAuthenticate("Tester");
                masterClient.JoinLobby(lobbyName, lobbyType);

                masterClient.JoinRandomGame(null, "C0>10", ErrorCode.Ok, null, null, "SqlLobbyMatchmaking2");
                masterClient.JoinRandomGame(null, "C0=10", ErrorCode.Ok);

                masterClient.Dispose();
                masterClient = this.CreateMasterClientAndAuthenticate("Tester");
                masterClient.JoinLobby(lobbyName, lobbyType);

                masterClient.JoinRandomGame(null, "C0>0", ErrorCode.Ok, null, null, "SqlLobbyMatchmaking1");
                masterClient.JoinLobby(lobbyName, lobbyType);
                masterClient.JoinRandomGame(null, "C0<20", ErrorCode.Ok, null, null, "SqlLobbyMatchmaking1");

                masterClient.Dispose();
                masterClient = this.CreateMasterClientAndAuthenticate("Tester");

                masterClient.JoinLobby(lobbyName, lobbyType);
                masterClient.JoinRandomGame(null, "C0='Map2'", ErrorCode.NoRandomMatchFound);
                masterClient.JoinRandomGame(null, "C0='Map1'", ErrorCode.Ok, null, null, "SqlLobbyMatchmaking2");

                // invalid sql should return error
                var joinResponse = masterClient.JoinRandomGame(null, "GRTF", ErrorCode.InvalidOperation);
                Assert.AreEqual(ErrorCode.InvalidOperation, joinResponse.ReturnCode);
            }
            finally
            {
                DisposeClients(masterClient);
                DisposeClients(gameClients);
            }
        }

        [Test]
        public void SqlLobbyEmptyPropertiesMatchmaking()
        {
            UnifiedTestClient masterClient = null;
            UnifiedTestClient masterClient2 = null;

            try
            {
                const string lobbyName = "Sql";
                const byte lobbyType = 2;

                var gameProperties = new Hashtable
                {
                    {"C0", null}
                };

                var roomName = MethodBase.GetCurrentMethod().Name;
                masterClient2 = this.CreateGameOnGameServer("GameClient", roomName, lobbyName, lobbyType, true, true, 0, gameProperties, null);

                Thread.Sleep(10);

                masterClient = this.CreateMasterClientAndAuthenticate("Tester");

                // specifing the lobbyname and type should give some matches
                masterClient.JoinRandomGame(null, null, ErrorCode.Ok, lobbyName, lobbyType);
                masterClient.JoinRandomGame(null, "C0=''", ErrorCode.NoRandomMatchFound, lobbyName, lobbyType);
            }
            finally
            {
                DisposeClients(masterClient);
                DisposeClients(masterClient2);
            }
        }

        [Test]
        public void SqlLobbyPropertiesUpdateBug()
        {
            UnifiedTestClient masterClient = null;
            UnifiedTestClient gameClients = null;

            try
            {
                const string lobbyName = "SqlLobby1";
                const byte lobbyType = 2;

                var gameProperties = new Hashtable {{"C0", 10}};
                var roomName = MethodBase.GetCurrentMethod().Name;
                gameClients = this.CreateGameOnGameServer("GameClient", roomName, lobbyName, lobbyType, true, true, 0, gameProperties, null);

                var setPrpertiesRequest = new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Properties, new Hashtable {{"C0", 1}}}
                    }
                };
                gameClients.SendRequestAndWaitForResponse(setPrpertiesRequest);

                masterClient = this.CreateMasterClientAndAuthenticate("Tester");


                //// specifing the lobbyname and type should give some matches
                //masterClient.JoinLobby(lobbyName, lobbyType);
                masterClient.JoinRandomGame(null, null, ErrorCode.Ok, lobbyName, lobbyType);

                masterClient.Disconnect();
                masterClient = this.CreateMasterClientAndAuthenticate("Tester");
                masterClient.JoinRandomGame(null, "C0=1", ErrorCode.Ok, lobbyName, lobbyType);
                masterClient.JoinRandomGame(null, "C0=10", ErrorCode.NoRandomMatchFound, lobbyName, lobbyType, roomName);
            }
            finally
            {
                DisposeClients(masterClient);
                DisposeClients(gameClients);
            }
        }

        [Test]
        public void SqlLobbyMaxPlayersNoFilter()
        {
            if (!this.IsOffline)
            {
                Assert.Ignore("This test works only in offline mode");
            }
            
            UnifiedTestClient masterClient = null;
            UnifiedTestClient gameClient1 = null;
            UnifiedTestClient gameClient2 = null;

            const string lobbyName = "SqlLobbyMaxPlayers";
            const byte lobbyType = 2;

            try
            {
                string roomName = this.GenerateRandomizedRoomName("SqlLobbyMaxPlayers_");
                gameClient1 = this.CreateGameOnGameServer(Player1, roomName, lobbyName, lobbyType, true, true, 1, null, null);

                // join 2nd client on master - full: 
                masterClient = this.CreateMasterClientAndAuthenticate("Tester");

                masterClient.JoinRandomGame(null, null, ErrorCode.NoRandomMatchFound);
                masterClient.JoinRandomGame(null, "C0=10", ErrorCode.NoRandomMatchFound);

                // specifing the lobbyname and type should give some matches
                masterClient.JoinLobby(lobbyName, lobbyType);
                masterClient.JoinGame(roomName, ErrorCode.GameFull);

                // join random 2nd client on master - full: 
                var joinRequest = new JoinRandomGameRequest();
                masterClient.JoinRandomGame(joinRequest, ErrorCode.NoRandomMatchFound);
                joinRequest.JoinRandomType = (byte) MatchmakingMode.SerialMatching;
                masterClient.JoinRandomGame(joinRequest, ErrorCode.NoRandomMatchFound);
                joinRequest.JoinRandomType = (byte) MatchmakingMode.RandomMatching;
                masterClient.JoinRandomGame(joinRequest, ErrorCode.NoRandomMatchFound);
                masterClient.Dispose();

                // join directly on GS: 
                gameClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                // we update token to pass host and game check on GS
                this.UpdateTokensGSAndGame(gameClient2, "localhost", roomName);
                this.ConnectAndAuthenticate(gameClient2, gameClient1.RemoteEndPoint, gameClient2.UserId);
                gameClient2.JoinGame(roomName, ErrorCode.GameFull);
            }
            finally
            {
                DisposeClients(masterClient, gameClient1, gameClient2);
            }
        }

        [Test]
        public void SqlLobbyPlayerCountChanged()
        {
            if (!this.IsOffline)
            {
                Assert.Ignore("This test works only in offline mode");
            }

            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;
            UnifiedTestClient client3 = null;
            UnifiedTestClient client4 = null;

            const string lobbyName = "SqlLobbyPlayerCountChanging";
            const byte lobbyType = 2;

            try
            {
                string roomName = this.GenerateRandomizedRoomName("SqlLobbyPlayerCountChanging_");
                client1 = this.CreateGameOnGameServer("Client1", roomName, lobbyName, lobbyType, true, true, 3, null, null);


                // join 2nd client 
                client2 = this.CreateMasterClientAndAuthenticate("Client2");
                client2.JoinLobby(lobbyName, lobbyType);
                var response = client2.JoinGame(roomName);

                // ok
                this.ConnectAndAuthenticate(client2, response.Address, client2.UserId);
                client2.JoinGame(roomName);

                Thread.Sleep(500);

                // ok
                client3 = this.CreateMasterClientAndAuthenticate("Client3");
                client3.JoinLobby(lobbyName, lobbyType);
                response = client3.JoinGame(roomName);

                this.ConnectAndAuthenticate(client3, response.Address, client3.UserId);
                client3.JoinGame(roomName);

                // test with client #4 
                client4 = this.CreateMasterClientAndAuthenticate("Client4");
                client4.JoinLobby(lobbyName, lobbyType);

                this.UpdateTokensGSAndGame(client4, "localhost", roomName);
                // ok
                this.ConnectAndAuthenticate(client4, response.Address, client4.UserId);

                // join directly - without any prior join on Master: 
                client4.JoinGame(roomName, ErrorCode.GameFull);

                client3.Disconnect();
                Thread.Sleep(500);

                this.ConnectAndAuthenticate(client4, response.Address);
                // now succeed:
                client4.JoinGame(roomName);
            }
            finally
            {
                DisposeClients(client1, client2, client3, client4);
            }
        }

        [Test]
        public void SqlLobbyMaxPlayersWithFilter()
        {

            UnifiedTestClient gameClient1 = null;

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;

            const string lobbyName = "SqlLobbyMaxPlayers";
            const byte lobbyType = 2;

            try
            {
                string roomName = this.GenerateRandomizedRoomName("SqlLobbyMaxPlayers_");
                var gameProperties = new Hashtable();
                gameProperties["C0"] = 10;
                gameProperties["C5"] = "Name";

                gameClient1 = this.CreateGameOnGameServer(Player1, roomName, lobbyName, lobbyType, true, true, 2, gameProperties, null);

                masterClient1 = this.CreateMasterClientAndAuthenticate("Tester1");
                masterClient2 = this.CreateMasterClientAndAuthenticate("Tester2");

                if (string.IsNullOrEmpty(this.Player1) && masterClient1.Token == null)
                {
                    Assert.Ignore("This test does not work correctly for old clients without userId and token");
                }

                // join 2nd client on master - no matches without lobby:
                masterClient1.JoinRandomGame(null, null, ErrorCode.NoRandomMatchFound);
                masterClient1.JoinRandomGame(null, "C0=10", ErrorCode.NoRandomMatchFound);

                // specifing the lobbyname and type should give some matches
                masterClient1.JoinLobby(lobbyName, lobbyType);
                masterClient2.JoinLobby(lobbyName, lobbyType);


                // join random - with filter:
                var joinRequest = new JoinRandomGameRequest {QueryData = "C0=10"};
                masterClient1.JoinRandomGame(joinRequest, ErrorCode.Ok);
                masterClient2.JoinRandomGame(joinRequest, ErrorCode.NoRandomMatchFound);


                // join directly on GS: 
                this.ConnectAndAuthenticate(masterClient1, gameClient1.RemoteEndPoint, masterClient1.UserId);
                masterClient1.JoinGame(roomName);

                masterClient2.JoinGame(roomName, ErrorCode.GameFull);

                // disconnect second client
                gameClient1.LeaveGame();
                gameClient1.Dispose();
                Thread.Sleep(500); // give the app lobby some time to update the game state

                masterClient2.JoinRandomGame(joinRequest, ErrorCode.Ok);

            }
            finally
            {
                DisposeClients(masterClient1, masterClient2, gameClient1);
            }
        }

        [Test]
        public void SqlLobbyMaxPlayer()
        {
            UnifiedTestClient masterClient = null;
            UnifiedTestClient gameClient = null;

            try
            {
                const string lobbyName = "SqlMaxPlayerLobby";
                const string roomName = "SqlMaxPlayer";
                const string roomName2 = "SqlMaxPlayer2";
                const string roomName3 = "SqlMaxPlayer3";
                const byte lobbyType = 2;

                var customRoomProperties = new Hashtable();
                customRoomProperties["C0"] = 1;

                var propsToListInLobby = new string[customRoomProperties.Count];
                propsToListInLobby[0] = "C0";


                gameClient = this.CreateGameOnGameServer(Player1, roomName, lobbyName, lobbyType, true, true, null, customRoomProperties,
                    propsToListInLobby);
                if (string.IsNullOrEmpty(this.Player1) && gameClient.Token == null)
                {
                    Assert.Ignore("This test does not work correctly for old clients without userId and token");
                }

                masterClient = this.CreateMasterClientAndAuthenticate(Player2);
                masterClient.JoinRandomGame(null, "C0>0", ErrorCode.Ok, lobbyName, lobbyType);
                masterClient.Disconnect();
                gameClient.Disconnect();

                gameClient = this.CreateGameOnGameServer(Player1, roomName2, lobbyName, lobbyType, true, true, 2, customRoomProperties,
                    propsToListInLobby);
                masterClient = this.CreateMasterClientAndAuthenticate(Player2);
                masterClient.JoinRandomGame(null, "C0>0", ErrorCode.Ok, lobbyName, lobbyType);
                masterClient.Disconnect();
                gameClient.Disconnect();

                gameClient = this.CreateGameOnGameServer(Player1, roomName3, lobbyName, lobbyType, true, true, 4, customRoomProperties,
                    propsToListInLobby);
                masterClient = this.CreateMasterClientAndAuthenticate(Player2);
                masterClient.JoinRandomGame(null, "C0>0", ErrorCode.Ok, lobbyName, lobbyType);
                masterClient.Disconnect();
                gameClient.Disconnect();

            }
            finally
            {
                DisposeClients(masterClient, gameClient);
            }
        }

        [Test]
        public void SqlLobbyWrongQueryData()
        {
            UnifiedTestClient masterClient = null;
            UnifiedTestClient gameClient = null;

            if (this.connectPolicy.IsRemote)
            {
                Assert.Ignore("This feature is not activated on cloud yet");
            }

            try
            {
                const string lobbyName = "SqlMaxPlayerLobby";
                const string roomName = "SqlMaxPlayer";
                const byte lobbyType = 2;

                var customRoomProperties = new Hashtable();
                customRoomProperties.Add("C0", 1);

                var propsToListInLobby = new string[customRoomProperties.Count];
                propsToListInLobby[0] = "C0";


                gameClient = this.CreateGameOnGameServer(Player1, roomName, lobbyName, lobbyType, true, true, null, customRoomProperties,
                    propsToListInLobby);
                if (string.IsNullOrEmpty(this.Player1) && gameClient.Token == null)
                {
                    Assert.Ignore("This test does not work correctly for old clients without userId and token");
                }

                masterClient = this.CreateMasterClientAndAuthenticate(Player2);
                masterClient.JoinRandomGame(null, "C0>0", ErrorCode.Ok, lobbyName, lobbyType);
                //semicolons are now allowed, used to send multiple queries with one call
//                masterClient.JoinRandomGame(null, "C0>0;", ErrorCode.InvalidOperation, lobbyName, lobbyType);
                masterClient.JoinRandomGame(null, "C0>0 JOIN", ErrorCode.InvalidOperation, lobbyName, lobbyType);

                masterClient.Disconnect();
                gameClient.Disconnect();

            }
            finally
            {
                DisposeClients(masterClient, gameClient);
            }
        }

        [Test]
        [Explicit("Very long running test")]
        public void SqlLobbyMaxPlayersWithFilterJoinTimeout()
        {
            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;

            UnifiedTestClient gameClient1 = null;

            const string lobbyName = "SqlLobbyMaxPlayers";
            const byte lobbyType = 2;

            try
            {
                var roomName = this.GenerateRandomizedRoomName("SqlLobbyMaxPlayers_");
                var gameProperties = new Hashtable();
                gameProperties["C0"] = 10;
                gameProperties["C5"] = "Name";

                gameClient1 = this.CreateGameOnGameServer(null, roomName, lobbyName, lobbyType, true, true, 2, gameProperties, null);

                var joinRequest = new JoinRandomGameRequest {QueryData = "C0=10"};

                // join first client
                masterClient1 = this.CreateMasterClientAndAuthenticate("Tester1");
                masterClient1.JoinLobby(lobbyName, lobbyType);
                masterClient1.JoinRandomGame(joinRequest, (short) Photon.Common.ErrorCode.Ok);

                // join second client
                // should fail because first client is still connecting to the game server
                masterClient2 = this.CreateMasterClientAndAuthenticate("Tester2");
                masterClient2.JoinLobby(lobbyName, lobbyType);
                masterClient2.JoinRandomGame(joinRequest, (short) Photon.Common.ErrorCode.NoMatchFound);
                masterClient2.Dispose();

                // wait for join timeout (default is currently 15 seconds)
                Thread.Sleep(30000);

                // join second client
                // should work because first client has timed out connecting to the game server
                masterClient2 = this.CreateMasterClientAndAuthenticate("Tester2");
                masterClient2.JoinLobby(lobbyName, lobbyType);
                masterClient2.JoinRandomGame(joinRequest, (short) Photon.Common.ErrorCode.Ok);
                masterClient2.Dispose();
            }
            finally
            {
                DisposeClients(masterClient1, masterClient2, gameClient1);
            }
        }

        [Test]
        public void SqlLobbyMultiQuery()
        {
            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;

            try
            {
                const string lobbyName = "sqlMultiQuery";

                var gameProperties = new Hashtable
                {
                    {"C0", 10}
                };

                var roomName = MethodBase.GetCurrentMethod().Name;
                masterClient2 = this.CreateGameOnGameServer("GameClient", roomName, lobbyName, (byte)AppLobbyType.SqlLobby, true, true, 0, gameProperties, null);

                Thread.Sleep(10);

                masterClient1 = this.CreateMasterClientAndAuthenticate("Tester");

                masterClient1.JoinRandomGame(null, "C0 < 5", ErrorCode.NoRandomMatchFound, lobbyName, (byte)AppLobbyType.SqlLobby);
                //tests to many queries
                masterClient1.JoinRandomGame(null, "C0 < 5;C0 < 10;C0 < 20;C0 < 50", ErrorCode.InvalidOperation, lobbyName, (byte)AppLobbyType.SqlLobby);
                //tests removing semicolon at end
                masterClient1.JoinRandomGame(null, "C0 < 5;C0 < 10;", ErrorCode.NoRandomMatchFound, lobbyName, (byte)AppLobbyType.SqlLobby);
                //tests matchmakingType JoinRandomOnSqlNoMatch (3) - removed this functionality
//                masterClient1.JoinRandomGame(null, "C0 < 5;C0 < 10", ErrorCode.Ok, 3, null,  lobbyName, (byte)AppLobbyType.SqlLobby);
                //match with second query
                masterClient1.JoinRandomGame(null, "C0 <= 5;C0 <= 10;C0 <= 20;", ErrorCode.Ok, lobbyName, (byte)AppLobbyType.SqlLobby);
                //match with third query
                masterClient1.JoinRandomGame(null, "C0 < 5;C0 < 10;C0 < 20", ErrorCode.Ok, lobbyName, (byte)AppLobbyType.SqlLobby);
            }
            finally
            {
                DisposeClients(masterClient1, masterClient2);
            }
        }

        //TODO - make this test lobby independent? (default + sql lobby?)
        [Test]
        public void JoinRandomCreateIfNotExist()
        {
            UnifiedTestClient masterClient1 = null;

            try
            {
                const string lobbyName = "joinRandomCreateIfNotExist";

                var gameProperties = new Hashtable
                {
                    {"C0", 10},
                    {(byte)255, (byte)5 } //max players
                };

                var roomName = MethodBase.GetCurrentMethod().Name;


                masterClient1 = this.CreateMasterClientAndAuthenticate("Client1");

                var additionalParameters = new Dictionary<byte, object>
                {
                    { 215, (byte)1},    //joinMode createIfNotExist
//                    { 255, roomName }   //gameId
                };

                //random room name
                masterClient1.JoinRandomGame(gameProperties, "C0 < 5", ErrorCode.Ok, 255, additionalParameters, lobbyName, (byte)AppLobbyType.SqlLobby);

                DisposeClients(masterClient1);
                masterClient1 = this.CreateMasterClientAndAuthenticate("Client1");

                Thread.Sleep(10);

                //room name set
                additionalParameters[255] = roomName;
                masterClient1.JoinRandomGame(gameProperties, "C0 < 5", ErrorCode.Ok, 255, additionalParameters, lobbyName, (byte)AppLobbyType.SqlLobby, new [] {roomName});

                Thread.Sleep(10);

                DisposeClients(masterClient1);
                masterClient1 = this.CreateMasterClientAndAuthenticate("Client1");

                additionalParameters[255] = roomName + "2";
                //GameProperties for game creation - set MaxPlayers to 2 from 5 - created game uses these values instead of JoinRandom GameProperties (parameter 255)
                additionalParameters[251] = new Hashtable{{(byte)255, (byte)2}};
                masterClient1.JoinRandomGame(gameProperties, "C0 < 5", ErrorCode.Ok, 255, additionalParameters, lobbyName, (byte)AppLobbyType.SqlLobby, new [] { roomName + "2" });


                //TODO more tests?
//                Thread.Sleep(10);
//               //all possible parameters
//                additionalParameters.Add(249, new Hashtable()); //ActorProperties
//                additionalParameters.Add(250, true);            //BroadcastActorProperties
//                additionalParameters.Add(241, true);            //DeleteCacheOnLeave
//                additionalParameters.Add(237, false);           //SuppressRoomEvents
//                additionalParameters.Add(254, 0);               //ActorNr
//                additionalParameters.Add(236, 30);               //EmptyRoomLiveTime
//                additionalParameters.Add(235, 30);              //PlayerTTL
//                additionalParameters.Add(232, false);           //CheckUserOnJoin
//                additionalParameters.Add(205, 0);               //CacheSliceIndex
//                additionalParameters.Add(204, new [] {"TestPlugin"});           //Plugins
//                additionalParameters.Add(234, (byte)0);         //WebFlags
//                additionalParameters.Add(239, true);            //PublishUserId
//                additionalParameters.Add(229, false);           //ForceRejoin
//                additionalParameters.Add(191, 0);               //RoomOptionFlags
//
//                masterClient1.JoinRandomGame(gameProperties, "C0 < 5", ErrorCode.GameIdAlreadyExists, 255, additionalParameters, lobbyName, (byte)AppLobbyType.SqlLobby);
            }
            finally
            {
                DisposeClients(masterClient1);
            }
        }

        //tests GetGameList operation
        [Test]
        public void SqlLobbyGetGameList()
        {
            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;
            UnifiedTestClient masterClient3 = null;
            UnifiedTestClient masterClient4 = null;

            var lobbyName = "sql1";
            var query = "C0 = 0 AND C1 > 50";

            try
            {
                masterClient1 = this.CreateMasterClientAndAuthenticate(Player1);

                if (string.IsNullOrEmpty(this.Player1) && masterClient1.Token == null)
                {
                    Assert.Ignore("This test does not work correctly for old clients without userId and token");
                }

                //no game found
                var response = masterClient1.GetGameList(query, lobbyName, (byte)AppLobbyType.SqlLobby);
                var gameList = (Hashtable)response.Parameters[ParameterCode.GameList];
                this.CheckGameListCount(0, gameList);
                //invalid query - filter - no error because 0 games > no query done
                //masterClient1.GetGameList(string.Empty, lobbyName, (byte)AppLobbyType.SqlListLobby, ErrorCode.InvalidOperation);
                //invalid query - lobby
                masterClient1.GetGameList(string.Empty, lobbyName, (byte)AppLobbyType.Default, ErrorCode.InvalidOperation);
                masterClient1.GetGameList(string.Empty, lobbyName, (byte)AppLobbyType.ChannelLobby, ErrorCode.InvalidOperation);
                masterClient1.GetGameList(string.Empty, lobbyName, (byte)AppLobbyType.AsyncRandomLobby, ErrorCode.InvalidOperation);

                //create a game
                string roomName = "SqlLobby_1_" + Guid.NewGuid().ToString().Substring(0, 6);
                this.CreateGameOnGameServer(Player1, roomName, lobbyName, (byte)AppLobbyType.SqlLobby, true, true, 0, new Hashtable { { "C0", 0 }, {"C1", 100} }, new[] { "C0", "C1" });

                //second client
                masterClient2 = this.CreateMasterClientAndAuthenticate(Player2);

                //invalid query - filter
                masterClient2.GetGameList(string.Empty, lobbyName, (byte)AppLobbyType.SqlLobby, ErrorCode.InvalidOperation);
                //one game found
                response = masterClient2.GetGameList(query, lobbyName, (byte)AppLobbyType.SqlLobby);
                gameList = (Hashtable)response.Parameters[ParameterCode.GameList];
                this.CheckGameListCount(1, gameList);

                //no game found
                response = masterClient2.GetGameList("C0=1", lobbyName, (byte)AppLobbyType.SqlLobby);
                gameList = (Hashtable)response.Parameters[ParameterCode.GameList];
                this.CheckGameListCount(0, gameList);

                //create a game
                roomName = "SqlLobby_2_" + Guid.NewGuid().ToString().Substring(0, 6);
                this.CreateGameOnGameServer(Player2, roomName, lobbyName, (byte)AppLobbyType.SqlLobby, true, true, 0, new Hashtable { { "C0", 0 }, { "C1", 100 } }, new[] { "C0", "C1" });

                masterClient3 = this.CreateMasterClientAndAuthenticate(Player3);

                //two games found
                response = masterClient3.GetGameList(query, lobbyName, (byte)AppLobbyType.SqlLobby);
                gameList = (Hashtable)response.Parameters[ParameterCode.GameList];
                this.CheckGameListCount(2, gameList);

                //no game found
                response = masterClient3.GetGameList("C0=1", lobbyName, (byte)AppLobbyType.SqlLobby);
                gameList = (Hashtable)response.Parameters[ParameterCode.GameList];
                this.CheckGameListCount(0, gameList);

                //create a game
                roomName = "SqlLobby_3_" + Guid.NewGuid().ToString().Substring(0, 6);
                this.CreateGameOnGameServer(Player2, roomName, lobbyName, (byte)AppLobbyType.SqlLobby, true, true, 0, new Hashtable { { "C0", 1 } }, new[] { "C0" });

                masterClient4 = this.CreateMasterClientAndAuthenticate("Player4");

                //two games found
                response = masterClient4.GetGameList(query, lobbyName, (byte)AppLobbyType.SqlLobby);
                gameList = (Hashtable)response.Parameters[ParameterCode.GameList];
                this.CheckGameListCount(2, gameList);

                //one game found
                response = masterClient4.GetGameList("C0=1", lobbyName, (byte)AppLobbyType.SqlLobby);
                gameList = (Hashtable)response.Parameters[ParameterCode.GameList];
                this.CheckGameListCount(1, gameList);
                //no game found
                response = masterClient4.GetGameList("C0=2", lobbyName, (byte)AppLobbyType.SqlLobby);
                gameList = (Hashtable)response.Parameters[ParameterCode.GameList];
                this.CheckGameListCount(0, gameList);
            }
            finally
            {
                DisposeClients(masterClient1, masterClient2, masterClient3, masterClient4);
            }
        }

        //tests query data checks with GetGameList
        [Test]
        //set OnlyLogQueryDataErrors to false
        [Explicit("requires OnlyLogQueryDataErrors set to false, default is true")]
        public void SqlLobbyGetGameList2()
        {
            UnifiedTestClient masterClient1 = null;

            var lobbyName = "sql2";

            try
            {
                masterClient1 = this.CreateMasterClientAndAuthenticate(Player1);

                if (string.IsNullOrEmpty(this.Player1) && masterClient1.Token == null)
                {
                    Assert.Ignore("This test does not work correctly for old clients without userId and token");
                }

                masterClient1.GetGameList("C0=2", lobbyName, (byte)AppLobbyType.SqlLobby, ErrorCode.Ok);

                masterClient1.GetGameList("C0=2;", lobbyName, (byte)AppLobbyType.SqlLobby, ErrorCode.InvalidOperation);
                var wrongWords = "ALTER;CREATE;DELETE;DROP;EXEC;EXECUTE;INSERT;INSERT INTO;MERGE;SELECT;UPDATE;UNION;UNION ALL".Split(';');
                foreach (var wrongWord in wrongWords)
                {
                    masterClient1.GetGameList(wrongWord + " TABLE Game", lobbyName, (byte)AppLobbyType.SqlLobby, ErrorCode.InvalidOperation);
                }
            }
            finally
            {
                DisposeClients(masterClient1);
            }
        }

        //tests result limit settings
        [Test]
        //set LimitSqlFilterResults to 5
        [Explicit("requires LimitSqlFilterResults set to 5, default is 100")]
        public void SqlLobbyGetGameList3()
        {
            UnifiedTestClient masterClient1 = null;

            var lobbyName = "sql3";

            try
            {
                masterClient1 = this.CreateMasterClientAndAuthenticate(Player1);

                if (string.IsNullOrEmpty(this.Player1) && masterClient1.Token == null)
                {
                    Assert.Ignore("This test does not work correctly for old clients without userId and token");
                }

                for (int i = 0; i < 6; i++)
                {
                    var roomName = string.Format("{0}_{1}", lobbyName, i);
                    this.CreateGameOnGameServer("Player"+i, roomName, lobbyName, (byte)AppLobbyType.SqlLobby, true, true, 0, new Hashtable { { "C0", 1 } }, new[] { "C0" });
                }

                //only 5 games found if LimitSqlFilterResults was set to 5
                var response = masterClient1.GetGameList("C0=1", lobbyName, (byte)AppLobbyType.SqlLobby);
                var gameList = (Hashtable)response.Parameters[ParameterCode.GameList];
                this.CheckGameListCount(5, gameList);
            }
            finally
            {
                DisposeClients(masterClient1);
            }
        }

        [Test]
        [Explicit("Requires StoredProcedures.config and anonymous access disabled")]
        //PSCS-3340
        //Test with VAOnlineTests "LOCAL_STEFAN"
        //app ids:
        //775e982a-6079-4c7e-9d6b-77c40dedb309 (dev)
        //f3e1f24c-7c77-4a38-bc2c-9573fc168962 (live)
        //use custom auth url https://wt-e4c18d407aa73a40e4182aaf00a2a2eb-0.sandbox.auth0-extend.com/custom_auth_ttales (webtask.io)
        /* Expected config (set in dashboard for apps, only for private clouds available):
            C0;C1;C2;C3;C4
            SpFile0:C0 < 10;C0 < 30
            SpFile1:C0 < 10;C0 < 30;
            SpPlaceholder:C0 < [C0];C1 = '[C1]';C0 > [C0]
            SpPlaceholderNotFound:C0 < [AcKey1];C1 = '[AcKey2]';C2 > [AcKey3]
            SpPlaceholderCaseSensitive:C0 < [c0];C1 = '[c1]';C0 > [c0]
            SpNameCaseSensitive:C0 < 10;C0 < 30
        */
        public void SqlLobbyStoredProceduresAndPlaceholder()
        {
            UnifiedTestClient masterClient = null;
            UnifiedTestClient[] gameClients = null;

            try
            {
                const string lobbyName = "SqlLobby1";
                const byte lobbyType = 2;

                gameClients = new UnifiedTestClient[3];

                for (int i = 0; i < gameClients.Length; i++)
                {
                    var gameProperties = new Hashtable();
                    switch (i)
                    {
                        case 1:
                            gameProperties.Add("C0", 10);
                            break;

                        case 2:
                            gameProperties.Add("C1", "AcVal");
                            break;
                    }

                    var roomName = "SqlLobbyMatchmaking" + i;
                    gameClients[i] = this.CreateGameOnGameServer("GameClient" + i, roomName, lobbyName, lobbyType, true, true, 0, gameProperties,
                        null);
                }

                masterClient = this.CreateMasterClientAndAuthenticate("Tester");

                masterClient.JoinRandomGame(null, "$SP.SpFile0", ErrorCode.Ok, lobbyName, lobbyType);
                masterClient.JoinRandomGame(null, "$SP.SpFile1", ErrorCode.Ok, lobbyName, lobbyType);
                masterClient.JoinRandomGame(null, "$SP.SpNotFound", ErrorCode.InvalidOperation, lobbyName, lobbyType);
                masterClient.JoinRandomGame(null, "$SP.SpPlaceholder", ErrorCode.Ok, lobbyName, lobbyType);
                masterClient.JoinRandomGame(null, "$SP.SpPlaceholderNotFound", ErrorCode.InvalidOperation, lobbyName, lobbyType);
                masterClient.JoinRandomGame(null, "$SP.SpPlaceholderCaseSensitive", ErrorCode.Ok, lobbyName, lobbyType);
                masterClient.JoinRandomGame(null, "$SP.SpNamecasesensitive", ErrorCode.Ok, lobbyName, lobbyType);

                //to test update, set PhotonCloud.Authentication.Settings - AuthCacheUpdateInterval to 10 or less (test doesn't fail if cache wasn't updated. this is just to check the log if an update happened) 
//                Thread.Sleep(60000);
            }
            finally
            {
                DisposeClients(masterClient);
                DisposeClients(gameClients);
            }
        }

        //current implementation requires MasterServerSettings.UseLegacyLobbies set to false
        [Ignore("Temporary test, new lobby  behaviour is not final")]
        [Test]
        public void SqlLobbyNoGameListEvents()
        {
            // previous tests could just have leaved games on the game server
            // so there might be AppStats or GameListUpdate event in schedule.
            // Just wait a second so this events can be published before starting the test
            Thread.Sleep(1100);

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;

            try
            {
                masterClient1 = this.CreateMasterClientAndAuthenticate(Player1);

                if (string.IsNullOrEmpty(this.Player1) && masterClient1.Token == null)
                {
                    Assert.Ignore("This test does not work correctly for old clients without userId and token");
                }

                Assert.IsTrue(masterClient1.OpJoinLobby("sql", AppLobbyType.SqlLobby));
                //no gamelist event
                try
                {
                    var ev = masterClient1.WaitForEvent(EventCode.GameList, 1000 + ConnectPolicy.WaitTime);
                    Assert.AreEqual(EventCode.GameList, ev.Code);
                    var gameList = (Hashtable)ev.Parameters[ParameterCode.GameList];
                    this.CheckGameListCount(0, gameList);

                    Assert.Fail("Got game list event");
                }
                catch (TimeoutException)
                {

                }


                masterClient2 = this.CreateMasterClientAndAuthenticate(Player2);

                Assert.IsTrue(masterClient2.OpJoinLobby("sql2", AppLobbyType.SqlLobby));
                //no gamelist event
                try
                {
                    var ev = masterClient2.WaitForEvent(EventCode.GameList);
                    Assert.AreEqual(EventCode.GameList, ev.Code);
                    var gameList = (Hashtable)ev.Parameters[ParameterCode.GameList];
                    this.CheckGameListCount(0, gameList);

                    Assert.Fail("Got game list event");
                }
                catch (TimeoutException)
                {

                }


                // join lobby again: 
                masterClient1.OperationResponseQueueClear();
                Assert.IsTrue(masterClient1.OpJoinLobby("sql3", AppLobbyType.SqlLobby));

                // wait for old app stats event
                //                masterClient2.CheckThereIsEvent(EventCode.AppStats, 10000);

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
                        var ev = masterClient2.WaitForEvent(1000);

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

                Assert.IsFalse(gameListUpdateReceived, "GameListUpdate event received");
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
                        var ev = masterClient2.WaitForEvent(1000);

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

                Assert.IsFalse(gameListUpdateReceived, "GameListUpdate event received");
                Assert.IsTrue(appStatsReceived, "AppStats event received");

                // leave lobby
                masterClient2.OpLeaveLobby();

                gameListUpdateReceived = false;
                appStatsReceived = false;

                masterClient1 = this.CreateMasterClientAndAuthenticate(Player1);

                roomName = this.GenerateRandomizedRoomName("LobbyGamelistEvents_2_");

                this.CreateRoomOnGameServer(masterClient1, roomName);

                timeout = Environment.TickCount + 10000;

                while (Environment.TickCount < timeout && (!gameListUpdateReceived || !appStatsReceived))
                {
                    try
                    {
                        var ev = masterClient2.WaitForEvent(1000);

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

        //tests new default lobby behaviour (LimitedGameList)
        //set LimitGameList and LimitGameListUpdate to 5
        //current implementation requires MasterServerSettings.UseLegacyLobbies set to false, LimitGameList=5 and LimitGameListUpdate=1
        [Ignore("Temporary test, new lobby  behaviour is not final")]
        [Test]
        public void LobbyLimitedGameListEvents()
        {
            // previous tests could just have leaved games on the game server
            // so there might be AppStats or GameListUpdate event in schedule.
            // Just wait a second so this events can be published before starting the test
            Thread.Sleep(1100);

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;

            try
            {
                masterClient1 = this.CreateMasterClientAndAuthenticate(Player1);

                if (string.IsNullOrEmpty(this.Player1) && masterClient1.Token == null)
                {
                    Assert.Ignore("This test does not work correctly for old clients without userId and token");
                }

                Assert.IsTrue(masterClient1.OpJoinLobby());
                var ev = masterClient1.WaitForEvent(EventCode.GameList, 1000 + ConnectPolicy.WaitTime);
                Assert.AreEqual(EventCode.GameList, ev.Code);
                var gameList = (Hashtable)ev.Parameters[ParameterCode.GameList];
                this.CheckGameListCount(0, gameList);

                //closed game
                var client11 = CreateGameOnGameServer("Player11", "game1", string.Empty, 0, true, false, 4, null, new [] {"asdf"});
                var client12 = CreateGameOnGameServer("Player12", "game2", string.Empty, 0, true, false, 4, null, new[] { "asdf" });
                //full game
                var client13 = CreateGameOnGameServer("Player13", "game3", string.Empty, 0, true, true, 1, null, new[] { "asdf" });
                var client14 = CreateGameOnGameServer("Player14", "game4", string.Empty, 0, true, true, 1, null, new[] { "asdf" });
                //open game
                var client15 = CreateGameOnGameServer("Player15", "game5", string.Empty, 0, true, true, 4, null, new[] { "asdf" });
                var client16 = CreateGameOnGameServer("Player16", "game6", string.Empty, 0, true, true, 4, null, new[] { "asdf" });
                var client17 = CreateGameOnGameServer("Player17", "game7", string.Empty, 0, true, true, 4, null, new[] { "asdf" });
                var client18 = CreateGameOnGameServer("Player18", "game8", string.Empty, 0, true, true, 4, null, new[] { "asdf" });

                masterClient2 = this.CreateMasterClientAndAuthenticate(Player2);
                Assert.IsTrue(masterClient2.OpJoinLobby());
                ev = masterClient2.WaitForEvent(EventCode.GameList);

                Assert.AreEqual(EventCode.GameList, ev.Code);
                gameList = (Hashtable)ev.Parameters[ParameterCode.GameList];

                foreach (DictionaryEntry entry in gameList)
                {
                    var game = (Hashtable)entry.Value;
                    Console.WriteLine("{0}, open {1}, full {2} ({3}/{4})",
                        entry.Key,
                        (bool)game[(byte)GameParameter.IsOpen],
                        (byte)game[(byte)GameParameter.MaxPlayers] == (byte)game[(byte)GameParameter.PlayerCount],
                        game[(byte)GameParameter.PlayerCount],
                        game[(byte)GameParameter.MaxPlayers]);
                }

                this.CheckGameListCount(5, gameList);

                masterClient2.EventQueueClear();

                client11.OpSetPropertiesOfRoom(new Hashtable { { "asdf", "qwer" } });
                client12.OpSetPropertiesOfRoom(new Hashtable { { "asdf", "qwer" } });
                client13.OpSetPropertiesOfRoom(new Hashtable { { "asdf", "qwer" } });
                client14.OpSetPropertiesOfRoom(new Hashtable { { "asdf", "qwer" } });
                client15.OpSetPropertiesOfRoom(new Hashtable { { "asdf", "qwer" } });
                client16.OpSetPropertiesOfRoom(new Hashtable { { "asdf", "qwer" } });
                client17.OpSetPropertiesOfRoom(new Hashtable { { "asdf", "qwer" } });
                client18.OpSetPropertiesOfRoom(new Hashtable { { "asdf", "qwer" } });


                for (int i = 0; i < 8; i++)
                {
                    client11.OpSetPropertiesOfRoom(new Hashtable { { "asdf", i } });

                    var gameListUpdateReceived = WaitForGameListUpdateEvent(masterClient2);
                    Assert.IsTrue(gameListUpdateReceived, "GameListUpdate event received " + i);
                }
            }
            finally
            {
                DisposeClients(masterClient1, masterClient2);
            }
        }

        [Test]
        public void LobbyStatistics()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;

            try
            {
                var authParameter = new Dictionary<byte, object> {{(byte) Operations.ParameterCode.LobbyStats, true}};

                // authenticate client and check if the Lobbystats event will be received
                // Remarks: The event cannot be checked for a specific lobby count because
                // previous tests may have created lobbies. 
                client1 = this.CreateMasterClientAndAuthenticate(Player1, authParameter);

                if (string.IsNullOrEmpty(this.Player1) || client1.Token == null)
                {
                    Assert.Ignore("This test does not work correctly for old clients without userId and token");
                }

                if (this.AuthPolicy == AuthPolicy.UseAuthOnce)// in this case we should send OpSettings to master
                {
                    client1.SendRequest(new OperationRequest
                    {
                        OperationCode = OperationCode.ServerSettings,
                        Parameters = new Dictionary<byte, object> { { SettingsRequestParameters.LobbyStats, true } },
                    });
                }

                var lobbyStatsEvent = client1.WaitForEvent((byte) Events.EventCode.LobbyStats);
                Assert.AreEqual((byte) Events.EventCode.LobbyStats, lobbyStatsEvent.Code);

                // Join to a new lobby and check if the new lobby will listet
                // for new clients
                var lobbyName = this.GenerateRandomizedRoomName("LobbyStatisticTest");
                const byte lobbyType = 2;
                client1.JoinLobby(lobbyName, lobbyType);

                client2 = this.CreateMasterClientAndAuthenticate(Player2, authParameter);
                if (this.AuthPolicy == AuthPolicy.UseAuthOnce)// in this case we should send OpSettings to master
                {
                    client2.SendRequest(new OperationRequest
                    {
                        OperationCode = OperationCode.ServerSettings,
                        Parameters = new Dictionary<byte, object> { { SettingsRequestParameters.LobbyStats, true } },
                    });
                }
                lobbyStatsEvent = client2.WaitForEvent((byte) Events.EventCode.LobbyStats);
                Assert.AreEqual((byte) Events.EventCode.LobbyStats, lobbyStatsEvent.Code);

                object temp;
                lobbyStatsEvent.Parameters.TryGetValue((byte) Operations.ParameterCode.LobbyName, out temp);
                var lobbyNames = GetParameter<string[]>(lobbyStatsEvent.Parameters, (byte) Operations.ParameterCode.LobbyName, "LobbyNames");
                var lobbyTypes = GetParameter<byte[]>(lobbyStatsEvent.Parameters, (byte) Operations.ParameterCode.LobbyType, "LobbyTypes");
                var peerCounts = GetParameter<int[]>(lobbyStatsEvent.Parameters, (byte) Operations.ParameterCode.PeerCount, "PeerCount");
                var gameCounts = GetParameter<int[]>(lobbyStatsEvent.Parameters, (byte) Operations.ParameterCode.GameCount, "GameCount");

                Assert.AreEqual(lobbyNames.Length, lobbyTypes.Length, "LobbyType count differs from LobbyName count");
                Assert.AreEqual(lobbyNames.Length, peerCounts.Length, "PeerCount count differs from LobbyName count");
                Assert.AreEqual(lobbyNames.Length, gameCounts.Length, "GameCount count differs from LobbyName count");

                var lobbyIndex = Array.IndexOf(lobbyNames, lobbyName);
                Assert.GreaterOrEqual(lobbyIndex, 0, "Lobby not found in statistics");
                Assert.AreEqual(lobbyType, lobbyTypes[lobbyIndex], "Wrong lobby type");
                Assert.AreEqual(1, peerCounts[lobbyIndex], "Wrong peer count");
                Assert.AreEqual(0, gameCounts[lobbyIndex], "Wrong game count");

                client2.Dispose();
                client2 = null;

                // create a new game for the lobby
                var gameName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);
                var createGameResponse = client1.CreateGame(gameName);
                this.ConnectAndAuthenticate(client1, createGameResponse.Address, client1.UserId);
                client1.CreateGame(gameName);

                // give the game server some time to report the game to the master server
                Thread.Sleep(100);

                // check if new game is listed in lobby statistics
                client2 = this.CreateMasterClientAndAuthenticate(Player2, authParameter);
                if (this.AuthPolicy == AuthPolicy.UseAuthOnce)// in this case we should send OpSettings to master
                {
                    client2.SendRequest(new OperationRequest
                    {
                        OperationCode = OperationCode.ServerSettings,
                        Parameters = new Dictionary<byte, object> { { SettingsRequestParameters.LobbyStats, true } },
                    });
                }
                lobbyStatsEvent = client2.WaitForEvent((byte) Events.EventCode.LobbyStats);
                Assert.AreEqual((byte) Events.EventCode.LobbyStats, lobbyStatsEvent.Code);

                lobbyStatsEvent.Parameters.TryGetValue((byte) Operations.ParameterCode.LobbyName, out temp);
                lobbyNames = GetParameter<string[]>(lobbyStatsEvent.Parameters, (byte) Operations.ParameterCode.LobbyName, "LobbyNames");
                lobbyTypes = GetParameter<byte[]>(lobbyStatsEvent.Parameters, (byte) Operations.ParameterCode.LobbyType, "LobbyTypes");
                peerCounts = GetParameter<int[]>(lobbyStatsEvent.Parameters, (byte) Operations.ParameterCode.PeerCount, "PeerCount");
                gameCounts = GetParameter<int[]>(lobbyStatsEvent.Parameters, (byte) Operations.ParameterCode.GameCount, "GameCount");

                Assert.AreEqual(lobbyNames.Length, lobbyTypes.Length, "LobbyType count differs from LobbyName count");
                Assert.AreEqual(lobbyNames.Length, peerCounts.Length, "PeerCount count differs from LobbyName count");
                Assert.AreEqual(lobbyNames.Length, gameCounts.Length, "GameCount count differs from LobbyName count");

                lobbyIndex = Array.IndexOf(lobbyNames, lobbyName);
                Assert.GreaterOrEqual(lobbyIndex, 0, "Lobby not found in statistics");
                Assert.AreEqual(lobbyType, lobbyTypes[lobbyIndex], "Wrong lobby type");
                Assert.AreEqual(1, peerCounts[lobbyIndex], "Wrong peer count");
                Assert.AreEqual(1, gameCounts[lobbyIndex], "Wrong game count");
            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [Test]
        public void LobbyCreateTest()
        {
            if (this.IsOnline)
            {
                Assert.Ignore("Test applicable to offline version of tests");
            }
            UnifiedTestClient client = null;
            const string LobbyName1 = "lobby1";
            const string LobbyName2 = "lobby2";
            const string LobbyName3 = "looby3";
            const string LobbyName4 = "looby4";

            const string GameName = "LobbyCreateTest";
            try
            {
                client = this.CreateMasterClientAndAuthenticate(this.Player1);

                var lobResponse = client.GetLobbyStats(null, null);
                var lobbiesCount = lobResponse.LobbyNames.Length;

                client.JoinLobby(LobbyName1);
                lobResponse = client.GetLobbyStats(null, null);
                Assert.AreEqual(++lobbiesCount, lobResponse.LobbyNames.Length);
                Assert.Contains(LobbyName1, lobResponse.LobbyNames);
                Assert.AreEqual(0, lobResponse.LobbyTypes[lobbiesCount - 1]);

                client.JoinLobby(LobbyName1, 0);
                lobResponse = client.GetLobbyStats(null, null);
                Assert.AreEqual(lobbiesCount, lobResponse.LobbyNames.Length);
                Assert.Contains(LobbyName1, lobResponse.LobbyNames);
                Assert.AreEqual(0, lobResponse.LobbyTypes[lobbiesCount - 1]);

                client.JoinLobby(LobbyName2, 1);
                lobResponse = client.GetLobbyStats(null, null);
                Assert.AreEqual(++lobbiesCount, lobResponse.LobbyNames.Length);
                Assert.Contains(LobbyName2, lobResponse.LobbyNames);
                Assert.AreEqual(1, lobResponse.LobbyTypes[lobResponse.LobbyTypes.Length - 1]);

                var createGame = new CreateGameRequest
                {
                    GameId = GameName,
                    LobbyName = LobbyName3,
                };

                client.CreateGame(createGame);

                lobResponse = client.GetLobbyStats(null, null);
                Assert.AreEqual(++lobbiesCount, lobResponse.LobbyNames.Length);
                Assert.Contains(LobbyName3, lobResponse.LobbyNames);
                Assert.AreEqual(0, lobResponse.LobbyTypes[lobResponse.LobbyTypes.Length - 1]);

                createGame = new CreateGameRequest
                {
                    GameId = GameName,
                    LobbyName = LobbyName4,
                };

                client.CreateGame(createGame, ErrorCode.GameIdAlreadyExists);

                lobResponse = client.GetLobbyStats(null, null);
                Assert.AreEqual(++lobbiesCount, lobResponse.LobbyNames.Length);
            }
            finally
            {
                DisposeClients(client);
            }
        }

        [Test]
        public void LobbyStatsForRestoredGames([Values(LobbyType.SqlLobby, LobbyType.Default, LobbyType.AsyncRandomLobby)]LobbyType lobbyType)
        {
            if (!this.UsePlugins)
            {
                Assert.Ignore("This test needs plugins");
            }

            if (!this.IsOffline)
            {
                Assert.Ignore("this test works only in offline mode");
            }

            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;
            UnifiedTestClient client3 = null;
            UnifiedTestClient client4 = null;

            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("this tests requires userId to be set");
            }

            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                client3 = this.CreateMasterClientAndAuthenticate(this.Player3);
                client4 = this.CreateMasterClientAndAuthenticate(this.Player2);

                var lobbyName = GenerateRandomizedRoomName("lobby");
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);

                var createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    SuppressRoomEvents = true,
                    AddUsers = new[] { this.Player1, this.Player2 },
                    Plugins = new[] { "SaveLoadStateTestPlugin" },
                    PlayerTTL = -1,
                    CheckUserOnJoin = true,
                    LobbyType = (byte)lobbyType,
                    LobbyName = lobbyName,
                    GameProperties = new Hashtable
                    {
                        { (byte)GameParameter.MaxPlayers, 2},
                        { (byte) GameParameter.IsVisible, false }
                    }
                };

                var createGameResponse = client1.CreateGame(createGameRequest);

                this.ConnectAndAuthenticate(client1, createGameResponse.Address);

                client1.CreateGame(createGameRequest);

                Thread.Sleep(300);

                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    LobbyType = (byte)lobbyType,
                    LobbyName = lobbyName,
                };
                var joinGameResponse = client2.JoinGame(joinRequest);
                this.ConnectAndAuthenticate(client2, joinGameResponse.Address);
                client2.JoinGame(joinRequest, ErrorCode.Ok);

                this.UpdateTokensGSAndGame(client4, "localhost", roomName);
                this.ConnectAndAuthenticate(client4, joinGameResponse.Address);
                client4.JoinGame(joinRequest, ErrorCode.JoinFailedFoundActiveJoiner);

                Thread.Sleep(100);

                client1.LeaveGame(true);
                client2.LeaveGame(true);

                this.ConnectAndAuthenticate(client1, this.MasterAddress);
                this.ConnectAndAuthenticate(client2, this.MasterAddress);

                joinRequest.JoinMode = JoinModes.RejoinOrJoin;
                joinRequest.Plugins = new[] { "SaveLoadStateTestPlugin" };

                joinGameResponse = client1.JoinGame(joinRequest);
                this.ConnectAndAuthenticate(client1, joinGameResponse.Address);
                client1.JoinGame(joinRequest);

                Thread.Sleep(300);

                var lobbyStatsResponse = client3.GetLobbyStats(new[] { lobbyName }, new[] { (byte)lobbyType });
                Assert.That(lobbyStatsResponse.PeerCount[0], Is.EqualTo(2));
                Assert.That(lobbyStatsResponse.GameCount[0], Is.EqualTo(1));
                lobbyStatsResponse = client3.GetLobbyStats(null, null);
            }
            finally
            {
                DisposeClients(client1, client2, client3);
            }
        }

        [Test]
        public void LobbyStatsTryRestoreFullGame([Values(LobbyType.SqlLobby, LobbyType.Default, LobbyType.AsyncRandomLobby)]LobbyType lobbyType)
        {
            if (!this.UsePlugins)
            {
                Assert.Ignore("This test needs plugins");
            }

            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("this tests requires userId to be set");
            }

            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;
            UnifiedTestClient client3 = null;
            UnifiedTestClient client4 = null;

            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                client3 = this.CreateMasterClientAndAuthenticate(this.Player3);
                client4 = this.CreateMasterClientAndAuthenticate("Player4");

                var lobbyName = GenerateRandomizedRoomName("lobby");
                var roomName = GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);

                var createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    SuppressRoomEvents = true,
                    AddUsers = new[] { this.Player1, this.Player2 },
                    Plugins = new[] { "SaveLoadStateTestPlugin" },
                    PlayerTTL = -1,
                    CheckUserOnJoin = true,
                    LobbyType = (byte)lobbyType,
                    LobbyName = lobbyName,
                    GameProperties = new Hashtable
                    {
                        { (byte)GameParameter.MaxPlayers, 2},
                        { (byte) GameParameter.IsVisible, false }
                    }
                };

                var createGameResponse = client1.CreateGame(createGameRequest);

                this.ConnectAndAuthenticate(client1, createGameResponse.Address);

                client1.CreateGame(createGameRequest);

                Thread.Sleep(300);

                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    LobbyType = (byte)lobbyType,
                    LobbyName = lobbyName,
                };
                var joinGameResponse = client2.JoinGame(joinRequest);
                this.ConnectAndAuthenticate(client2, joinGameResponse.Address);
                client2.JoinGame(joinRequest, ErrorCode.Ok);

                Thread.Sleep(100);

                client1.LeaveGame(true);
                client2.LeaveGame(true);

                this.ConnectAndAuthenticate(client1, this.MasterAddress);
                this.ConnectAndAuthenticate(client2, this.MasterAddress);

                joinRequest.JoinMode = JoinModes.RejoinOrJoin;
                joinRequest.Plugins = new[] { "SaveLoadStateTestPlugin" };

                joinGameResponse = client4.JoinGame(joinRequest);
                this.ConnectAndAuthenticate(client4, joinGameResponse.Address);
                client4.JoinGame(joinRequest, ErrorCode.GameFull);

                client4.Disconnect();

                Thread.Sleep(400);

                var lobbyStatsResponse = client3.GetLobbyStats(new[] { lobbyName }, new[] { (byte)lobbyType });
                Assert.That(lobbyStatsResponse.PeerCount[0], Is.EqualTo(lobbyType == LobbyType.AsyncRandomLobby ? 2 : 0));
                Assert.That(lobbyStatsResponse.GameCount[0], Is.EqualTo(lobbyType == LobbyType.AsyncRandomLobby ? 1 : 0));
                lobbyStatsResponse = client3.GetLobbyStats(null, null);
            }
            finally
            {
                DisposeClients(client1, client2, client3, client4);
            }
        }

        [Test]
        [Ignore("LobbyStatsPublishInterval property of game server settings must be set to 1 for this test to run")]
        public void LobbyStatisticsPublish()
        {
            UnifiedTestClient client = null;

            try
            {
                var authParameter = new Dictionary<byte, object>();
                authParameter.Add((byte) Operations.ParameterCode.LobbyStats, true);

                client = this.CreateMasterClientAndAuthenticate(null, authParameter);
                client.WaitForEvent((byte) Events.EventCode.LobbyStats);

                int count = 0;
                while (count < 3)
                {
                    client.WaitForEvent((byte) Events.EventCode.LobbyStats, 2000);
                    count++;
                }

            }
            finally
            {
                DisposeClients(client);
            }
        }

        [Test]
        public void LobbyStatisticsRequest()
        {
            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;
            try
            {
                var lobbyName = this.GenerateRandomizedRoomName("LobbyStatisticsRequest1");
                const byte lobbyType = 0;
                var lobbyName2 = this.GenerateRandomizedRoomName("LobbyStatisticsRequest2");
                const byte lobbyType2 = 2;
                const string roomName = "TestRoom";

                // join lobby on master
                masterClient1 = this.CreateMasterClientAndAuthenticate(Player1);

                if (string.IsNullOrEmpty(this.Player1) && masterClient1.Token == null)
                {
                    Assert.Ignore("This test does not work correctly for old clients without userId and token");
                }

                masterClient1.JoinLobby(lobbyName, lobbyType);
                masterClient1.WaitForEvent((byte) Events.EventCode.GameList);

                // get stats for all lobbies
                var response = masterClient1.GetLobbyStats(null, null);
                var expectedLobbyNames = new string[] {lobbyName};
                var expectedLobbyTypes = new byte[] {lobbyType};
                var expectedPeerCount = new int[] {1};
                var expectedGameCount = new int[] {0};
                this.VerifyLobbyStatisticsFullList(response, expectedLobbyNames, expectedLobbyTypes, expectedPeerCount, expectedGameCount);

                // get stats for specific lobbies (the last second should not exists and return 0 for game and peer count)
                var lobbyNames = new string[] {lobbyName, lobbyName, lobbyName2};
                var lobbyTypes = new byte[] {lobbyType, lobbyType2, lobbyType2};

                response = masterClient1.GetLobbyStats(lobbyNames, lobbyTypes);
                expectedPeerCount = new int[] {1, 0, 0};
                expectedGameCount = new int[] {0, 0, 0};
                this.VerifyLobbyStatisticsList(response, expectedPeerCount, expectedGameCount);

                // join lobby on master with second client
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                masterClient2.JoinLobby(lobbyName, lobbyType);
                //masterClient2.WaitForEvent((byte) Events.EventCode.GameList);

                // check if peer count has been updated
                response = masterClient2.GetLobbyStats(lobbyNames, lobbyTypes);
                expectedPeerCount = new int[] {2, 0, 0};
                expectedGameCount = new int[] {0, 0, 0};
                this.VerifyLobbyStatisticsList(response, expectedPeerCount, expectedGameCount);
                masterClient2.Disconnect();

                // join second client to another lobby 
                var masterClient3 = this.CreateMasterClientAndAuthenticate(Player3);
                masterClient3.JoinLobby(lobbyName2, lobbyType2);
                //masterClient3.WaitForEvent((byte) Events.EventCode.GameList);

                // check if peer count has been updated
                response = masterClient3.GetLobbyStats(lobbyNames, lobbyTypes);
                expectedPeerCount = new int[] {1, 0, 1};
                expectedGameCount = new int[] {0, 0, 0};
                this.VerifyLobbyStatisticsList(response, expectedPeerCount, expectedGameCount);

                // create game on master server
                var createGameResponse = masterClient3.CreateGame(roomName);
                masterClient3.Disconnect();

                // there should be on player in lobby3 even if the game is not created on the game server 
                response = masterClient1.GetLobbyStats(lobbyNames, lobbyTypes);
                expectedPeerCount = new int[] {1, 0, 1};
                expectedGameCount = new int[] {0, 0, 1};
                this.VerifyLobbyStatisticsList(response, expectedPeerCount, expectedGameCount);

                // create the game on the game server
                this.ConnectAndAuthenticate(masterClient3, createGameResponse.Address, masterClient3.UserId);
                masterClient3.CreateGame(roomName);
                Thread.Sleep(100); // give game server some time to report game update to master

                // check if peer and game count have been updated
                response = masterClient1.GetLobbyStats(lobbyNames, lobbyTypes);
                expectedPeerCount = new int[] {1, 0, 1};
                expectedGameCount = new int[] {0, 0, 1};
                this.VerifyLobbyStatisticsList(response, expectedPeerCount, expectedGameCount);

                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                masterClient2.JoinGame(roomName);
                // join second client to the game
                this.ConnectAndAuthenticate(masterClient2, createGameResponse.Address);
                masterClient2.JoinGame(roomName);
                Thread.Sleep(300);

                response = masterClient1.GetLobbyStats(lobbyNames, lobbyTypes);
                expectedPeerCount = new int[] {1, 0, 2};
                expectedGameCount = new int[] {0, 0, 1};
                this.VerifyLobbyStatisticsList(response, expectedPeerCount, expectedGameCount);

                // leave game on the game server
                masterClient3.LeaveGame();
                masterClient3.Dispose();
                Thread.Sleep(300); // give game server some time to report game update to master

                response = masterClient1.GetLobbyStats(lobbyNames, lobbyTypes);
                expectedPeerCount = new int[] {1, 0, 1};
                expectedGameCount = new int[] {0, 0, 1};
                this.VerifyLobbyStatisticsList(response, expectedPeerCount, expectedGameCount);

                // remove game on the game server
                masterClient2.LeaveGame();
                masterClient2.Dispose();
                Thread.Sleep(1000);

                response = masterClient1.GetLobbyStats(lobbyNames, lobbyTypes);
                expectedPeerCount = new int[] {1, 0, 0};
                expectedGameCount = new int[] {0, 0, 0};
                this.VerifyLobbyStatisticsList(response, expectedPeerCount, expectedGameCount);

                // check invalid operations
                lobbyNames = new string[] {lobbyName};
                lobbyTypes = new byte[] {lobbyType, lobbyType2, lobbyType2};
                masterClient1.GetLobbyStats(lobbyNames, lobbyTypes, ErrorCode.InvalidOperation);
                masterClient1.GetLobbyStats(lobbyNames, null, ErrorCode.InvalidOperation);

            }
            finally
            {
                DisposeClients(masterClient1, masterClient2);
            }
        }

        [Test]
        public void LobbyStatisticsUpdateAfterGameFull()
        {
            if (this.IsOnline)
            {
                Assert.Ignore("this test works only in offline mode");
            }

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;
            UnifiedTestClient masterClient3 = null;
            UnifiedTestClient masterClient4 = null;

            try
            {
                var lobbyName = this.GenerateRandomizedRoomName("LobbyStatisticsUpdateAfterGameFull");
                const byte lobbyType = 0;

                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);

                var authParameter = new Dictionary<byte, object> { { (byte)Operations.ParameterCode.LobbyStats, true } };
                // join lobby on master
                masterClient1 = this.CreateMasterClientAndAuthenticate(Player1);

                if (string.IsNullOrEmpty(this.Player1) && masterClient1.Token == null)
                {
                    Assert.Ignore("This test does not work correctly for old clients without userId and token");
                }

                if (this.AuthPolicy == AuthPolicy.UseAuthOnce)// in this case we should send OpSettings to master
                {
                    masterClient1.SendRequest(new OperationRequest
                    {
                        OperationCode = OperationCode.ServerSettings,
                        Parameters = new Dictionary<byte, object> { { SettingsRequestParameters.LobbyStats, true } },
                    });
                }
                masterClient1.JoinLobby(lobbyName, lobbyType);
                masterClient1.WaitForEvent((byte)Events.EventCode.GameList);

                // get stats for all lobbies
                var response = masterClient1.GetLobbyStats(null, null);
                var expectedLobbyNames = new string[] { lobbyName };
                var expectedLobbyTypes = new byte[] { lobbyType };
                var expectedPeerCount = new int[] { 1 };
                var expectedGameCount = new int[] { 0 };
                this.VerifyLobbyStatisticsFullList(response, expectedLobbyNames, expectedLobbyTypes, expectedPeerCount, expectedGameCount);

                // get stats for specific lobbies (the last second should not exists and return 0 for game and peer count)
                var lobbyNames = new string[] { lobbyName  };
                var lobbyTypes = new byte[] { lobbyType};

                // join lobby on master with second client
                masterClient2 = this.CreateMasterClientAndAuthenticate(Player2);
                masterClient2.JoinLobby(lobbyName, lobbyType);
                masterClient2.WaitForEvent((byte)Events.EventCode.GameList);

                // join second client to another lobby 
                masterClient3 = this.CreateMasterClientAndAuthenticate(Player3);
                masterClient3.JoinLobby(lobbyName, lobbyType);
                masterClient3.WaitForEvent((byte)Events.EventCode.GameList);

                // create game on master server
                var createGameResponse = masterClient3.CreateGame(roomName, true, true, 2);

                this.ConnectAndAuthenticate(masterClient3, createGameResponse.Address);

                // create game on Game server
                masterClient3.CreateGame(roomName, true, true, 2);

                Thread.Sleep(300);

                var joinResponse = masterClient2.JoinGame(new JoinGameRequest() {GameId = roomName});
                this.ConnectAndAuthenticate(masterClient2, joinResponse.Address);
                masterClient2.JoinGame(new JoinGameRequest() { GameId = roomName });

                Thread.Sleep(500); // give game server some time to report game update to master

                // there should be on player in lobby3 even if the game is not created on the game server 
                response = masterClient1.GetLobbyStats(lobbyNames, lobbyTypes);
                expectedPeerCount = new int[] { 3 };
                expectedGameCount = new int[] { 1 };
                this.VerifyLobbyStatisticsList(response, expectedPeerCount, expectedGameCount);

                // read all expected GameListUpdates before we start
                var eventData = masterClient1.WaitForEvent((byte)Events.EventCode.GameListUpdate);
                // we try to get it here because there is difference in speed of execution between offline and online tests under resharper
                masterClient1.TryWaitForEvent((byte)Events.EventCode.GameListUpdate, this.WaitTimeout, out eventData);

                masterClient4 = this.CreateMasterClientAndAuthenticate("Player4");

                masterClient1.EventQueueClear();
                // we update token to pass host and game check on GS
                this.UpdateTokensGSAndGame(masterClient4, "localhost", roomName);

                for (var i = 0; i < GameServer.GameServerSettings.Default.JoinErrorCountToReinitialize + 1; ++i)
                {
                    this.ConnectAndAuthenticate(masterClient4, createGameResponse.Address);
                    masterClient4.JoinGame(roomName, ErrorCode.GameFull);
                }

                Thread.Sleep(100); // give game server some time to report game update to master

                // receiving of this event says us that we updated game info on master
                eventData = masterClient1.WaitForEvent((byte)Events.EventCode.GameListUpdate, 10000);

                var gameList = (Hashtable) eventData.Parameters[(byte) ParameterKey.GameList];

                var properities = (Hashtable) gameList[roomName];

                Assert.That(properities, Is.Not.Null);

                Assert.That(properities[(byte)GameParameter.MaxPlayers], Is.EqualTo(2));
                Assert.That(properities[(byte)GameParameter.IsOpen], Is.True);
                Assert.That(properities[(byte) GameParameter.PlayerCount], Is.EqualTo(2));

                masterClient1.EventQueueClear();
                /// we check that inside 10 second range we do not send reinit game message
                for (var i = 0; i < GameServer.GameServerSettings.Default.JoinErrorCountToReinitialize + 1; ++i)
                {
                    this.ConnectAndAuthenticate(masterClient4, createGameResponse.Address);
                    masterClient4.JoinGame(roomName, ErrorCode.GameFull);
                }

                Thread.Sleep(100); // give game server some time to report game update to master
                Assert.That(masterClient1.TryWaitForEvent((byte)Events.EventCode.GameListUpdate, this.WaitTimeout, out eventData), Is.False);

                response = masterClient1.GetLobbyStats(lobbyNames, lobbyTypes);
                expectedPeerCount = new int[] { 3 };
                expectedGameCount = new int[] { 1 };
                this.VerifyLobbyStatisticsList(response, expectedPeerCount, expectedGameCount);

            }
            finally
            {
                DisposeClients(masterClient1, masterClient2, masterClient3, masterClient4);
            }
        }

        [TestCase(AppLobbyType.Default)]
        [TestCase(AppLobbyType.ChannelLobby)]
        public void LobbyJoinLobbyGameCountLimitTest(AppLobbyType appLobbyType)
        {
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("this tests requires userId to be set");
            }

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;
            UnifiedTestClient masterClient3 = null;

            try
            {
                var lobbyName = this.GenerateRandomizedRoomName("LobbyStatisticsRequest1");
                var lobbyType = (byte)appLobbyType;
                string roomName = this.GenerateRandomizedRoomName("LobbyJoinLobbyGameCountLimitTest_TestRoom");

                // join lobby on master
                masterClient1 = this.CreateMasterClientAndAuthenticate(Player1);
                masterClient1.JoinLobby(lobbyName, lobbyType);
//                masterClient1.WaitForEvent((byte)Events.EventCode.GameList);

                this.CreateRoomOnGameServer(masterClient1, roomName + Player1);

                // join lobby on master with second client
                masterClient2 = this.CreateMasterClientAndAuthenticate(Player2);
                masterClient2.JoinLobby(lobbyName, lobbyType);
//                masterClient2.WaitForEvent((byte)Events.EventCode.GameList);

                this.CreateRoomOnGameServer(masterClient2, roomName + Player2);

                // join third client to another lobby 
                masterClient3 = this.CreateMasterClientAndAuthenticate(Player3);

                const int maxGamesCountInList = 1;
                masterClient3.JoinLobby(lobbyName, lobbyType, maxGamesCountInList);
                var ev = masterClient3.WaitForEvent((byte)Events.EventCode.GameList);

                var gameList = (Hashtable) ev[ParameterCode.GameList];

                Assert.AreEqual(maxGamesCountInList, gameList.Count);


            }
            finally
            {
                DisposeClients(masterClient1, masterClient2, masterClient3);
            }
        }

        [Test]
        public void InActiveInGameDoNotGetThisGameAsRandom_DefaultLobbyTest()
        {
            InActiveInGameDoNotGetThisGameAsRandomTestBody(AppLobbyType.Default);
        }

        [Test]
        public void InActiveInGameDoNotGetThisGameAsRandom_SQLLobbyTest()
        {
            InActiveInGameDoNotGetThisGameAsRandomTestBody(AppLobbyType.SqlLobby);
        }

        [Test]
        public void RemoveGameByTimeoutWithExpectedUseres()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;


            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                var createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    AddUsers = new string[] {"PlayerX"},
                };

                client1.CreateGame(createGameRequest);

                // join 2nd client 
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                client2.CreateGame(createGameRequest, ErrorCode.GameIdAlreadyExists);

                Thread.Sleep(32000);

                client2.CreateGame(createGameRequest);
            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [Test]
        public void DestroyGameByTimeoutOnMaster()
        {
            UnifiedTestClient masterClient = null;
            UnifiedTestClient masterClient2 = null;

            try
            {
                masterClient = this.CreateMasterClientAndAuthenticate(Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate(Player2);
                var roomName = this.GenerateRandomizedRoomName("DestroyGameByTimeout_");
                masterClient.CreateGame(roomName);
                int n = 0;
                const int N = 40;
                while (n < N)
                {
                    var request = new OperationRequest()
                    {
                        OperationCode = OperationCode.CreateGame,
                        Parameters = new Dictionary<byte, object>
                        {
                            {(byte)ParameterCode.RoomName, roomName}
                        }
                    };
                    masterClient2.SendRequest(request);

                    var response = masterClient2.WaitForOperationResponse();
                    Assert.That(response.ReturnCode, Is.EqualTo(ErrorCode.GameIdAlreadyExists).Or.EqualTo(ErrorCode.Ok));
                    if (response.ReturnCode == ErrorCode.Ok)
                    {
                        n = 0;
                        break;
                    }
                    ++n;
                    Thread.Sleep(1000);
                }

                if (n != 0)
                {
                    Assert.Fail("Game is not destroyed on master after {0} ms", N * 1000);
                }
            }
            finally
            {
                DisposeClients(masterClient, masterClient2);
            }
        }

        [TestCase(AppLobbyType.Default)]
        [TestCase(AppLobbyType.SqlLobby)]
        [TestCase(AppLobbyType.ChannelLobby)]
        public void NoUpdatesFromInvisibleGame(AppLobbyType lobbyType)
        {
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires userId to be set");
            }

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;

            const string LobbyName = "Lobby";
            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);
                var joinRequest = new CreateGameRequest()
                {
                    GameId = roomName,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    LobbyType = (byte)lobbyType,
                    LobbyName = LobbyName,
                    EmptyRoomLiveTime = 2000,
                    GameProperties = new Hashtable { { (byte)GameParameter.IsVisible, false } },
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(Player1);

                var joinResponse1 = masterClient1.CreateGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address, masterClient1.UserId);
                masterClient1.CreateGame(joinRequest);

                masterClient2 = this.CreateMasterClientAndAuthenticate(Player2);
                this.ConnectAndAuthenticate(masterClient2, this.MasterAddress);

                masterClient2.JoinLobby(LobbyName, (byte)lobbyType);

                masterClient1.LeaveGame(false);

                EventData ev;
                Assert.That(masterClient2.TryWaitEvent(EventCode.GameListUpdate, 5000, out ev), Is.False);
              
            }
            finally
            {
                DisposeClients(masterClient1, masterClient2);
            }
        }

        [TestCase(AppLobbyType.Default)]
        [TestCase(AppLobbyType.ChannelLobby)]
        public void NoUpdatesAfterMakingGameInvisible(AppLobbyType lobbyType)
        {
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires userId to be set");
            }

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;

            const string LobbyName = "Lobby";
            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);
                var joinRequest = new CreateGameRequest()
                {
                    GameId = roomName,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    LobbyType = (byte)lobbyType,
                    LobbyName = LobbyName,
                    EmptyRoomLiveTime = 1000,
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(Player1);

                var joinResponse1 = masterClient1.CreateGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address, masterClient1.UserId);
                masterClient1.CreateGame(joinRequest);

                Thread.Sleep(1000);// we wait to skip all updates related to game creation

                masterClient2 = this.CreateMasterClientAndAuthenticate(Player2);
                this.ConnectAndAuthenticate(masterClient2, this.MasterAddress);

                masterClient2.JoinLobby(LobbyName, (byte)lobbyType);

                masterClient1.OpSetPropertiesOfRoom(new Hashtable {{(byte) GameParameter.IsVisible, false}});

                EventData ev;
                Assert.That(masterClient2.TryWaitEvent(EventCode.GameListUpdate, 2000, out ev), Is.True);

                masterClient1.LeaveGame(false);

                Assert.That(masterClient2.TryWaitEvent(EventCode.GameListUpdate, 3000, out ev), Is.False);
            }
            finally
            {
                DisposeClients(masterClient1, masterClient2);
            }
        }
        #endregion

        #region Helpers

        private void InActiveInGameDoNotGetThisGameAsRandomTestBody(AppLobbyType lobbyType)
        {
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("This test requires userId to be set");
            }

            UnifiedTestClient masterClient1 = null;
            const string LobbyName = "Lobby";
            try
            {
                var roomName = this.GenerateRandomizedRoomName("InActiveInGameDoNotGetThisGameAsRandomTest_");
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = Photon.Hive.Operations.JoinModes.CreateIfNotExists,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    PlayerTTL = 2000,
                    EmptyRoomLiveTime = 5000,
                    LobbyType = (byte)lobbyType,
                    LobbyName = LobbyName,
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(Player1);

                var joinResponse1 = masterClient1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address, masterClient1.UserId);
                masterClient1.JoinGame(joinRequest);

                masterClient1.LeaveGame(true);

                Thread.Sleep(700);

                this.ConnectAndAuthenticate(masterClient1, this.MasterAddress, masterClient1.UserId);

                masterClient1.JoinRandomGame(new Hashtable(), 0, new Hashtable(),
                    MatchmakingMode.FillRoom, LobbyName, lobbyType, null, ErrorCode.NoRandomMatchFound);

                Thread.Sleep(1500);

                var response = masterClient1.JoinRandomGame(new Hashtable(), 0, new Hashtable(),
                    MatchmakingMode.FillRoom, LobbyName, lobbyType, null);

                Assert.AreEqual(roomName, response.GameId);

                this.ConnectAndAuthenticate(masterClient1, response.Address, masterClient1.UserId);
                masterClient1.JoinRoom(roomName, null, 0, new RoomOptions(), false, true, ErrorCode.Ok);

                Thread.Sleep(200);

                masterClient1.LeaveGame(false);

                Thread.Sleep(500);

                this.ConnectAndAuthenticate(masterClient1, this.MasterAddress, masterClient1.UserId);

                masterClient1.JoinRandomGame(new Hashtable(), 0, new Hashtable(),
                    MatchmakingMode.FillRoom, LobbyName, lobbyType, null);

                Assert.AreEqual(roomName, response.GameId);
            }
            finally
            {
                DisposeClients(masterClient1);
            }
        }

        private bool WaitForGameListUpdateEvent(UnifiedTestClient client, int expectedCount = 0)
        {
            var timeout = Environment.TickCount + 10000;

            var gameListUpdateReceived = false;

            while (Environment.TickCount < timeout && !gameListUpdateReceived)
            {
                try
                {
                    var ev = client.WaitForEvent(1000);

                    if (ev.Code == EventCode.GameListUpdate)
                    {
                        gameListUpdateReceived = true;
                        var roomList = (Hashtable)ev.Parameters[ParameterCode.GameList];
                        if (expectedCount > 0)
                        {
                            this.CheckGameListCount(expectedCount, roomList);
                        }
                        Console.WriteLine("WaitForGameListUpdateEvent, got {0} updates", roomList.Count);
                        foreach (var key in roomList.Keys)
                        {
                            Console.WriteLine("Update for game {0}", key);
                        }
                    }
                }
                catch (TimeoutException)
                {
                }
            }

            return gameListUpdateReceived;
        }

        #endregion
    }
}
