using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

using ExitGames.Client.Photon;

using NUnit.Framework;

using Photon.Hive.Operations;
using Photon.Hive.Plugin;
using Photon.LoadBalancing.Operations;
using Photon.LoadBalancing.UnifiedClient;
using Photon.Realtime;
using Photon.UnitTest.Utils.Basic;

using EventCode = Photon.Realtime.EventCode;
using OperationCode = Photon.Realtime.OperationCode;
using ParameterCode = Photon.Realtime.ParameterCode;

namespace Photon.LoadBalancing.UnitTests.UnifiedTests
{
    public abstract partial class LBApiTestsImpl
    {
        #region Properties tests

        [Test]
        public void Props_SetPropertiesForLobby()
        {

            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;

            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                if (string.IsNullOrEmpty(this.Player1) || client1.Token == null)
                {
                    Assert.Ignore("This test does not work correctly for old clients without userId and token");
                }

                Assert.IsTrue(client1.OpJoinLobby());
                var ev = client1.WaitForEvent(EventCode.GameList);
                Assert.AreEqual(EventCode.GameList, ev.Code);
                var gameList = (Hashtable) ev.Parameters[ParameterCode.GameList];
                this.CheckGameListCount(0, gameList);

                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                Assert.IsTrue(client2.OpJoinLobby());
                ev = client2.WaitForEvent(EventCode.GameList);
                Assert.AreEqual(EventCode.GameList, ev.Code);
                gameList = (Hashtable) ev.Parameters[ParameterCode.GameList];
                this.CheckGameListCount(0, gameList);

                client1.EventQueueClear();
                client2.EventQueueClear();

                client1.OperationResponseQueueClear();
                client2.OperationResponseQueueClear();

                // open game
                string roomName = "SetPropertiesForLobby_" + Guid.NewGuid().ToString().Substring(0, 6);

                var player1Properties = new Hashtable {{"Name", this.Player1}};

                var gameProperties = new Hashtable
                {
                    ["P1"] = 1,
                    ["P2"] = 2,

                    ["L1"] = 1,
                    ["L2"] = 2
                };


                var lobbyProperties = new[] {"L1", "L2", "L3"};

                var createRoomResponse = client1.CreateRoom(
                    roomName,
                    new RoomOptions
                    {
                        CustomRoomProperties = gameProperties,
                        CustomRoomPropertiesForLobby = lobbyProperties
                    }, TypedLobby.Default, player1Properties,
                    false);

                var gameServerAddress1 = createRoomResponse.Address;
                Console.WriteLine("Created room " + roomName + " on GS: " + gameServerAddress1);

                // move 1st client to GS: 
                this.ConnectAndAuthenticate(client1, gameServerAddress1, client1.UserId);

                client1.CreateRoom(
                    roomName,
                    new RoomOptions
                    {
                        CustomRoomProperties = gameProperties,
                        CustomRoomPropertiesForLobby = lobbyProperties
                    },
                    TypedLobby.Default,
                    player1Properties,
                    true);

                // get own join event: 
                ev = client1.WaitForEvent();
                Assert.AreEqual(EventCode.Join, ev.Code);
                Assert.AreEqual(1, ev.Parameters[ParameterCode.ActorNr]);

                var actorList = (int[]) ev.Parameters[ParameterCode.ActorList];
                Assert.AreEqual(1, actorList.Length);
                Assert.AreEqual(1, actorList[0]);

                var ActorProperties = ((Hashtable) ev.Parameters[ParameterCode.PlayerProperties]);
                Assert.AreEqual(this.Player1, ActorProperties["Name"]);

                ev = client2.WaitForEvent(EventCode.GameListUpdate);

                Hashtable roomList = null;
                // we have this loop in order to protect test from unexpected update, which we get because of other tests
                var exitLoop = false;
                while (!exitLoop)
                {
                    roomList = (Hashtable) ev.Parameters[ParameterCode.GameList];

                    Assert.GreaterOrEqual(roomList.Count, 1);

                    if (roomList[roomName] == null)
                    {
                        ev = client2.WaitForEvent(EventCode.GameListUpdate, 12*ConnectPolicy.WaitTime);
                    }
                    else
                    {
                        exitLoop = true;
                    }
                }
                var room = (Hashtable) roomList[roomName];
                Assert.IsNotNull(room);
                Assert.AreEqual(5, room.Count);

                Assert.IsNotNull(room[GamePropertyKey.IsOpen], "IsOpen");
                Assert.IsNotNull(room[GamePropertyKey.MaxPlayers], "MaxPlayers");
                Assert.IsNotNull(room[GamePropertyKey.PlayerCount], "PlayerCount");
                Assert.IsNotNull(room["L1"], "L1");
                Assert.IsNotNull(room["L2"], "L2");


                Assert.AreEqual(true, room[GamePropertyKey.IsOpen], "IsOpen");
                Assert.AreEqual(0, room[GamePropertyKey.MaxPlayers], "MaxPlayers");
                Assert.AreEqual(1, room[GamePropertyKey.PlayerCount], "PlayerCount");
                Assert.AreEqual(1, room["L1"], "L1");
                Assert.AreEqual(2, room["L2"], "L2");

                client1.OpSetPropertiesOfRoom(new Hashtable {{"L3", 3}, {"L1", null}, {"L2", 20}});


                ev = client2.WaitForEvent(EventCode.GameListUpdate);

                roomList = (Hashtable) ev.Parameters[ParameterCode.GameList];
                Assert.AreEqual(1, roomList.Count);

                room = (Hashtable) roomList[roomName];
                Assert.IsNotNull(room);
                Assert.AreEqual(5, room.Count);

                Assert.IsNotNull(room[GamePropertyKey.IsOpen], "IsOpen");
                Assert.IsNotNull(room[GamePropertyKey.MaxPlayers], "MaxPlayers");
                Assert.IsNotNull(room[GamePropertyKey.PlayerCount], "PlayerCount");
                Assert.IsNotNull(room["L2"], "L2");
                Assert.IsNotNull(room["L3"], "L3");

                Assert.AreEqual(true, room[GamePropertyKey.IsOpen], "IsOpen");
                Assert.AreEqual(0, room[GamePropertyKey.MaxPlayers], "MaxPlayers");
                Assert.AreEqual(1, room[GamePropertyKey.PlayerCount], "PlayerCount");
                Assert.AreEqual(20, room["L2"], "L2");
                Assert.AreEqual(3, room["L3"], "L3");

                client1.SendRequestAndWaitForResponse(new OperationRequest {OperationCode = OperationCode.Leave});
            }
            finally
            {
                DisposeClients(client1, client2, client1);
            }
        }

        [Test]
        public void Props_MatchByProperties()
        {
            UnifiedTestClient masterClient = null;
            UnifiedTestClient gameClient = null;

            try
            {
                // create game on the game server
                string roomName = this.GenerateRandomizedRoomName("MatchByProperties_");

                var gameProperties = new Hashtable
                {
                    ["P1"] = 1,
                    ["P2"] = 2,
                    ["L1"] = 1,
                    ["L2"] = 2,
                    ["L3"] = 3
                };

                var lobbyProperties = new[] {"L1", "L2", "L3"};

                gameClient = this.CreateGameOnGameServer(this.Player1, roomName, null, 0, true, true, 0, gameProperties, lobbyProperties);

                // test matchmaking
                masterClient = this.CreateMasterClientAndAuthenticate(this.Player2);
                masterClient.EventQueueClear();
                masterClient.OperationResponseQueueClear();

                var joinRequest = new JoinRandomGameRequest
                {
                    JoinRandomType = (byte) MatchmakingMode.FillRoom,
                    GameProperties = new Hashtable()
                };


                joinRequest.GameProperties.Add("N", null);
                masterClient.OperationResponseQueueClear();
                masterClient.JoinRandomGame(joinRequest, ErrorCode.NoRandomMatchFound);

                joinRequest.GameProperties.Clear();
                joinRequest.GameProperties.Add("L1", 5);
                masterClient.OperationResponseQueueClear();
                masterClient.JoinRandomGame(joinRequest, ErrorCode.NoRandomMatchFound);

                joinRequest.GameProperties.Clear();
                joinRequest.GameProperties.Add("L1", 1);
                joinRequest.GameProperties.Add("L2", 1);
                masterClient.OperationResponseQueueClear();
                masterClient.JoinRandomGame(joinRequest, ErrorCode.NoRandomMatchFound);

                joinRequest.GameProperties.Clear();
                joinRequest.GameProperties.Add("L1", 1);
                joinRequest.GameProperties.Add("L2", 2);
                masterClient.OperationResponseQueueClear();
                masterClient.JoinRandomGame(joinRequest, ErrorCode.Ok);

                gameClient.LeaveGame();
            }
            finally
            {
                DisposeClients(masterClient, gameClient);
            }
        }

        [Test]
        public void Props_BroadcastProperties()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;


            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                client1.EventQueueClear();
                client2.EventQueueClear();

                client1.OperationResponseQueueClear();
                client2.OperationResponseQueueClear();

                // open game
                string roomName = "BroadcastProperties_" + Guid.NewGuid().ToString().Substring(0, 6);

                var player1Properties = new Hashtable { { "Name", this.Player1 } };

                var gameProperties = new Hashtable
                {
                    ["P1"] = 1,
                    ["P2"] = 2
                };

                var lobbyProperties = new[] { "L1", "L2", "L3" };

                var createResponse = client1.CreateRoom(
                    roomName,
                    new RoomOptions
                    {
                        CustomRoomProperties = gameProperties,
                        CustomRoomPropertiesForLobby = lobbyProperties
                    }, TypedLobby.Default, player1Properties, false);

                var gameServerAddress1 = createResponse.Address;
                Console.WriteLine("Created room " + roomName + " on GS: " + gameServerAddress1);

                // move 1st client to GS: 
                this.ConnectAndAuthenticate(client1, gameServerAddress1, client1.UserId);

                client1.CreateRoom(
                    roomName,
                    new RoomOptions
                    {
                        CustomRoomProperties = gameProperties,
                        CustomRoomPropertiesForLobby = lobbyProperties
                    }, TypedLobby.Default, player1Properties, true);

                client2.JoinGame(roomName);

                // move 2nd client to GS: 
                this.ConnectAndAuthenticate(client2, gameServerAddress1, client2.UserId);

                var player2Properties = new Hashtable { { "Name", this.Player2 } };

                var joinResponse = client2.JoinRoom(roomName, player2Properties, 0, new RoomOptions(), false, true);

                var room = joinResponse.GameProperties;
                Assert.IsNotNull(room);
                Assert.AreEqual(7, room.Count);

                Assert.IsNotNull(room[GamePropertyKey.IsOpen], "IsOpen");
                Assert.IsNotNull(room[GamePropertyKey.IsVisible], "IsVisible");
                Assert.IsNotNull(room[GamePropertyKey.PropsListedInLobby], "PropertiesInLobby");
                Assert.IsNotNull(room["P1"], "P1");
                Assert.IsNotNull(room["P2"], "P2");


                Assert.AreEqual(true, room[GamePropertyKey.IsOpen], "IsOpen");
                Assert.AreEqual(true, room[GamePropertyKey.IsVisible], "IsVisible");

                Assert.AreEqual(1, room[GamePropertyKey.MasterClientId], "MasterClientId");
                Assert.AreEqual(3, ((string[])room[GamePropertyKey.PropsListedInLobby]).Length, "PropertiesInLobby");
                Assert.AreEqual("L1", ((string[])room[GamePropertyKey.PropsListedInLobby])[0], "PropertiesInLobby");
                Assert.AreEqual("L2", ((string[])room[GamePropertyKey.PropsListedInLobby])[1], "PropertiesInLobby");
                Assert.AreEqual("L3", ((string[])room[GamePropertyKey.PropsListedInLobby])[2], "PropertiesInLobby");
                Assert.AreEqual(1, room["P1"], "P1");
                Assert.AreEqual(2, room["P2"], "P2");

                // set properties: 
                var setProperties = new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>()
                };

                setProperties.Parameters[ParameterCode.Broadcast] = true;
                setProperties.Parameters[ParameterCode.Properties] = new Hashtable { { "P3", 3 }, { "P1", null }, { "P2", 20 } };

                var setPropResponse = client1.SendRequestAndWaitForResponse(setProperties);
                Assert.AreEqual(OperationCode.SetProperties, setPropResponse.OperationCode);
                Assert.AreEqual(ErrorCode.Ok, setPropResponse.ReturnCode, setPropResponse.DebugMessage);

                var ev = client2.WaitForEvent(EventCode.PropertiesChanged);

                room = (Hashtable)ev.Parameters[ParameterCode.Properties];
                Assert.IsNotNull(room);
                Assert.AreEqual(3, room.Count);

                Assert.IsNull(room["P1"], "P1");
                Assert.IsNotNull(room["P2"], "P2");
                Assert.IsNotNull(room["P3"], "P3");

                Assert.AreEqual(null, room["P1"], "P1");
                Assert.AreEqual(20, room["P2"], "P2");
                Assert.AreEqual(3, room["P3"], "P3");

                var getProperties = new OperationRequest
                {
                    OperationCode = OperationCode.GetProperties,
                    Parameters = new Dictionary<byte, object>()
                };
                getProperties.Parameters[ParameterCode.Properties] = PropertyType.Game;

                var getPropResponse = client2.SendRequestAndWaitForResponse(getProperties);

                Assert.AreEqual(OperationCode.GetProperties, getPropResponse.OperationCode);
                Assert.AreEqual(ErrorCode.Ok, getPropResponse.ReturnCode, getPropResponse.DebugMessage);

                room = (Hashtable)getPropResponse.Parameters[ParameterCode.GameProperties];
                Assert.IsNotNull(room);
                Assert.AreEqual(8, room.Count);

                Assert.IsNotNull(room[GamePropertyKey.IsOpen], "IsOpen");
                Assert.IsNotNull(room[GamePropertyKey.IsVisible], "IsVisible");
                Assert.IsNotNull(room[GamePropertyKey.PropsListedInLobby], "PropertiesInLobby");
                Assert.IsNull(room["P1"], "P1");
                Assert.IsNotNull(room["P2"], "P2");
                Assert.IsNotNull(room["P3"], "P3");


                Assert.AreEqual(true, room[GamePropertyKey.IsOpen], "IsOpen");
                Assert.AreEqual(true, room[GamePropertyKey.IsVisible], "IsVisible");
                Assert.AreEqual(3, ((string[])room[GamePropertyKey.PropsListedInLobby]).Length, "PropertiesInLobby");
                Assert.AreEqual("L1", ((string[])room[GamePropertyKey.PropsListedInLobby])[0], "PropertiesInLobby");
                Assert.AreEqual("L2", ((string[])room[GamePropertyKey.PropsListedInLobby])[1], "PropertiesInLobby");
                Assert.AreEqual("L3", ((string[])room[GamePropertyKey.PropsListedInLobby])[2], "PropertiesInLobby");
                Assert.AreEqual(null, room["P1"], "P1");
                Assert.AreEqual(20, room["P2"], "P2");
                Assert.AreEqual(3, room["P3"], "P3");
            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [Test]
        public void Props_SetPropertiesToInactivePlayer()
        {
            if (!this.UsePlugins)
            {
                Assert.Ignore("This test needs plugins");
            }

            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;

            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                client1.EventQueueClear();
                client2.EventQueueClear();

                client1.OperationResponseQueueClear();
                client2.OperationResponseQueueClear();

                // open game
                string roomName = MethodBase.GetCurrentMethod().Name + Guid.NewGuid().ToString().Substring(0, 6);

                var createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    PlayerTTL = 30000,
                    Plugins = new[] { "SetPropertiesToInActiveActorTestPlugin" }
                };
                var createResponse = client1.CreateGame(createGameRequest);

                var gameServerAddress1 = createResponse.Address;
                Console.WriteLine("Created room " + roomName + " on GS: " + gameServerAddress1);

                // move 1st client to GS: 
                this.ConnectAndAuthenticate(client1, gameServerAddress1, client1.UserId);

                client1.CreateGame(createGameRequest);

                client2.JoinGame(roomName);
                // move 2nd client to GS: 
                this.ConnectAndAuthenticate(client2, gameServerAddress1, client2.UserId);

                client2.JoinRoom(roomName, null, 0, new RoomOptions(), false, true);

                Thread.Sleep(100);

                client2.LeaveGame(true);

                var ev = client1.WaitForEvent(0, this.WaitTimeout);
                Assert.That(ev[0], Is.Empty);
            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [Test]
        public void Props_SetEmptyRoomTTLUsingRequestParam()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;

            const int EmptyRoomTTL = 3000;
            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                client1.EventQueueClear();
                client2.EventQueueClear();

                client1.OperationResponseQueueClear();
                client2.OperationResponseQueueClear();

                // open game
                string roomName = MethodBase.GetCurrentMethod().Name + Guid.NewGuid().ToString().Substring(0, 6);

                var createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    EmptyRoomLiveTime = EmptyRoomTTL,
                    GameProperties = new Hashtable { { (byte)GameParameter.EmptyRoomTTL, 123} }// we check that correct ttl is set
                };
                var createResponse = client1.CreateGame(createGameRequest);

                var gameServerAddress1 = createResponse.Address;
                Console.WriteLine("Created room " + roomName + " on GS: " + gameServerAddress1);

                // move 1st client to GS: 
                this.ConnectAndAuthenticate(client1, gameServerAddress1, client1.UserId);

                client1.CreateGame(createGameRequest);


                client2.JoinGame(roomName);
                // move 2nd client to GS: 
                this.ConnectAndAuthenticate(client2, gameServerAddress1, client2.UserId);

                var joinResponse = client2.JoinRoom(roomName, null, 0, new RoomOptions(), false, true);

                //check that properties contain EmptyRoomTTL wel-known property

                Assert.That(joinResponse.GameProperties.Contains((byte)GameParameter.EmptyRoomTTL));
                Assert.That(joinResponse.GameProperties[((byte)GameParameter.EmptyRoomTTL)], Is.EqualTo(EmptyRoomTTL));

                Thread.Sleep(100);

                client2.LeaveGame();
                client1.LeaveGame();

                Thread.Sleep(EmptyRoomTTL + 300);

                this.ConnectAndAuthenticate(client2, this.MasterAddress);

                client2.SendRequestAndWaitForResponse(new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        { ParameterCode.RoomName, roomName }
                    }
                }, ErrorCode.GameDoesNotExist);

            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [Test]
        public void Props_SetEmptyRoomTTLUsingProperties()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;

            const int EmptyRoomTTL = 3000;
            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                // open game
                string roomName = MethodBase.GetCurrentMethod().Name + Guid.NewGuid().ToString().Substring(0, 6);

                var createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    GameProperties = new Hashtable { { (byte)GameParameter.EmptyRoomTTL, EmptyRoomTTL } }// we check that correct ttl is set
                };
                var createResponse = client1.CreateGame(createGameRequest);

                var gameServerAddress1 = createResponse.Address;
                Console.WriteLine("Created room " + roomName + " on GS: " + gameServerAddress1);

                // move 1st client to GS: 
                this.ConnectAndAuthenticate(client1, gameServerAddress1, client1.UserId);

                client1.CreateGame(createGameRequest);

                client2.JoinGame(roomName);
                // move 2nd client to GS: 
                this.ConnectAndAuthenticate(client2, gameServerAddress1, client2.UserId);

                var joinResponse = client2.JoinRoom(roomName, null, 0, new RoomOptions(), false, true);

                //check that properties contain EmptyRoomTTL wel-known property

                Assert.That(joinResponse.GameProperties.Contains((byte)GameParameter.EmptyRoomTTL));
                Assert.That(joinResponse.GameProperties[((byte)GameParameter.EmptyRoomTTL)], Is.EqualTo(EmptyRoomTTL));

                Thread.Sleep(100);

                client2.LeaveGame();
                client1.LeaveGame();

                Thread.Sleep(EmptyRoomTTL + 300);

                this.ConnectAndAuthenticate(client2, this.MasterAddress);

                client2.SendRequestAndWaitForResponse(new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        { ParameterCode.RoomName, roomName }
                    }
                }, ErrorCode.GameDoesNotExist);

            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [Test]
        public void Props_SetTooBigEmptyRoomTTLUsingPropertiesOnJoin()
        {
            UnifiedTestClient client1 = null;

            const int EmptyRoomTTL = 3000000;
            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                // open game
                string roomName = MethodBase.GetCurrentMethod().Name + Guid.NewGuid().ToString().Substring(0, 6);

                var createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    GameProperties = new Hashtable { { (byte)GameParameter.EmptyRoomTTL, EmptyRoomTTL } }// we check that correct ttl is set
                };
                var createResponse = client1.CreateGame(createGameRequest);

                var gameServerAddress1 = createResponse.Address;
                Console.WriteLine("Created room " + roomName + " on GS: " + gameServerAddress1);

                // move 1st client to GS: 
                this.ConnectAndAuthenticate(client1, gameServerAddress1);

                client1.CreateGame(createGameRequest, ErrorCode.InvalidOperation);
            }
            finally
            {
                DisposeClients(client1);
            }
        }

        [Test]
        public void Props_UpdateEmptyRoomTTL()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;

            const int EmptyRoomTTL = 30000;
            const int EmptyRoomTTL2 = 2000;
            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                // open game
                string roomName = MethodBase.GetCurrentMethod().Name + Guid.NewGuid().ToString().Substring(0, 6);

                var createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    GameProperties = new Hashtable { { (byte)GameParameter.EmptyRoomTTL, EmptyRoomTTL } }// we check that correct ttl is set
                };
                var createResponse = client1.CreateGame(createGameRequest);

                var gameServerAddress1 = createResponse.Address;
                Console.WriteLine("Created room " + roomName + " on GS: " + gameServerAddress1);

                // move 1st client to GS: 
                this.ConnectAndAuthenticate(client1, gameServerAddress1, client1.UserId);

                client1.CreateGame(createGameRequest);

                client1.SendRequestAndWaitForResponse(new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Properties,  new Hashtable{ { (byte)GameParameter.EmptyRoomTTL, EmptyRoomTTL2} } }
                    }
                });

                client2.JoinGame(roomName);
                // move 2nd client to GS: 
                this.ConnectAndAuthenticate(client2, gameServerAddress1, client2.UserId);

                var joinResponse = client2.JoinRoom(roomName, null, 0, new RoomOptions(), false, true);

                //check that properties contain EmptyRoomTTL wel-known property

                Assert.That(joinResponse.GameProperties.Contains((byte)GameParameter.EmptyRoomTTL));
                Assert.That(joinResponse.GameProperties[((byte)GameParameter.EmptyRoomTTL)], Is.EqualTo(EmptyRoomTTL2));

                Thread.Sleep(100);

                client2.LeaveGame();
                client1.LeaveGame();

                Thread.Sleep(EmptyRoomTTL2 + 300);

                this.ConnectAndAuthenticate(client2, this.MasterAddress);

                client2.SendRequestAndWaitForResponse(new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        { ParameterCode.RoomName, roomName }
                    }
                }, ErrorCode.GameDoesNotExist);

            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [Test]
        public void Props_UpdateEmptyRoomTTLUsingTooBigValue()
        {
            UnifiedTestClient client1 = null;

            const int EmptyRoomTTL = 3000;
            const int EmptyRoomTTL2 = 200000000;
            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                // open game
                var roomName = MethodBase.GetCurrentMethod().Name + Guid.NewGuid().ToString().Substring(0, 6);

                var createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    GameProperties = new Hashtable { { (byte)GameParameter.EmptyRoomTTL, EmptyRoomTTL } }// we check that correct ttl is set
                };
                var createResponse = client1.CreateGame(createGameRequest);

                var gameServerAddress1 = createResponse.Address;
                Console.WriteLine("Created room " + roomName + " on GS: " + gameServerAddress1);

                // move 1st client to GS: 
                this.ConnectAndAuthenticate(client1, gameServerAddress1, client1.UserId);

                client1.CreateGame(createGameRequest);

                client1.SendRequestAndWaitForResponse(new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Properties,  new Hashtable{ { (byte)GameParameter.EmptyRoomTTL, EmptyRoomTTL2} } }
                    }
                }, ErrorCode.InvalidOperation);
            }
            finally
            {
                DisposeClients(client1);
            }
        }

        [Test]
        public void Props_UpdateEmptyRoomTTLAndCheckBroadcast()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;

            const int EmptyRoomTTL = 30000;
            const int EmptyRoomTTL2 = 2000;
            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                // open game
                string roomName = MethodBase.GetCurrentMethod().Name + Guid.NewGuid().ToString().Substring(0, 6);

                var createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    GameProperties = new Hashtable { { (byte)GameParameter.EmptyRoomTTL, EmptyRoomTTL } }// we check that correct ttl is set
                };
                var createResponse = client1.CreateGame(createGameRequest);

                var gameServerAddress1 = createResponse.Address;
                Console.WriteLine("Created room " + roomName + " on GS: " + gameServerAddress1);

                // move 1st client to GS: 
                this.ConnectAndAuthenticate(client1, gameServerAddress1, client1.UserId);

                client1.CreateGame(createGameRequest);

                client2.JoinGame(roomName);
                // move 2nd client to GS: 
                this.ConnectAndAuthenticate(client2, gameServerAddress1, client2.UserId);

                client2.JoinRoom(roomName, null, 0, new RoomOptions(), false, true);

                client1.EventQueueClear();
                client2.EventQueueClear();

                client1.SendRequestAndWaitForResponse(new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Properties,  new Hashtable{ { (byte)GameParameter.EmptyRoomTTL, EmptyRoomTTL2} } },
                        {ParameterCode.Broadcast, true}
                    }
                });

                Assert.That(client2.TryWaitForEvent(EventCode.PropertiesChanged, out _), Is.True);

            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [Test]
        public void Props_SetPlayerTTLUsingRequestParam()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;

            const int PlayerTTL = 3000;
            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                client1.EventQueueClear();
                client2.EventQueueClear();

                client1.OperationResponseQueueClear();
                client2.OperationResponseQueueClear();

                // open game
                string roomName = MethodBase.GetCurrentMethod().Name + Guid.NewGuid().ToString().Substring(0, 6);

                var createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    PlayerTTL = PlayerTTL,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    GameProperties = new Hashtable { { (byte)GameParameter.PlayerTTL, 123 } }// we check that correct ttl is set
                };
                var createResponse = client1.CreateGame(createGameRequest);

                var gameServerAddress1 = createResponse.Address;
                Console.WriteLine("Created room " + roomName + " on GS: " + gameServerAddress1);

                // move 1st client to GS: 
                this.ConnectAndAuthenticate(client1, gameServerAddress1, client1.UserId);

                client1.CreateGame(createGameRequest);


                client2.JoinGame(roomName);
                // move 2nd client to GS: 
                this.ConnectAndAuthenticate(client2, gameServerAddress1, client2.UserId);

                var joinResponse = client2.JoinRoom(roomName, null, 0, new RoomOptions(), false, true);

                //check that properties contain PlayerTTL wel-known property

                Assert.That(joinResponse.GameProperties.Contains((byte)GameParameter.PlayerTTL));
                Assert.That(joinResponse.GameProperties[((byte)GameParameter.PlayerTTL)], Is.EqualTo(PlayerTTL));

                Thread.Sleep(100);

                var client2Token = client2.Token;
                client2.LeaveGame(true);

                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                // restore token to be able to pass auth on GS
                client2.Token = client2Token;
                this.ConnectAndAuthenticate(client2, gameServerAddress1, client2.UserId);

                Thread.Sleep(PlayerTTL + 300);

                client2.SendRequestAndWaitForResponse(new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        { ParameterCode.RoomName, roomName },
                        { ParameterCode.JoinMode, JoinModeConstants.RejoinOnly },
                        { ParameterCode.ActorNr, 2 }
                    }
                }, ErrorCode.JoinFailedWithRejoinerNotFound);

            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [Test]
        public void Props_SetPlayerTTLUsingProperties()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;

            const int PlayerTTL = 3000;
            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                client1.EventQueueClear();
                client2.EventQueueClear();

                client1.OperationResponseQueueClear();
                client2.OperationResponseQueueClear();

                // open game
                string roomName = MethodBase.GetCurrentMethod().Name + Guid.NewGuid().ToString().Substring(0, 6);

                var createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    GameProperties = new Hashtable { { (byte)GameParameter.PlayerTTL, PlayerTTL } }
                };
                var createResponse = client1.CreateGame(createGameRequest);

                var gameServerAddress1 = createResponse.Address;
                Console.WriteLine("Created room " + roomName + " on GS: " + gameServerAddress1);

                // move 1st client to GS: 
                this.ConnectAndAuthenticate(client1, gameServerAddress1, client1.UserId);

                client1.CreateGame(createGameRequest);


                client2.JoinGame(roomName);
                // move 2nd client to GS: 
                this.ConnectAndAuthenticate(client2, gameServerAddress1, client2.UserId);

                var joinResponse = client2.JoinRoom(roomName, null, 0, new RoomOptions(), false, true);

                //check that properties contain PlayerTTL wel-known property

                Assert.That(joinResponse.GameProperties.Contains((byte)GameParameter.PlayerTTL));
                Assert.That(joinResponse.GameProperties[((byte)GameParameter.PlayerTTL)], Is.EqualTo(PlayerTTL));

                Thread.Sleep(100);

                var client2Token = client2.Token;
                client2.LeaveGame(true);

                client2.Dispose();

                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                // restore token to be able to pass auth on GS
                client2.Token = client2Token;
                this.ConnectAndAuthenticate(client2, gameServerAddress1, client2.UserId);

                Thread.Sleep(PlayerTTL + 300);

                client2.SendRequestAndWaitForResponse(new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        { ParameterCode.RoomName, roomName },
                        { ParameterCode.JoinMode, JoinModeConstants.RejoinOnly },
                        { ParameterCode.ActorNr, 2 }
                    }
                }, ErrorCode.JoinFailedWithRejoinerNotFound);

            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [Test]
        public void Props_UpdatePlayerTTL()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;

            const int PlayerTTL = 30000;
            const int PlayerTTL2 = 2000;
            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                client1.EventQueueClear();
                client2.EventQueueClear();

                client1.OperationResponseQueueClear();
                client2.OperationResponseQueueClear();

                // open game
                string roomName = MethodBase.GetCurrentMethod().Name + Guid.NewGuid().ToString().Substring(0, 6);

                var createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    PlayerTTL = PlayerTTL,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    GameProperties = new Hashtable { { (byte)GameParameter.PlayerTTL, 123 } }// we check that correct ttl is set
                };
                var createResponse = client1.CreateGame(createGameRequest);

                var gameServerAddress1 = createResponse.Address;
                Console.WriteLine("Created room " + roomName + " on GS: " + gameServerAddress1);

                // move 1st client to GS: 
                this.ConnectAndAuthenticate(client1, gameServerAddress1, client1.UserId);

                client1.CreateGame(createGameRequest);


                client2.JoinGame(roomName);
                // move 2nd client to GS: 
                this.ConnectAndAuthenticate(client2, gameServerAddress1, client2.UserId);

                var joinResponse = client2.JoinRoom(roomName, null, 0, new RoomOptions(), false, true);

                //check that properties contain PlayerTTL wel-known property

                Assert.That(joinResponse.GameProperties.Contains((byte)GameParameter.PlayerTTL));
                Assert.That(joinResponse.GameProperties[((byte)GameParameter.PlayerTTL)], Is.EqualTo(PlayerTTL));

                client1.SendRequestAndWaitForResponse(new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Properties,  new Hashtable{ { (byte)GameParameter.PlayerTTL, PlayerTTL2} } }
                    }
                });
                var client2Token = client2.Token;

                Thread.Sleep(100);

                client2.LeaveGame(true);

                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                // restore token to be able to pass auth on GS
                client2.Token = client2Token;
                this.ConnectAndAuthenticate(client2, gameServerAddress1, client2.UserId);

                Thread.Sleep(PlayerTTL2 + 300);

                client2.SendRequestAndWaitForResponse(new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        { ParameterCode.RoomName, roomName },
                        { ParameterCode.JoinMode, JoinModeConstants.RejoinOnly },
                        { ParameterCode.ActorNr, 2 }
                    }
                }, ErrorCode.JoinFailedWithRejoinerNotFound);

            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [Test]
        public void Props_UpdatePlayerTTLAndCheckBroadcast()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;

            const int PlayerTTL = 30000;
            const int PlayerTTL2 = 2000;
            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                client1.EventQueueClear();
                client2.EventQueueClear();

                client1.OperationResponseQueueClear();
                client2.OperationResponseQueueClear();

                // open game
                string roomName = MethodBase.GetCurrentMethod().Name + Guid.NewGuid().ToString().Substring(0, 6);

                var createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    PlayerTTL = PlayerTTL,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    GameProperties = new Hashtable { { (byte)GameParameter.PlayerTTL, 123 } }// we check that correct ttl is set
                };
                var createResponse = client1.CreateGame(createGameRequest);

                var gameServerAddress1 = createResponse.Address;
                Console.WriteLine("Created room " + roomName + " on GS: " + gameServerAddress1);

                // move 1st client to GS: 
                this.ConnectAndAuthenticate(client1, gameServerAddress1, client1.UserId);

                client1.CreateGame(createGameRequest);


                client2.JoinGame(roomName);
                // move 2nd client to GS: 
                this.ConnectAndAuthenticate(client2, gameServerAddress1, client2.UserId);

                var joinResponse = client2.JoinRoom(roomName, null, 0, new RoomOptions(), false, true);

                //check that properties contain PlayerTTL wel-known property

                Assert.That(joinResponse.GameProperties.Contains((byte)GameParameter.PlayerTTL));
                Assert.That(joinResponse.GameProperties[((byte)GameParameter.PlayerTTL)], Is.EqualTo(PlayerTTL));

                client1.EventQueueClear();
                client2.EventQueueClear();

                client1.SendRequestAndWaitForResponse(new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Properties,  new Hashtable{ { (byte)GameParameter.PlayerTTL, PlayerTTL2} } },
                        {ParameterCode.Broadcast, true}
                    }
                });

                Assert.That(client2.TryWaitForEvent(EventCode.PropertiesChanged, out _), Is.True);
            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [Test]
        public void Props_UpdatePlayerTTLAndEmptyRoomTTLUsingPlugin()
        {
            if (!this.UsePlugins)
            {
                Assert.Ignore("This test needs plugins");
            }

            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;

            const int PlayerTTL = 2000;
            const int EmptyRoomTTL = 2000;
            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                client1.EventQueueClear();
                client2.EventQueueClear();

                client1.OperationResponseQueueClear();
                client2.OperationResponseQueueClear();

                // open game
                string roomName = MethodBase.GetCurrentMethod().Name + Guid.NewGuid().ToString().Substring(0, 6);

                var createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    Plugins = new [] {"SetPlayerTTLAndEmptyRoomTTLPlugin"}
                };
                var createResponse = client1.CreateGame(createGameRequest);

                var gameServerAddress1 = createResponse.Address;
                Console.WriteLine("Created room " + roomName + " on GS: " + gameServerAddress1);

                // move 1st client to GS: 
                this.ConnectAndAuthenticate(client1, gameServerAddress1, client1.UserId);

                client1.CreateGame(createGameRequest);

                client2.JoinGame(roomName);
                // move 2nd client to GS: 
                this.ConnectAndAuthenticate(client2, gameServerAddress1, client2.UserId);

                client2.JoinRoom(roomName, null, 0, new RoomOptions(), false, true);


                client1.SendRequest(new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Code,  (byte)1 },
                    }
                });

                var client2Token = client2.Token;
                Thread.Sleep(100);

                client2.LeaveGame(true);
                client2.Dispose();

                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                // restore token to be able to pass auth on GS
                client2.Token = client2Token;
                this.ConnectAndAuthenticate(client2, gameServerAddress1, client2.UserId);

                Thread.Sleep(PlayerTTL + 300);

                client2.SendRequestAndWaitForResponse(new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        { ParameterCode.RoomName, roomName },
                        { ParameterCode.JoinMode, JoinModeConstants.RejoinOnly },
                        { ParameterCode.ActorNr, 2 }
                    }
                }, ErrorCode.JoinFailedWithRejoinerNotFound);


                client1.LeaveGame();

                Thread.Sleep(EmptyRoomTTL + 300);

                client2.Dispose();
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                client2.SendRequestAndWaitForResponse(new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        { ParameterCode.RoomName, roomName }
                    }
                }, ErrorCode.GameDoesNotExist);

            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [Test]
        public void Props_UpdatePlayerTTLAndEmptyRoomTTLOnCreate()
        {
            if (!this.UsePlugins)
            {
                Assert.Ignore("This test needs plugins");
            }

            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;

            const int PlayerTTL = 2000;
            const int EmptyRoomTTL = 2000;
            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                client1.EventQueueClear();
                client2.EventQueueClear();

                client1.OperationResponseQueueClear();
                client2.OperationResponseQueueClear();

                // open game
                string roomName = MethodBase.GetCurrentMethod().Name + Guid.NewGuid().ToString().Substring(0, 6);

                var createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    Plugins = new[] { "SetPlayerTTLAndEmptyRoomTTLPlugin" }
                };
                var createResponse = client1.CreateGame(createGameRequest);

                var gameServerAddress1 = createResponse.Address;
                Console.WriteLine("Created room " + roomName + " on GS: " + gameServerAddress1);

                // move 1st client to GS: 
                this.ConnectAndAuthenticate(client1, gameServerAddress1, client1.UserId);

                client1.CreateGame(createGameRequest);

                client2.JoinGame(roomName);
                // move 2nd client to GS: 
                this.ConnectAndAuthenticate(client2, gameServerAddress1, client2.UserId);

                client2.JoinRoom(roomName, null, 0, new RoomOptions(), false, true);


                Thread.Sleep(100);
                var client2Token = client2.Token;

                client2.LeaveGame(true);
                client2.Dispose();

                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                // restore token to be able to pass auth on GS
                client2.Token = client2Token;
                this.ConnectAndAuthenticate(client2, gameServerAddress1, client2.UserId);

                Thread.Sleep(PlayerTTL + 300);

                client2.SendRequestAndWaitForResponse(new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        { ParameterCode.RoomName, roomName },
                        { ParameterCode.JoinMode, JoinModeConstants.RejoinOnly },
                        { ParameterCode.ActorNr, 2 }
                    }
                }, ErrorCode.JoinFailedWithRejoinerNotFound);


                client1.LeaveGame();

                Thread.Sleep(EmptyRoomTTL + 300);

                client2.Dispose();
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                client2.SendRequestAndWaitForResponse(new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        { ParameterCode.RoomName, roomName }
                    }
                }, ErrorCode.GameDoesNotExist);

            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [Test]
        public void Props_UpdateMaxPlayersToWrongValueUsingNegativeActorId ()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;

            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                // open game
                string roomName = MethodBase.GetCurrentMethod().Name + Guid.NewGuid().ToString().Substring(0, 6);

                var createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                };
                var createResponse = client1.CreateGame(createGameRequest);

                var gameServerAddress1 = createResponse.Address;
                Console.WriteLine("Created room " + roomName + " on GS: " + gameServerAddress1);

                // move 1st client to GS: 
                this.ConnectAndAuthenticate(client1, gameServerAddress1, client1.UserId);

                client1.CreateGame(createGameRequest);


                client1.SendRequest(new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {(byte)ParameterKey.ActorNr, -1 },
                        {(byte)ParameterKey.Properties, new Hashtable { { (byte)GameParameter.MaxPlayers, "string"} }} 
                    }
                });

                Assert.That(client1.TryWaitForOperationResponse(this.WaitTimeout, out _), Is.True);

                client2.JoinGame(roomName);
                // move 2nd client to GS: 
                this.ConnectAndAuthenticate(client2, gameServerAddress1, client2.UserId);

                client2.JoinRoom(roomName, null, 0, new RoomOptions(), false, true);
            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [Test]
        public void Props_SetPropsWithNullOrEmptyPropertiesCollection()
        {
            UnifiedTestClient client1 = null;

            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                // open game
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);

                var createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                };
                var createResponse = client1.CreateGame(createGameRequest);

                var gameServerAddress1 = createResponse.Address;
                // move 1st client to GS: 
                this.ConnectAndAuthenticate(client1, gameServerAddress1);

                client1.CreateGame(createGameRequest);

                client1.SendRequestAndWaitForResponse(new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {(byte)ParameterKey.ActorNr, -1 },
                        {(byte)ParameterKey.Properties, new Hashtable()}
                    }
                }, ErrorCode.Ok);

                client1.SendRequestAndWaitForResponse(new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {(byte)ParameterKey.ActorNr, -1 },
                        {(byte)ParameterKey.Properties, null }
                    }
                }, ErrorCode.InvalidOperation);

                DisposeClients(client1);

                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                // open game
                roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);

                createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                };
                createResponse = client1.CreateGame(createGameRequest);

                gameServerAddress1 = createResponse.Address;
                // move 1st client to GS: 
                this.ConnectAndAuthenticate(client1, gameServerAddress1);

                client1.CreateGame(createGameRequest);


                client1.SendRequestAndWaitForResponse(new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {(byte)ParameterKey.ActorNr, 1 },
                        {(byte)ParameterKey.Properties, new Hashtable()}
                    }
                }, ErrorCode.Ok);

                client1.SendRequestAndWaitForResponse(new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {(byte)ParameterKey.ActorNr, 1 },
                        {(byte)ParameterKey.Properties, null}
                    }
                }, ErrorCode.InvalidOperation);
            }
            finally
            {
                DisposeClients(client1);
            }
        }

        [Test]
        public void Props_SetNonExistingPropUsingCAS()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;

            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                // open game
                string roomName = MethodBase.GetCurrentMethod().Name + Guid.NewGuid().ToString().Substring(0, 6);

                var createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                };
                var createResponse = client1.CreateGame(createGameRequest);

                var gameServerAddress1 = createResponse.Address;
                Console.WriteLine("Created room " + roomName + " on GS: " + gameServerAddress1);

                // move 1st client to GS: 
                this.ConnectAndAuthenticate(client1, gameServerAddress1, client1.UserId);

                client1.CreateGame(createGameRequest);

                client1.SendRequestAndWaitForResponse(new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {(byte)ParameterKey.ExpectedValues, new Hashtable {{"propertyKey", null}} },
                        {(byte)ParameterKey.Properties, new Hashtable{{"propertyKey", "value"}} }
                    }
                });

                client2.JoinGame(roomName);
                // move 2nd client to GS: 
                this.ConnectAndAuthenticate(client2, gameServerAddress1);

                var joinResponse = client2.JoinGame(roomName);
                Assert.That(joinResponse.GameProperties.Contains("propertyKey"), Is.True);
                Assert.That(joinResponse.GameProperties["propertyKey"], Is.EqualTo("value"));
            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [Test]
        public void Props_SetWrongValueForNonExistingPropertyTest()
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

                var setPropertiesRequest = new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,

                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.ExpectedValues, new Hashtable
                        {
                            {(byte) GameParameter.ExpectedUsers, null},
                            {"propertyId", null}
                        }},
                        {ParameterCode.Properties, new Hashtable
                        {
                            {(byte) GameParameter.ExpectedUsers, new [] {this.Player2, this.Player1, this.Player3, "Player4"}},
                            {"propertyKey", "value"}
                        }}
                    }
                };

                Thread.Sleep(10);
                masterClient1.SendRequestAndWaitForResponse(setPropertiesRequest, ErrorCode.InvalidOperation);


                joinRequest.JoinMode = JoinModeConstants.JoinOnly;
                joinRequest.AddUsers = null;
                masterClient2.JoinGame(joinRequest);

                this.ConnectAndAuthenticate(masterClient2, joinResponse1.Address);
                var joinGameResponse = masterClient2.JoinGame(joinRequest);

                Assert.That(joinGameResponse.GameProperties.Contains((byte) GameParameter.ExpectedUsers), Is.False);
                Assert.That(joinGameResponse.GameProperties.Contains("propertyId"), Is.False);

                Thread.Sleep(100);
                lobbyStatsResponse = masterClient3.GetLobbyStats(null, null);
                Assert.AreEqual(2, lobbyStatsResponse.PeerCount[0]);
            }
            finally
            {
                DisposeClients(masterClient1, masterClient2, masterClient3);
            }
        }

        [Test]
        public void Props_GetActorPropertiesUsingNonUniqList()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;

            const int PlayerTTL = 3000;
            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                client1.EventQueueClear();
                client2.EventQueueClear();

                client1.OperationResponseQueueClear();
                client2.OperationResponseQueueClear();

                // open game
                string roomName = MethodBase.GetCurrentMethod().Name + Guid.NewGuid().ToString().Substring(0, 6);

                var createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    GameProperties = new Hashtable { { (byte)GameParameter.PlayerTTL, PlayerTTL } }
                };
                var createResponse = client1.CreateGame(createGameRequest);

                var gameServerAddress1 = createResponse.Address;
                Console.WriteLine("Created room " + roomName + " on GS: " + gameServerAddress1);

                // move 1st client to GS: 
                this.ConnectAndAuthenticate(client1, gameServerAddress1, client1.UserId);

                client1.CreateGame(createGameRequest);

                client2.JoinGame(roomName);
                // move 2nd client to GS: 
                this.ConnectAndAuthenticate(client2, gameServerAddress1, client2.UserId);

                var joinResponse = client2.JoinRoom(roomName, null, 0, new RoomOptions(), false, true);

                //check that properties contain PlayerTTL wel-known property

                Assert.That(joinResponse.GameProperties.Contains((byte)GameParameter.PlayerTTL));
                Assert.That(joinResponse.GameProperties[((byte)GameParameter.PlayerTTL)], Is.EqualTo(PlayerTTL));

                Thread.Sleep(100);

                var response = client2.SendRequestAndWaitForResponse(new OperationRequest
                {
                    OperationCode = OperationCode.GetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        { ParameterCode.ActorList, new[] {2, 2, 1, 1} },
                        { ParameterCode.Properties, PropertyTypeFlag.Actor },
                    }
                });

                var actorProperties = (Hashtable)response[ParameterCode.PlayerProperties];
                Assert.That(actorProperties.Count, Is.EqualTo(2));
            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [Test]
        public void Props_GetActorPropertiesUsingWrongIdsInList()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;

            const int PlayerTTL = 3000;
            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                client1.EventQueueClear();
                client2.EventQueueClear();

                client1.OperationResponseQueueClear();
                client2.OperationResponseQueueClear();

                // open game
                string roomName = MethodBase.GetCurrentMethod().Name + Guid.NewGuid().ToString().Substring(0, 6);

                var createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    GameProperties = new Hashtable { { (byte)GameParameter.PlayerTTL, PlayerTTL } }
                };
                var createResponse = client1.CreateGame(createGameRequest);

                var gameServerAddress1 = createResponse.Address;
                Console.WriteLine("Created room " + roomName + " on GS: " + gameServerAddress1);

                // move 1st client to GS: 
                this.ConnectAndAuthenticate(client1, gameServerAddress1, client1.UserId);

                client1.CreateGame(createGameRequest);


                client2.JoinGame(roomName);
                // move 2nd client to GS: 
                this.ConnectAndAuthenticate(client2, gameServerAddress1, client2.UserId);

                var joinResponse = client2.JoinRoom(roomName, null, 0, new RoomOptions(), false, true);

                //check that properties contain PlayerTTL wel-known property

                Assert.That(joinResponse.GameProperties.Contains((byte)GameParameter.PlayerTTL));
                Assert.That(joinResponse.GameProperties[((byte)GameParameter.PlayerTTL)], Is.EqualTo(PlayerTTL));

                Thread.Sleep(100);

                var response = client2.SendRequestAndWaitForResponse(new OperationRequest
                {
                    OperationCode = OperationCode.GetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        { ParameterCode.ActorList, new[] {1110, 1110, 11, 2221} },
                        { ParameterCode.Properties, (byte)0x02 },
                    }
                });

                var actorProperties = (Hashtable)response[ParameterCode.PlayerProperties];
                Assert.That(actorProperties.Count, Is.EqualTo(0));
            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [Test]
        public void Props_GetActorPropertiesUsingNullKeysInList()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;

            const int PlayerTTL = 3000;
            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                client1.EventQueueClear();
                client2.EventQueueClear();

                client1.OperationResponseQueueClear();
                client2.OperationResponseQueueClear();

                // open game
                string roomName = MethodBase.GetCurrentMethod().Name + Guid.NewGuid().ToString().Substring(0, 6);

                var createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    GameProperties = new Hashtable { { (byte)GameParameter.PlayerTTL, PlayerTTL }, {"x", "y"}, {"y", "x"} }
                };
                var createResponse = client1.CreateGame(createGameRequest);

                var gameServerAddress1 = createResponse.Address;
                Console.WriteLine("Created room " + roomName + " on GS: " + gameServerAddress1);

                // move 1st client to GS: 
                this.ConnectAndAuthenticate(client1, gameServerAddress1, client1.UserId);

                client1.CreateGame(createGameRequest);

                client2.JoinGame(roomName);

                // move 2nd client to GS: 
                this.ConnectAndAuthenticate(client2, gameServerAddress1, client2.UserId);

                var joinResponse = client2.JoinRoom(roomName, null, 0, new RoomOptions(), false, true);

                //check that properties contain PlayerTTL wel-known property

                Assert.That(joinResponse.GameProperties.Contains((byte)GameParameter.PlayerTTL));
                Assert.That(joinResponse.GameProperties[((byte)GameParameter.PlayerTTL)], Is.EqualTo(PlayerTTL));

                Thread.Sleep(100);

                var response = client2.SendRequestAndWaitForResponse(new OperationRequest
                {
                    OperationCode = OperationCode.GetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        { ParameterCode.ActorList, new[] {1} },
                        { ParameterCode.GameProperties, new object[] {null, "x", null, "y"} },
                        { ParameterCode.PlayerProperties, new object[] {null,  null} },
                        { ParameterCode.Properties, (byte)PropertyType.GameAndActor }
                    }
                });

                var actorsProperties = (Hashtable)response[ParameterCode.PlayerProperties];
                Assert.That(actorsProperties.Count, Is.EqualTo(1));
                Assert.That(((Hashtable)actorsProperties[1]).Count, Is.EqualTo(0));
                var gameProperties = (Hashtable)response[ParameterCode.GameProperties];
                Assert.That(gameProperties.Count, Is.EqualTo(2));
            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [Test]
        public void Props_SetActorPropertiesCASFailureTest()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;

            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                client1.EventQueueClear();
                client2.EventQueueClear();

                client1.OperationResponseQueueClear();
                client2.OperationResponseQueueClear();

                // open game
                string roomName = MethodBase.GetCurrentMethod().Name + Guid.NewGuid().ToString().Substring(0, 6);

                var createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    ActorProperties = new Hashtable { {"x", "y"}, {"y", "x"} },

                };
                var createResponse = client1.CreateGame(createGameRequest);

                var gameServerAddress1 = createResponse.Address;
                Console.WriteLine("Created room " + roomName + " on GS: " + gameServerAddress1);

                // move 1st client to GS: 
                this.ConnectAndAuthenticate(client1, gameServerAddress1, client1.UserId);

                client1.CreateGame(createGameRequest);

                var joinResponse = client2.JoinGame(roomName);

                // move 2nd client to GS: 
                this.ConnectAndAuthenticate(client2, joinResponse.Address, client2.UserId);

                client2.JoinRoom(roomName, null, 0, new RoomOptions(), false, true);

                client1.SendRequestAndWaitForResponse(new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {(byte)ParameterKey.ExpectedValues, new Hashtable {{"x", "wrong"}} },
                        {(byte)ParameterKey.Properties, new Hashtable{{"x", "new"}} }
                    }
                }, ErrorCode.InvalidOperation);

                Thread.Sleep(100);

                var response = client2.SendRequestAndWaitForResponse(new OperationRequest
                {
                    OperationCode = OperationCode.GetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        { ParameterCode.ActorList, new[] {1} },
                        { ParameterCode.Properties, (byte)PropertyType.Actor }
                    }
                });

                var actorsProperties = (Hashtable)response[ParameterCode.PlayerProperties];
                Assert.That(actorsProperties.Count, Is.EqualTo(1));
                var actor1Properties = (Hashtable) actorsProperties[1];
                Assert.That(actor1Properties.Count, Is.EqualTo(2));
                Assert.That(actor1Properties["x"], Is.EqualTo("y"));
            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [Test]
        public void Props_WrongPlayerTTLType()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;

            const byte PlayerTTL = 30;
            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                client1.EventQueueClear();
                client2.EventQueueClear();

                client1.OperationResponseQueueClear();
                client2.OperationResponseQueueClear();

                // open game
                string roomName = MethodBase.GetCurrentMethod().Name + Guid.NewGuid().ToString().Substring(0, 6);

                var createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                    GameProperties = new Hashtable { { (byte)GameParameter.PlayerTTL, PlayerTTL } }
                };
                var createResponse = client1.CreateGame(createGameRequest);

                var gameServerAddress1 = createResponse.Address;

                // move 1st client to GS: 
                this.ConnectAndAuthenticate(client1, gameServerAddress1, client1.UserId);

                client1.CreateGame(createGameRequest);


                client2.JoinGame(roomName);
                // move 2nd client to GS: 
                this.ConnectAndAuthenticate(client2, gameServerAddress1, client2.UserId);

                var joinResponse = client2.JoinRoom(roomName, null, 0, new RoomOptions(), false, true);

                //check that properties contain PlayerTTL wel-known property

                Assert.That(joinResponse.GameProperties.Contains((byte)GameParameter.PlayerTTL));
                Assert.That(joinResponse.GameProperties[((byte)GameParameter.PlayerTTL)], Is.EqualTo(PlayerTTL));

                client2.SendRequestAndWaitForResponse(new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {
                            ParameterCode.Properties, new Hashtable { { (byte)GameParameter.PlayerTTL, PlayerTTL } }
                        },
                    }
                });

                Thread.Sleep(100);

                client2.Disconnect();
            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [Test]
        public void Props_WrongWebFlagsType()
        {
            TestBody(1000f, 100);
            TestBody<short>(1000, 100);
            TestBody(1000, 100);
            TestBody(1000L, 100);
            TestBody("xxxx", "100");

            // we need to start from scratch every time because server disconnects us in case of error
            void TestBody<T>(T wrongValue, T rightValue)
            {
                UnifiedTestClient client1 = null;
                UnifiedTestClient client2 = null;

                const byte PlayerTTL = 30;
                try
                {
                    client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                    client2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                    client1.EventQueueClear();
                    client2.EventQueueClear();

                    client1.OperationResponseQueueClear();
                    client2.OperationResponseQueueClear();

                    // open game
                    string roomName = MethodBase.GetCurrentMethod().Name + Guid.NewGuid().ToString().Substring(0, 6);

                    var createGameRequest = new CreateGameRequest
                    {
                        GameId = roomName,
                        CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                        GameProperties = new Hashtable {{(byte) GameParameter.PlayerTTL, PlayerTTL}}
                    };
                    var createResponse = client1.CreateGame(createGameRequest);

                    var gameServerAddress1 = createResponse.Address;

                    // move 1st client to GS: 
                    this.ConnectAndAuthenticate(client1, gameServerAddress1, client1.UserId);

                    client1.CreateGame(createGameRequest);


                    client2.JoinGame(roomName);
                    // move 2nd client to GS: 
                    this.ConnectAndAuthenticate(client2, gameServerAddress1, client2.UserId);

                    client2.JoinRoom(roomName, null, 0, new RoomOptions(), false, true);

                    client2.SendRequestAndWaitForResponse(new OperationRequest
                    {
                        OperationCode = OperationCode.SetProperties,
                        Parameters = new Dictionary<byte, object>
                        {
                            {
                                ParameterCode.Properties, new Hashtable {{(byte) GameParameter.PlayerTTL, PlayerTTL}}
                            },
                            {
                                ParameterCode.EventForward, rightValue
                            }
                        }
                    });

                    client2.SendRequestAndWaitForResponse(new OperationRequest
                    {
                        OperationCode = OperationCode.SetProperties,
                        Parameters = new Dictionary<byte, object>
                        {
                            {
                                ParameterCode.Properties, new Hashtable {{(byte) GameParameter.PlayerTTL, PlayerTTL}}
                            },
                            {
                                ParameterCode.EventForward, wrongValue
                            }
                        }
                    }, ErrorCode.InvalidOperation);
                }
                finally
                {
                    DisposeClients(client1, client2);
                }
            }
        }

        [Test]
        public void Props_MasterClientIdNotInt()
        {
            TestBody(1);
            TestBody(1.0);
            TestBody((byte)1);

            // we need to start from scratch every time because server disconnects us in case of error
            void TestBody<T>(T masterClientId)
            {
                UnifiedTestClient client1 = null;
                UnifiedTestClient client2 = null;

                const byte PlayerTTL = 30;
                try
                {
                    client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                    client2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                    client1.EventQueueClear();
                    client2.EventQueueClear();

                    client1.OperationResponseQueueClear();
                    client2.OperationResponseQueueClear();

                    // open game
                    string roomName = MethodBase.GetCurrentMethod().Name + Guid.NewGuid().ToString().Substring(0, 6);

                    var createGameRequest = new CreateGameRequest
                    {
                        GameId = roomName,
                        CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                        GameProperties = new Hashtable {{(byte) GameParameter.PlayerTTL, PlayerTTL}}
                    };
                    var createResponse = client1.CreateGame(createGameRequest);

                    var gameServerAddress1 = createResponse.Address;

                    // move 1st client to GS: 
                    this.ConnectAndAuthenticate(client1, gameServerAddress1, client1.UserId);

                    client1.CreateGame(createGameRequest);


                    client2.JoinGame(roomName);
                    // move 2nd client to GS: 
                    this.ConnectAndAuthenticate(client2, gameServerAddress1, client2.UserId);

                    client2.JoinRoom(roomName, null, 0, new RoomOptions(), false, true);

                    client2.SendRequestAndWaitForResponse(new OperationRequest
                    {
                        OperationCode = OperationCode.SetProperties,
                        Parameters = new Dictionary<byte, object>
                        {
                            {
                                ParameterCode.Properties, new Hashtable {{(byte) GameParameter.MasterClientId, masterClientId } }
                            },
                        }
                    });
                }
                finally
                {
                    DisposeClients(client1, client2);
                }
            }
        }


        [Test]
        public void Props_SetTooBigPropertiesInOneGo()
        {
            if (this.connectPolicy.IsOnline)
            {
                Assert.Ignore("This is an offline test");
            }

            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;

            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);


                // open game
                string roomName = MethodBase.GetCurrentMethod().Name + Guid.NewGuid().ToString().Substring(0, 6);

                var createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    GameProperties = new Hashtable { { (byte)GameParameter.EmptyRoomTTL, 123 } }// we check that correct ttl is set
                };
                var createResponse = client1.CreateGame(createGameRequest);

                var gameServerAddress1 = createResponse.Address;
                Console.WriteLine("Created room " + roomName + " on GS: " + gameServerAddress1);

                // move 1st client to GS: 
                this.ConnectAndAuthenticate(client1, gameServerAddress1, client1.UserId);

                client1.CreateGame(createGameRequest);

                client1.WaitForEvent(EventCode.Join);

                client1.SendRequest(new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Properties, new Hashtable
                            {
                                {1, new byte[900]}
                            }
                        }
                    }
                });

                client2.JoinGame(roomName);
                // move 2nd client to GS: 
                this.ConnectAndAuthenticate(client2, gameServerAddress1, client2.UserId);

                client2.JoinGame(roomName, ErrorCode.GameClosed);

            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [Test]
        public void Props_SetTooBigPropertiesInManyRequests()
        {
            if (this.connectPolicy.IsOnline)
            {
                Assert.Ignore("This is an offline test");
            }

            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;

            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);


                // open game
                string roomName = MethodBase.GetCurrentMethod().Name + Guid.NewGuid().ToString().Substring(0, 6);

                var createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    GameProperties = new Hashtable { { (byte)GameParameter.EmptyRoomTTL, 123 } }// we check that correct ttl is set
                };
                var createResponse = client1.CreateGame(createGameRequest);

                var gameServerAddress1 = createResponse.Address;
                Console.WriteLine("Created room " + roomName + " on GS: " + gameServerAddress1);

                // move 1st client to GS: 
                this.ConnectAndAuthenticate(client1, gameServerAddress1, client1.UserId);

                client1.CreateGame(createGameRequest);

                client1.WaitForEvent(EventCode.Join);

                for (int i = 0; i < 10; ++i)
                {
                    client1.SendRequest(new OperationRequest
                    {
                        OperationCode = OperationCode.SetProperties,
                        Parameters = new Dictionary<byte, object>
                        {
                            {ParameterCode.Properties, new Hashtable { {i, new byte[80]}}}
                        },
                    }
                    );
                }

                this.UpdateTokensGSAndGame(client2, "localhost", roomName);
                // move 2nd client to GS: 
                this.ConnectAndAuthenticate(client2, gameServerAddress1);

                client2.JoinGame(roomName, ErrorCode.GameClosed);
            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [Test]
        public void Props_SetTooBigPropertiesInActorProperties()
        {
            if (this.connectPolicy.IsOnline)
            {
                Assert.Ignore("This is an offline test");
            }

            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;
            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);


                // open game
                string roomName = MethodBase.GetCurrentMethod().Name + Guid.NewGuid().ToString().Substring(0, 6);

                var createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    GameProperties = new Hashtable { { (byte)GameParameter.EmptyRoomTTL, 123 } }// we check that correct ttl is set
                };
                var createResponse = client1.CreateGame(createGameRequest);

                var gameServerAddress1 = createResponse.Address;
                Console.WriteLine("Created room " + roomName + " on GS: " + gameServerAddress1);

                // move 1st client to GS: 
                this.ConnectAndAuthenticate(client1, gameServerAddress1, client1.UserId);

                client1.CreateGame(createGameRequest);

                client1.WaitForEvent(EventCode.Join);

                for (int i = 0; i < 10; ++i)
                {
                    client1.SendRequest(new OperationRequest
                    {
                        OperationCode = OperationCode.SetProperties,
                        Parameters = new Dictionary<byte, object>
                        {
                            {ParameterCode.Properties, new Hashtable { {i, new byte[80]}}},
                            {ParameterCode.ActorNr, 1}
                        },
                    }
                    );
                }

                this.UpdateTokensGSAndGame(client2, "localhost", roomName);
                // move 2nd client to GS: 
                this.ConnectAndAuthenticate(client2, gameServerAddress1, client2.UserId);

                client2.JoinGame(roomName, ErrorCode.GameClosed);
            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [Test]
        public void Props_CreatingGameWithTooBigProperties()
        {
            UnifiedTestClient client1 = null;

            if (this.connectPolicy.IsOnline)
            {
                Assert.Ignore("This is an offline test");
            }

            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                // open game
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);

                var createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    GameProperties = new Hashtable { { "xxx", new byte[100_000] } }
                };
                var createResponse = client1.CreateGame(createGameRequest);

                var gameServerAddress1 = createResponse.Address;
                Console.WriteLine("Created room " + roomName + " on GS: " + gameServerAddress1);

                // move 1st client to GS: 
                this.ConnectAndAuthenticate(client1, gameServerAddress1);

                client1.CreateGame(createGameRequest, ErrorCode.InvalidOperation);

                DisposeClients(client1);

                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                this.UpdateTokensGSAndGame(client1, "localhost", roomName);

                // move 1st client to GS: 
                this.ConnectAndAuthenticate(client1, gameServerAddress1);
                createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    GameProperties = new Hashtable { { "xxx", new byte[30_000] } },
                    ActorProperties = new Hashtable { { "yyy", new byte[30_000]} }
                };
                client1.CreateGame(createGameRequest, ErrorCode.InvalidOperation);
                DisposeClients(client1);

                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                this.UpdateTokensGSAndGame(client1, "localhost", roomName);
                // move 1st client to GS: 
                this.ConnectAndAuthenticate(client1, gameServerAddress1);
                roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);
                createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    ActorProperties = new Hashtable { { "yyy", new byte[60_000]} }
                };
                client1.CreateGame(createGameRequest, ErrorCode.InvalidOperation);

            }
            finally
            {
                DisposeClients(client1);
            }
        }

        [Test]
        public void Props_JoiningWithTooBigProperties()
        {
            if (this.connectPolicy.IsOnline)
            {
                Assert.Ignore("This is an offline test");
            }

            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;

            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);


                // open game
                string roomName = MethodBase.GetCurrentMethod().Name + Guid.NewGuid().ToString().Substring(0, 6);

                var createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    GameProperties = new Hashtable { { (byte)GameParameter.EmptyRoomTTL, 123 } }// we check that correct ttl is set
                };
                var createResponse = client1.CreateGame(createGameRequest);

                var gameServerAddress1 = createResponse.Address;
                Console.WriteLine("Created room " + roomName + " on GS: " + gameServerAddress1);

                // move 1st client to GS: 
                this.ConnectAndAuthenticate(client1, gameServerAddress1, client1.UserId);

                client1.CreateGame(createGameRequest);

                client1.WaitForEvent(EventCode.Join);

                client2.JoinGame(roomName);
                // move 2nd client to GS: 
                this.ConnectAndAuthenticate(client2, gameServerAddress1, client2.UserId);

                client2.JoinGame(
                    new JoinGameRequest
                    {
                        GameId = roomName,
                        GameProperties = new Hashtable { { "xxx", new byte[100_000]} }
                    }
                    , ErrorCode.InvalidOperation);
            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [Test]
        public void Props_TooBigAfterCreationAndJoining()// we create with quite big properties and then join with also big properties
        {
            if (this.connectPolicy.IsOnline)
            {
                Assert.Ignore("This is an offline test");
            }

            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;
            UnifiedTestClient client3 = null;

            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                client3 = this.CreateMasterClientAndAuthenticate(this.Player3);


                // open game
                string roomName = MethodBase.GetCurrentMethod().Name + Guid.NewGuid().ToString().Substring(0, 6);

                var createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    GameProperties = new Hashtable { { "xxx", new byte[450]} }
                };
                var createResponse = client1.CreateGame(createGameRequest);

                var gameServerAddress1 = createResponse.Address;
                Console.WriteLine("Created room " + roomName + " on GS: " + gameServerAddress1);

                // move 1st client to GS: 
                this.ConnectAndAuthenticate(client1, gameServerAddress1, client1.UserId);

                client1.CreateGame(createGameRequest);

                client1.WaitForEvent(EventCode.Join);

                client2.JoinGame(roomName);
                // move 2nd client to GS: 
                this.ConnectAndAuthenticate(client2, gameServerAddress1, client2.UserId);

                client3.JoinGame(roomName);

                client2.JoinGame(
                    new JoinGameRequest
                    {
                        GameId = roomName,
                        ActorProperties = new Hashtable { { "yyy", new byte[450]} }
                    });

                this.ConnectAndAuthenticate(client3, gameServerAddress1, client2.UserId);
                client3.JoinGame(roomName, ErrorCode.GameClosed);
            }
            finally
            {
                DisposeClients(client1, client2, client3);
            }
        }


        /// <summary>
        /// we test that after exceeding of some limit user can not create new properties
        /// </summary>
        [Test]
        public void Props_SetTooManyUniqProperties()
        {
            if (this.connectPolicy.IsOnline)
            {
                Assert.Ignore("This is an offline test");
            }

            UnifiedTestClient client1 = null;

            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);


                // open game
                string roomName = MethodBase.GetCurrentMethod().Name + Guid.NewGuid().ToString().Substring(0, 6);

                var createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    GameProperties = new Hashtable { { (byte)GameParameter.EmptyRoomTTL, 123 } }// we check that correct ttl is set
                };
                var createResponse = client1.CreateGame(createGameRequest);

                var gameServerAddress1 = createResponse.Address;
                Console.WriteLine("Created room " + roomName + " on GS: " + gameServerAddress1);

                // move 1st client to GS: 
                this.ConnectAndAuthenticate(client1, gameServerAddress1, client1.UserId);

                client1.CreateGame(createGameRequest);

                client1.WaitForEvent(EventCode.Join);

                // test apps set limit 100 for amount of properties and 1000 for size

                for (int j = 0; j < 10; ++j)
                {
                    var h = new Hashtable();
                    for (int i = j * 100; i < j * 100 + 100; ++i)
                    {
                        h.Add((short)i, (short)i);
                    }

                    client1.SendRequestAndWaitForResponse(new OperationRequest
                        {
                            OperationCode = OperationCode.SetProperties,
                            Parameters = new Dictionary<byte, object>
                            {
                                {ParameterCode.Properties, h},
                                {ParameterCode.TargetActorNr, 1}
                            },
                        }
                    );
                }

                client1.SendRequestAndWaitForResponse(new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                            {
                                {ParameterCode.Properties, new Hashtable{{"key", "value"}}},
                            },
                }, 32743);
            }
            finally
            {
                DisposeClients(client1);
            }
        }

        /// <summary>
        /// in this test we do not use expected users, just generic properties
        /// </summary>
        [Test]
        public void Props_SetPropertiesFailedCASTest()
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
                        {"a", "b"},
                    },

                    AddUsers = new[] { this.Player2, this.Player3 }
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                var joinResponse1 = masterClient1.JoinGame(joinRequest);

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, joinResponse1.Address);
                masterClient1.JoinGame(joinRequest);

                masterClient1.WaitForEvent(EventCode.Join);

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
                        {ParameterCode.ExpectedValues, new Hashtable{{"a", "wrong"}} },
                        {ParameterCode.Properties, new Hashtable{{"a", "new"}}}
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
                        {ParameterCode.GameProperties, new object[]{"a"}}
                    }
                });

                var properties = (Hashtable)response[ParameterCode.GameProperties];

                Assert.That(properties.Count, Is.EqualTo(1));
                Assert.That(properties["a"], Is.EqualTo("b"));

                Thread.Sleep(100);
            }
            finally
            {
                DisposeClients(masterClient1, masterClient2);
            }
        }

        /// <summary>
        /// there is four cases we have to test:
        /// - PublishUserId == False
        /// - PublishUserId == true request does not send filter
        /// - PublishUserId == true request has filter but does not request userId
        /// - PublishUserId == true request has filter and requests userId
        /// </summary>
        #region GetProperties Test Group
        [Test]
        public void Props_GetUserIdUsingGetPropertiesPublishUserIdFalse()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;

            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                // open game
                string roomName = MethodBase.GetCurrentMethod().Name + Guid.NewGuid().ToString().Substring(0, 6);

                var createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    PublishUserId = false
                };
                var createResponse = client1.CreateGame(createGameRequest);

                var gameServerAddress1 = createResponse.Address;

                // move 1st client to GS: 
                this.ConnectAndAuthenticate(client1, gameServerAddress1, client1.UserId);

                client1.CreateGame(createGameRequest);

                client1.WaitForEvent(EventCode.Join);

                client2.JoinGame(roomName);
                // move 2nd client to GS: 
                this.ConnectAndAuthenticate(client2, gameServerAddress1, client2.UserId);

                client2.JoinGame(roomName);

                var getPropertiesResponse = client2.GetActorsProperties();

                Assert.That(getPropertiesResponse.ActorProperties.Count, Is.EqualTo(2));
                Assert.That(getPropertiesResponse.ActorProperties[1], Does.Not.ContainKey(ActorProperties.UserId));
                Assert.That(getPropertiesResponse.ActorProperties[2], Does.Not.ContainKey(ActorProperties.UserId));

                var response = client2.SendRequestAndWaitForResponse(new OperationRequest
                {
                    OperationCode = OperationCode.GetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Properties, PropertyTypeFlag.Actor},
                        {ParameterCode.PlayerProperties, new object[]{ActorProperties.UserId}}
                    }
                });

                var properties = (Hashtable)response[ParameterCode.PlayerProperties];

                Assert.That(properties.Count, Is.EqualTo(2));
                Assert.That(properties[1], Does.Not.ContainKey(ActorProperties.UserId));
                Assert.That(properties[2], Does.Not.ContainKey(ActorProperties.UserId));

            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [Test]
        public void Props_GetUserIdUsingGetPropertiesPublishUserIdTrue()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;

            try
            {
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                // open game
                string roomName = MethodBase.GetCurrentMethod().Name + Guid.NewGuid().ToString().Substring(0, 6);

                var createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    PublishUserId = true
                };
                var createResponse = client1.CreateGame(createGameRequest);

                var gameServerAddress1 = createResponse.Address;

                // move 1st client to GS: 
                this.ConnectAndAuthenticate(client1, gameServerAddress1, client1.UserId);

                client1.CreateGame(createGameRequest);

                client1.WaitForEvent(EventCode.Join);

                client2.JoinGame(roomName);
                // move 2nd client to GS: 
                this.ConnectAndAuthenticate(client2, gameServerAddress1, client2.UserId);

                client2.JoinGame(roomName);

                var getPropertiesResponse = client2.GetActorsProperties();

                Assert.That(getPropertiesResponse.ActorProperties.Count, Is.EqualTo(2));
                Assert.That(getPropertiesResponse.ActorProperties[1], Does.ContainKey(ActorProperties.UserId));
                Assert.That(getPropertiesResponse.ActorProperties[2], Does.ContainKey(ActorProperties.UserId));

                var response = client2.SendRequestAndWaitForResponse(new OperationRequest
                {
                    OperationCode = OperationCode.GetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Properties, PropertyTypeFlag.Actor},
                        {ParameterCode.PlayerProperties, new object[]{ActorProperties.UserId}}
                    }
                });

                var properties = (Hashtable)response[ParameterCode.PlayerProperties];
                Assert.That(properties.Count, Is.EqualTo(2));

                Assert.That(properties[1], Does.ContainKey(ActorProperties.UserId));
                Assert.That(properties[2], Does.ContainKey(ActorProperties.UserId));

                response = client2.SendRequestAndWaitForResponse(new OperationRequest
                {
                    OperationCode = OperationCode.GetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Properties, PropertyTypeFlag.Actor},
                        {ParameterCode.PlayerProperties, new object[]{(byte)1, (byte)2}}
                    }
                });

                properties = (Hashtable)response[ParameterCode.PlayerProperties];

                Assert.That(properties.Count, Is.EqualTo(2));
                Assert.That(properties[1], Does.Not.ContainKey(ActorProperties.UserId));
                Assert.That(properties[2], Does.Not.ContainKey(ActorProperties.UserId));

            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        #endregion GetProperties Test Group

        #endregion
    }
}
