// --------------------------------------------------------------------------------------------------------------------
// <copyright file="BasicUseCases.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------


using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Web.Script.Serialization;

using Newtonsoft.Json;

using NUnit.Framework;

using Photon.Common;
using Photon.Hive.Caching;
using Photon.Hive.Common.Lobby;
using Photon.Hive.Events;
using Photon.Hive.Operations;
using Photon.Hive.Plugin;
using Photon.Hive.Serialization;
using Photon.Hive.WebRpc;
using Photon.Hive.WebRpc.Configuration;
using Photon.SocketServer;

using LiteEventCode = Photon.Realtime.EventCode;
using LiteOpCode = Photon.Realtime.OperationCode;
using ReceiverGroup = Photon.Hive.Operations.ReceiverGroup;
using SendParameters = Photon.SocketServer.SendParameters;


namespace Photon.Hive.Tests.Disconnected
{
    /// <summary>
    ///   The basic use cases.
    /// </summary>
    [TestFixture]
    [Parallelizable(ParallelScope.Self)]
    public class BasicUseCases
    {
        #region Constants and Fields

        /// <summary>
        ///   The wait timeout.
        /// </summary>
        private const int WaitTimeout = 5000;

        #endregion

        #region Public Methods

        [SetUp]
        public virtual void Setup()
        {
            var app = new HiveApplication();
            app.OnStart("NUnit", "Lite", new DummyApplicationSink(), null, null, null, string.Empty);
        }

        [TearDown]
        public void TestCleanUp()
        {
            TestGameCache.Instance.TryGetRoomWithoutReference("testGame", out var room);
            ((TestGame) room)?.WaitForDispose();
        }

        [Test]
        public void BiggerThenMaxEmptyRoomLiveTimeTest()
        {
            var peerOne = new DummyPeer();
            var litePeerOne = new TestHivePeer(peerOne.Protocol, peerOne);
            RoomReference room = null;

            const int maxRoomLiveTime = 1000;
            try
            {
                room = TestGameCache.Instance.GetRoomReference("testGame", null);
                Assert.AreEqual(maxRoomLiveTime, room.Room.MaxEmptyRoomTTL);

                // peer 1: join
                var request = GetJoinRequest();
                request.Parameters.Add((byte)ParameterKey.EmptyRoomLiveTime, 1000);

                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());
                Assert.IsTrue(peerOne.WaitForNextResponse(WaitTimeout));

                Assert.AreEqual(maxRoomLiveTime, room.Room.EmptyRoomLiveTime);

                room.Dispose();
                PeerHelper.SimulateDisconnect(litePeerOne);
                Thread.Sleep(2 * maxRoomLiveTime);
            }
            finally
            {
                room?.Dispose();

                if (litePeerOne.Connected)
                {
                    PeerHelper.SimulateDisconnect(litePeerOne);
                }
            }
        }

        [Test]
        public void JoinGameFailsIfGameNotExists()
        {
            var peerOne = new DummyPeer();
            var litePeerOne = new TestHivePeer(peerOne.Protocol, peerOne);

            try
            {
                // peer 1: create game
                var request = GetJoinGameRequest();
                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());
                Assert.IsTrue(peerOne.WaitForNextResponse(WaitTimeout));

                var responseList = peerOne.GetResponseList();
                Assert.AreEqual(1, responseList.Count);
                var response = responseList[0];
                Assert.AreEqual(32758, response.ReturnCode, response.DebugMessage);
            }
            finally
            {
                PeerHelper.SimulateDisconnect(litePeerOne);
            }
        }

        [Test]
        public void JoinGameOldCreateIfNoExists()
        {
            var peerOne = new DummyPeer();
            var litePeerOne = new TestHivePeer(peerOne.Protocol, peerOne);

            try
            {
                // peer 1: create game
                var request = GetJoinGameRequest();
                request[(byte)ParameterKey.CreateIfNotExists] = true;
                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());
                Assert.IsTrue(peerOne.WaitForNextResponse(WaitTimeout));

                var responseList = peerOne.GetResponseList();
                Assert.AreEqual(1, responseList.Count);
                var response = responseList[0];
                Assert.AreEqual(0, response.ReturnCode, response.DebugMessage);
            }
            finally
            {
                PeerHelper.SimulateDisconnect(litePeerOne);
            }
        }

        [Test]
        public void JoinGameOldRejoin()
        {
            var peerOne = new DummyPeer();
            var peerTwo = new DummyPeer();
            var litePeerOne = new TestHivePeer(peerTwo.Protocol, peerOne, "myuser1");
            var litePeerTwo = new TestHivePeer(peerTwo.Protocol, peerTwo, "myuser2");
            RoomReference room = null;
            TestGame game = null;

            try
            {
                // peer 1: create game
                var request = GetCreateGameRequest();
                request[(byte)ParameterKey.PlayerTTL] = -1;
                request[(byte)ParameterKey.EmptyRoomLiveTime] = 999;
                request[(byte)ParameterKey.ActorProperties] = new Hashtable { { "TestKey", "TestVal1" } };
                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());
                Assert.IsTrue(peerOne.WaitForNextResponse(WaitTimeout));

                var responseList = peerOne.GetResponseList();
                Assert.AreEqual(1, responseList.Count);
                var response = responseList[0];
                Assert.AreEqual(0, response.ReturnCode, response.DebugMessage);
                Assert.IsFalse(response.Parameters.ContainsKey((byte)ParameterKey.ActorProperties), "We don't expect any actor properties");

                request = GetJoinGameRequest();
                request[(byte)ParameterKey.ActorProperties] = new Hashtable { { "TestKey", "TestVal2" } };
                PeerHelper.InvokeOnOperationRequest(litePeerTwo, request, new SendParameters());
                Assert.IsTrue(peerTwo.WaitForNextResponse(WaitTimeout));
                responseList = peerTwo.GetResponseList();
                Assert.AreEqual(1, responseList.Count);
                response = responseList[0];
                Assert.AreEqual(0, response.ReturnCode, response.DebugMessage);
                var allActorProperties = (Hashtable)response.Parameters[(byte)ParameterKey.ActorProperties];
                Assert.AreEqual(1, allActorProperties.Count, "We only expect the properties of the first actor");
                var actorProperties = (Hashtable)allActorProperties[1];
                Assert.AreEqual("TestVal1", actorProperties["TestKey"], "Mismatch in properties of the first actor");

                room = TestGameCache.Instance.GetRoomReference("testGame", litePeerTwo);
                game = (TestGame)room.Room;

                PeerHelper.SimulateDisconnect(litePeerTwo);
                game.WaitRemovePeerFromGame();

                litePeerTwo = new TestHivePeer(peerTwo.Protocol, peerTwo, "myuser2");

                // peer 1: join game
                request = GetJoinGameRequest();
                request[(byte)ParameterKey.CreateIfNotExists] = true;
                request[(byte)ParameterKey.ActorNr] = 2;
                request[(byte)ParameterKey.JoinMode] = JoinModeConstants.RejoinOnly;
                PeerHelper.InvokeOnOperationRequest(litePeerTwo, request, new SendParameters());
                Assert.IsTrue(peerTwo.WaitForNextResponse(WaitTimeout));

                responseList = peerTwo.GetResponseList();
                Assert.AreEqual(1, responseList.Count);
                response = responseList[0];
                Assert.AreEqual(0, response.ReturnCode, response.DebugMessage);
                Assert.AreEqual(2, response.Parameters[(byte)ParameterKey.ActorNr], "ActorNr miss match.");
                allActorProperties = (Hashtable)response.Parameters[(byte)ParameterKey.ActorProperties];
                Assert.AreEqual(2, allActorProperties.Count, "We expect the properties of both actors");
                actorProperties = (Hashtable)allActorProperties[1];
                Assert.AreEqual("TestVal1", actorProperties["TestKey"], "Mismatch in properties of the first actor");
                actorProperties = (Hashtable)allActorProperties[2];
                Assert.AreEqual("TestVal2", actorProperties["TestKey"], "Mismatch in properties of the second actor");
            }
            finally
            {
                room?.Dispose();

                if (litePeerOne.Connected)
                {
                    PeerHelper.SimulateDisconnect(litePeerOne);
                }
                if (litePeerTwo.Connected)
                {
                    PeerHelper.SimulateDisconnect(litePeerTwo);
                }

                game?.WaitForDispose();
            }
        }

        [Test]
        public void CreateOrJoinGameWithExpectedPlugin()
        {
            var peerOne = new DummyPeer();
            var peerTwo = new DummyPeer();
            var litePeerOne = new TestHivePeer(peerOne.Protocol, peerOne);
            var litePeerTwo = new TestHivePeer(peerOne.Protocol, peerTwo);
            RoomReference room = null;

            try
            {
                // peer 1: create game - expected plugin mismatch error
                var createGameRequest = GetCreateGameRequest();
                createGameRequest[(byte)ParameterKey.Plugins] = new[] { "DefaultX" };
                PeerHelper.InvokeOnOperationRequest(litePeerOne, createGameRequest, new SendParameters());
                Assert.IsTrue(peerOne.WaitForNextResponse(WaitTimeout));

                var responseList = peerOne.GetResponseList();
                Assert.AreEqual(1, responseList.Count);
                var response = responseList[0];
                Assert.AreEqual((short)ErrorCode.PluginMismatch, response.ReturnCode, response.DebugMessage);

                PeerHelper.SimulateDisconnect(litePeerOne);

                // peer 1: join (CreateIfNotExists) - expected plugin mismatch error
                litePeerOne = new TestHivePeer(peerOne.Protocol, peerOne);
                var joinGameRequest = GetJoinGameRequest();
                joinGameRequest[(byte)ParameterKey.JoinMode] = (int)JoinModeConstants.CreateIfNotExists;
                joinGameRequest[(byte)ParameterKey.Plugins] = new[] { "DefaultX" };
                PeerHelper.InvokeOnOperationRequest(litePeerOne, joinGameRequest, new SendParameters());
                Assert.IsTrue(peerOne.WaitForNextResponse(WaitTimeout));

                responseList = peerOne.GetResponseList();
                Assert.AreEqual(1, responseList.Count);
                response = responseList[0];
                Assert.AreEqual((short)ErrorCode.PluginMismatch, response.ReturnCode, response.DebugMessage);
                PeerHelper.SimulateDisconnect(litePeerOne);
                TestGame game;
                if (TestGameCache.Instance.TryGetRoomReference("testGame", litePeerOne, out room))
                {
                    game = (TestGame)room.Room;
                    room.Dispose();
                    game.WaitForDispose();
                }

                // peer 1: create game - OK
                litePeerOne = new TestHivePeer(peerOne.Protocol, peerOne);
                createGameRequest = GetCreateGameRequest();
                createGameRequest[(byte)ParameterKey.Plugins] = new[] { "TestPlugin" };
                PeerHelper.InvokeOnOperationRequest(litePeerOne, createGameRequest, new SendParameters());
                Assert.IsTrue(peerOne.WaitForNextResponse(WaitTimeout));

                responseList = peerOne.GetResponseList();
                Assert.AreEqual(1, responseList.Count);
                response = responseList[0];
                Assert.AreEqual(0, response.ReturnCode, response.DebugMessage);

                // peer 1: join (plain) - expected plugin mismatch error
                joinGameRequest = GetJoinGameRequest();
                joinGameRequest[(byte)ParameterKey.Plugins] = new[] { "DefaultX" };
                PeerHelper.InvokeOnOperationRequest(litePeerTwo, joinGameRequest, new SendParameters());
                Assert.IsTrue(peerTwo.WaitForNextResponse(WaitTimeout));

                responseList = peerTwo.GetResponseList();
                Assert.AreEqual(1, responseList.Count);
                response = responseList[0];
                Assert.AreEqual((short)ErrorCode.PluginMismatch, response.ReturnCode, response.DebugMessage);

                // peer 1: join (plain) - OK
                joinGameRequest = GetJoinGameRequest();
                joinGameRequest[(byte)ParameterKey.Plugins] = new[] { "TestPlugin" };
                PeerHelper.InvokeOnOperationRequest(litePeerTwo, joinGameRequest, new SendParameters());
                Assert.IsTrue(peerTwo.WaitForNextResponse(WaitTimeout));

                responseList = peerTwo.GetResponseList();
                Assert.AreEqual(1, responseList.Count);
                response = responseList[0];
                Assert.AreEqual(0, response.ReturnCode, response.DebugMessage);

                room = TestGameCache.Instance.GetRoomReference("testGame", litePeerOne);
                game = (TestGame)room.Room;
                PeerHelper.SimulateDisconnect(litePeerOne);
                PeerHelper.SimulateDisconnect(litePeerTwo);
                room.Dispose();
                game.WaitForDispose();
            }
            finally
            {
                room?.Dispose();

                if (litePeerOne.Connected)
                {
                    PeerHelper.SimulateDisconnect(litePeerOne);
                }
                if (litePeerTwo.Connected)
                {
                    PeerHelper.SimulateDisconnect(litePeerTwo);
                }
            }
        }

        [Test]
        public void CreateGameWithErrorPlugin()
        {
            var peerOne = new DummyPeer();
            var peerTwo = new DummyPeer();
            var litePeerOne = new TestHivePeer(peerOne.Protocol, peerOne);
            var litePeerTwo = new TestHivePeer(peerOne.Protocol, peerTwo);
            RoomReference room = null;

            try
            {
                // peer 1: create game - expected plugin mismatch error
                var createGameRequest = GetCreateGameRequest();
                createGameRequest[(byte)ParameterKey.Plugins] = new[] { "ErrorPlugin" };
                PeerHelper.InvokeOnOperationRequest(litePeerOne, createGameRequest, new SendParameters());
                Assert.IsTrue(peerOne.WaitForNextResponse(WaitTimeout));

                var responseList = peerOne.GetResponseList();
                Assert.AreEqual(1, responseList.Count);
                var response = responseList[0];
                Assert.AreEqual((short)ErrorCode.PluginReportedError, response.ReturnCode, response.DebugMessage);

                Assert.That(TestGameCache.Instance.TryGetRoomReference("testGame", litePeerOne, out room), Is.False);

                //if (TestGameCache.Instance.TryGetRoomReference("testGame", litePeerOne, out room))
                //{
                //    PeerHelper.SimulateDisconnect(litePeerOne);

                //    var game = (TestGame) room.Room;
                //    room.Dispose();
                //    game.WaitForDispose();
                //}
            }
            finally
            {
                room?.Dispose();

                if (litePeerOne.Connected)
                {
                    PeerHelper.SimulateDisconnect(litePeerOne);
                }
                if (litePeerTwo.Connected)
                {
                    PeerHelper.SimulateDisconnect(litePeerTwo);
                }
            }
        }

        [Test]
        public void JoinGameNewCreateIfNoExists()
        {
            var peerOne = new DummyPeer();
            var litePeerOne = new TestHivePeer(peerOne.Protocol, peerOne);

            try
            {
                // peer 1: create game
                var request = GetJoinGameRequest();
                request[(byte)ParameterKey.JoinMode] = (int)JoinModeConstants.CreateIfNotExists;
                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());
                Assert.IsTrue(peerOne.WaitForNextResponse(WaitTimeout));

                var responseList = peerOne.GetResponseList();
                Assert.AreEqual(1, responseList.Count);
                var response = responseList[0];
                Assert.AreEqual(0, response.ReturnCode, response.DebugMessage);
            }
            finally
            {
                PeerHelper.SimulateDisconnect(litePeerOne);
            }
        }

        [Test]
        public void JoinGameNewRejoin()
        {
            var peerOne = new DummyPeer();
            var peerTwo = new DummyPeer();
            var litePeerOne = new TestHivePeer(peerTwo.Protocol, peerOne, "myuser1");
            var litePeerTwo = new TestHivePeer(peerTwo.Protocol, peerTwo, "myuser2");
            RoomReference room = null;
            TestGame game = null;

            try
            {
                // peer 1: create game
                var request = GetCreateGameRequest();
                request[(byte)ParameterKey.PlayerTTL] = -1;
                request[(byte)ParameterKey.CheckUserOnJoin] = true;
                request[(byte)ParameterKey.EmptyRoomLiveTime] = 999;
                request[(byte)ParameterKey.ActorProperties] = new Hashtable { { "TestKey", "TestVal1" } };
                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());
                Assert.IsTrue(peerOne.WaitForNextResponse(WaitTimeout));

                var responseList = peerOne.GetResponseList();
                Assert.AreEqual(1, responseList.Count);
                var response = responseList[0];
                Assert.AreEqual(0, response.ReturnCode, response.DebugMessage);
                Assert.IsFalse(response.Parameters.ContainsKey((byte)ParameterKey.ActorProperties), "We don't expect any actor properties");

                request = GetJoinGameRequest();
                request[(byte)ParameterKey.ActorProperties] = new Hashtable { { "TestKey", "TestVal2" } };
                PeerHelper.InvokeOnOperationRequest(litePeerTwo, request, new SendParameters());
                Assert.IsTrue(peerTwo.WaitForNextResponse(WaitTimeout));
                responseList = peerTwo.GetResponseList();
                Assert.AreEqual(1, responseList.Count);
                response = responseList[0];
                Assert.AreEqual(0, response.ReturnCode, response.DebugMessage);
                var allActorProperties = (Hashtable)response.Parameters[(byte)ParameterKey.ActorProperties];
                Assert.AreEqual(1, allActorProperties.Count, "We only expect the properties of the first actor");
                var actorProperties = (Hashtable)allActorProperties[1];
                Assert.AreEqual("TestVal1", actorProperties["TestKey"], "Mismatch in properties of the first actor");

                room = TestGameCache.Instance.GetRoomReference("testGame", litePeerTwo);
                game = (TestGame)room.Room;

                PeerHelper.SimulateDisconnect(litePeerTwo);
                game.WaitRemovePeerFromGame();

                litePeerTwo = new TestHivePeer(peerTwo.Protocol, peerTwo, "myuser2");

                // peer 1: join game
                request = GetJoinGameRequest();
                request[(byte)ParameterKey.JoinMode] = (int)JoinModeConstants.RejoinOrJoin;
                //request[(byte)ParameterKey.ActorNr] = 2;
                PeerHelper.InvokeOnOperationRequest(litePeerTwo, request, new SendParameters());
                Assert.IsTrue(peerTwo.WaitForNextResponse(WaitTimeout));

                responseList = peerTwo.GetResponseList();
                Assert.AreEqual(1, responseList.Count);
                response = responseList[0];
                Assert.AreEqual(0, response.ReturnCode, response.DebugMessage);
                Assert.AreEqual(2, response.Parameters[(byte)ParameterKey.ActorNr], "ActorNr miss match.");
                allActorProperties = (Hashtable)response.Parameters[(byte)ParameterKey.ActorProperties];
                Assert.AreEqual(2, allActorProperties.Count, "We expect the properties of both actors");
                actorProperties = (Hashtable)allActorProperties[1];
                Assert.AreEqual("TestVal1", actorProperties["TestKey"], "Mismatch in properties of the first actor");
                actorProperties = (Hashtable)allActorProperties[2];
                Assert.AreEqual("TestVal2", actorProperties["TestKey"], "Mismatch in properties of the second actor");
            }
            finally
            {
                room?.Dispose();

                if (litePeerOne.Connected)
                {
                    PeerHelper.SimulateDisconnect(litePeerOne);
                }
                if (litePeerTwo.Connected)
                {
                    PeerHelper.SimulateDisconnect(litePeerTwo);
                }

                game?.WaitForDispose();
            }
        }

        [Test]
        public void CreateGame()
        {
            var peerOne = new DummyPeer();
            var peerTwo = new DummyPeer();
            var litePeerOne = new TestHivePeer(peerOne.Protocol, peerOne);
            var litePeerTwo = new TestHivePeer(peerTwo.Protocol, peerTwo);

            try
            {
                // peer 1: create game
                var request = GetCreateGameRequest();
                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());
                Assert.IsTrue(peerOne.WaitForNextResponse(WaitTimeout));

                var responseList = peerOne.GetResponseList();
                Assert.AreEqual(1, responseList.Count);
                var response = responseList[0];
                Assert.AreEqual(0, response.ReturnCode);
                Assert.AreEqual(1, response.Parameters[(byte)ParameterKey.ActorNr]);

                // peer 1: receive own join event
                Assert.IsTrue(peerOne.WaitForNextEvent(WaitTimeout));
                var eventList = peerOne.GetEventList();
                Assert.AreEqual(1, eventList.Count);
                var eventData = eventList[0];
                Assert.AreEqual(LiteOpCode.Join, eventData.Code);
                Assert.AreEqual(1, eventData.Parameters[(byte)ParameterKey.ActorNr]);

                // peer 2: join game 
                request = GetJoinGameRequest();
                PeerHelper.InvokeOnOperationRequest(litePeerTwo, request, new SendParameters());
                Assert.IsTrue(peerTwo.WaitForNextResponse(WaitTimeout));

                responseList = peerTwo.GetResponseList();
                Assert.AreEqual(1, responseList.Count);
                response = responseList[0];
                Assert.AreEqual(0, response.ReturnCode, response.DebugMessage);
                Assert.AreEqual(2, response.Parameters[(byte)ParameterKey.ActorNr]);

                // peer 2: receive own join event
                Assert.IsTrue(peerTwo.WaitForNextEvent(WaitTimeout));
                eventList = peerTwo.GetEventList();
                Assert.AreEqual(1, eventList.Count);
                eventData = eventList[0];
                Assert.AreEqual(LiteOpCode.Join, eventData.Code);
                Assert.AreEqual(2, eventData.Parameters[(byte)ParameterKey.ActorNr]);

                // peer 1: receive join event of peer 2
                Assert.IsTrue(peerOne.WaitForNextEvent(WaitTimeout));
                eventList = peerOne.GetEventList();
                Assert.AreEqual(1, eventList.Count);
                eventData = eventList[0];
                Assert.AreEqual(LiteOpCode.Join, eventData.Code);
                Assert.AreEqual(2, eventData.Parameters[(byte)ParameterKey.ActorNr]);

                PeerHelper.SimulateDisconnect(litePeerTwo);
                litePeerTwo = new TestHivePeer(peerTwo.Protocol, peerTwo);

                // peer 2: join again
                request = GetJoinRequest();
                PeerHelper.InvokeOnOperationRequest(litePeerTwo, request, new SendParameters());
                Assert.IsTrue(peerTwo.WaitForNextResponse(WaitTimeout));
                responseList = peerTwo.GetResponseList();
                Assert.AreEqual(1, responseList.Count);

                // peer 2: do not receive own leave event, receive own join event
                Assert.IsTrue(peerTwo.WaitForNextEvent(WaitTimeout));
                eventList = peerTwo.GetEventList();
                Assert.AreEqual(1, eventList.Count);
                eventData = eventList[0];
                Assert.AreEqual(LiteOpCode.Join, eventData.Code);
                Assert.AreEqual(3, eventData.Parameters[(byte)ParameterKey.ActorNr]);

                // peer 1: receive leave and event of peer 2
                Assert.IsTrue(peerOne.WaitForNextEvent(WaitTimeout));
                eventList = peerOne.GetEventList();
                Assert.GreaterOrEqual(eventList.Count, 1);
                eventData = eventList[0];
                Assert.AreEqual(LiteOpCode.Leave, eventData.Code);
                Assert.AreEqual(2, eventData.Parameters[(byte)ParameterKey.ActorNr]);

                if (eventList.Count == 1)
                {
                    // waiting for join event
                    Assert.IsTrue(peerOne.WaitForNextEvent(WaitTimeout));
                    eventList = peerOne.GetEventList();
                    Assert.AreEqual(1, eventList.Count);
                    eventData = eventList[0];
                }
                else
                {
                    eventData = eventList[1];
                }

                Assert.AreEqual(LiteOpCode.Join, eventData.Code);
                Assert.AreEqual(3, eventData.Parameters[(byte)ParameterKey.ActorNr]);
            }
            finally
            {
                PeerHelper.SimulateDisconnect(litePeerOne);
                PeerHelper.SimulateDisconnect(litePeerTwo);
            }
        }

        [Test]
        public void ReInitGameTest()
        {
            var peerOne = new DummyPeer();
            var peerTwo = new DummyPeer();
            var litePeerOne = new TestHivePeer(peerOne.Protocol, peerOne);
            var litePeerTwo = new TestHivePeer(peerTwo.Protocol, peerTwo);
            const string gameName = "ReInitGame";
            try
            {
                // peer 1: create game
                var request = GetCreateGameRequest();
                request.Parameters[(byte)ParameterKey.GameId] = gameName;
                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());
                Assert.IsTrue(peerOne.WaitForNextResponse(WaitTimeout));

                var responseList = peerOne.GetResponseList();
                Assert.AreEqual(1, responseList.Count);
                var response = responseList[0];
                Assert.AreEqual(0, response.ReturnCode);
                Assert.AreEqual(1, response.Parameters[(byte)ParameterKey.ActorNr]);

                // peer 1: receive own join event
                Assert.IsTrue(peerOne.WaitForNextEvent(WaitTimeout));
                var eventList = peerOne.GetEventList();
                Assert.AreEqual(1, eventList.Count);
                var eventData = eventList[0];
                Assert.AreEqual(LiteOpCode.Join, eventData.Code);
                Assert.AreEqual(1, eventData.Parameters[(byte)ParameterKey.ActorNr]);

                var room = TestGameCache.Instance.GetRoomReference(gameName, litePeerOne);
                var game = (TestGame)room.Room;
                var plugin = (TestPlugin)game.PluginInstance.Plugin;
                room.Dispose();

                PeerHelper.SimulateDisconnect(litePeerOne);

                Assert.IsTrue(plugin.WaitForOnCloseEvent(1000));

                // peer 2: join game 
                request = GetJoinRequest();
                request.Parameters[(byte)ParameterKey.GameId] = gameName;
                PeerHelper.InvokeOnOperationRequest(litePeerTwo, request, new SendParameters());

                plugin.AllowContinue();

                Assert.IsTrue(peerTwo.WaitForNextResponse(WaitTimeout));

                Assert.AreNotEqual(plugin, game.Plugin);

                responseList = peerTwo.GetResponseList();
                Assert.AreEqual(1, responseList.Count);
                response = responseList[0];
                Assert.AreEqual(0, response.ReturnCode, response.DebugMessage);
                Assert.AreEqual(1, response.Parameters[(byte)ParameterKey.ActorNr]);

            }
            finally
            {
                if (litePeerOne.Connected)
                {
                    PeerHelper.SimulateDisconnect(litePeerOne);
                }
                PeerHelper.SimulateDisconnect(litePeerTwo);
            }
        }

        [Test]
        public void StopCloseGameIfThereIsActiveTest()
        {
            var peerOne = new DummyPeer();
            var peerTwo = new DummyPeer();
            var litePeerOne = new TestHivePeer(peerOne.Protocol, peerOne);
            var litePeerTwo = new TestHivePeer(peerTwo.Protocol, peerTwo);
            const string gameName = "StopCloseGameIfThereIsActive";
            try
            {
                // peer 1: create game
                var request = GetCreateGameRequest();
                request.Parameters[(byte)ParameterKey.GameId] = gameName;
                request.Parameters[(byte)ParameterKey.EmptyRoomLiveTime] = 1000;

                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());
                Assert.IsTrue(peerOne.WaitForNextResponse(WaitTimeout));

                var responseList = peerOne.GetResponseList();
                Assert.AreEqual(1, responseList.Count);
                var response = responseList[0];
                Assert.AreEqual(0, response.ReturnCode);
                Assert.AreEqual(1, response.Parameters[(byte)ParameterKey.ActorNr]);

                // peer 1: receive own join event
                Assert.IsTrue(peerOne.WaitForNextEvent(WaitTimeout));
                var eventList = peerOne.GetEventList();
                Assert.AreEqual(1, eventList.Count);
                var eventData = eventList[0];
                Assert.AreEqual(LiteOpCode.Join, eventData.Code);
                Assert.AreEqual(1, eventData.Parameters[(byte)ParameterKey.ActorNr]);

                var room = TestGameCache.Instance.GetRoomReference(gameName, litePeerOne);
                var game = (TestGame)room.Room;
                var plugin = (TestPlugin)game.PluginInstance.Plugin;
                room.Dispose();

                PeerHelper.SimulateDisconnect(litePeerOne);

                Assert.IsTrue(plugin.WaitForOnBeforeCloseEvent(1000));

                // peer 2: join game 
                request = GetJoinGameRequest();
                request.Parameters[(byte)ParameterKey.GameId] = gameName;
                PeerHelper.InvokeOnOperationRequest(litePeerTwo, request, new SendParameters());

                plugin.AllowContinue();

                Assert.IsTrue(peerTwo.WaitForNextResponse(WaitTimeout));

                Assert.IsFalse(plugin.WaitForOnCloseEvent(1000));
            }
            finally
            {
                if (litePeerOne.Connected)
                {
                    PeerHelper.SimulateDisconnect(litePeerOne);
                }
                PeerHelper.SimulateDisconnect(litePeerTwo);
            }
        }

        /// <summary>
        /// The join, connection loss, rejoin plus saving and restoring game state
        /// </summary>
        [Test]
        public void GetSetGameState()
        {
            var peerOne = new DummyPeer();
            var peerTwo = new DummyPeer();
            var litePeerOne = new TestHivePeer(peerOne.Protocol, peerOne);
            var litePeerTwo = new TestHivePeer(peerTwo.Protocol, peerTwo);
            RoomReference room = null;

            try
            {
                // peer 1: join
                var request = GetJoinRequest();
                request.Parameters[(byte)ParameterKey.PlayerTTL] = -1;
                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());
                Assert.IsTrue(peerOne.WaitForNextResponse(WaitTimeout));

                // peer 1: receive own join event
                Assert.IsTrue(peerOne.WaitForNextEvent(WaitTimeout));
                var eventList = peerOne.GetEventList();
                Assert.AreEqual(1, eventList.Count);
                var eventData = eventList[0];
                Assert.AreEqual(LiteOpCode.Join, eventData.Code);
                Assert.AreEqual(1, eventData.Parameters[(byte)ParameterKey.ActorNr]);

                room = TestGameCache.Instance.GetRoomReference("testGame", litePeerOne);

                var game = (TestGame)room.Room;

                const byte testKey1 = 1;
                const string testValue1 = "STRING1";
                const int testKey2 = 123;
                const double testValue2 = 123.4;
                const string testKey3 = "testKey";
                const short testValue3 = 123;

                game.Properties.Set(testKey1, testValue1);
                game.Properties.Set(testKey2, testValue2);
                game.Properties.Set(testKey3, testValue3);

                game.GetActors().ToArray()[0].Properties.Set(testKey1, testValue1);
                game.GetActors().ToArray()[0].Properties.Set(testKey2, testValue2);
                game.GetActors().ToArray()[0].Properties.Set(testKey3, testValue3);

                PeerHelper.SimulateDisconnect(litePeerOne);
                game.WaitRemovePeerFromGame();

                Assert.AreEqual(1, game.ActorsManager.InactiveActorsCount);

                var gameState = game.GetGameState();

                var propertiesCount = game.Properties.Count;
                var actorNr = game.GetDisconnectedActors().ToArray()[0].ActorNr;

                room.Dispose(); // dispose last room reference. Room will be destroyed after this line
                Assert.IsTrue(game.WaitForDispose());

                // get new room with same name
                room = TestGameCache.Instance.GetRoomReference("testGame", litePeerOne);
                game = (TestGame)room.Room;
                game.CreatePlugin();
                game.SetGameState(gameState);

                // checking restored game state
                Assert.AreEqual(1, game.ActorsManager.InactiveActorsCount);
                Assert.AreEqual(propertiesCount, game.Properties.Count);

                Assert.AreEqual(testValue1, game.Properties.GetProperty(testKey1).Value);
                Assert.AreEqual(testValue2, game.Properties.GetProperty(testKey2).Value);
                Assert.AreEqual(testValue3, game.Properties.GetProperty(testKey3).Value);

                request = GetJoinRequest();
                request.Parameters[(byte)ParameterKey.ActorNr] = actorNr;
                request.Parameters[(byte)ParameterKey.JoinMode] = JoinModeConstants.RejoinOnly;
                PeerHelper.InvokeOnOperationRequest(litePeerTwo, request, new SendParameters());
                Assert.IsTrue(peerTwo.WaitForNextResponse(WaitTimeout));

                Assert.AreEqual(0, game.ActorsManager.InactiveActorsCount);
                Assert.AreEqual(1, game.GetActors().ToList().Count);
                Assert.AreEqual(actorNr, game.GetActors().ToArray()[0].ActorNr);

                var actor0 = game.GetActors().ToArray()[0];
                Assert.AreEqual(testValue1, actor0.Properties.GetProperty(testKey1).Value);
                Assert.AreEqual(testValue2, actor0.Properties.GetProperty(testKey2).Value);
                Assert.AreEqual(testValue3, actor0.Properties.GetProperty(testKey3).Value);
            }
            finally
            {
                room?.Dispose();

                if (litePeerOne.Connected)
                {
                    PeerHelper.SimulateDisconnect(litePeerOne);
                }
                if (litePeerTwo.Connected)
                {
                    PeerHelper.SimulateDisconnect(litePeerTwo);
                }
            }
        }

        [Test]
        public void GetSetNewGameState()
        {
            var peerOne = new DummyPeer();
            var peerTwo = new DummyPeer();
            var litePeerOne = new TestHivePeer(peerOne.Protocol, peerOne, "User1");
            var litePeerTwo = new TestHivePeer(peerTwo.Protocol, peerTwo, "User2");

            RoomReference room = null;

            try
            {
                const byte testKey1 = 1;
                const string testValue1 = "STRING1";
                const int testKey2 = 123;
                const double testValue2 = 123.4;
                const string testKey3 = "key3";
                const short testValue3 = 123;
                const string testKey4 = "key4";
                const string testValue4 = "val4";
                const string testKey5 = "key5";
                const bool testValue5 = true;
                const string testKey6 = "key6";
                const bool testValue6 = true;

                const bool deleteCacheOnLeave = true;
                const int emptyRoomLiveTime = 1000;
                const string lobbyId = "MyLobby";
                const int maxPlayer = 4;
                const bool isVisible = false;
                const bool isOpen = false;
                string[] lobbyProperties = { "key3", "key4", "key5" };
                const bool suppressRoomEvents = true;
                const int slice = 101;
                var gameProperties = new Hashtable
                                         {
                                             { testKey1, testValue1 },
                                             { testKey2, testValue2 },
                                             { testKey3, testValue3 },
                                             { testKey4, testValue4 },
                                             { testKey5, testValue5 },
                                             { testKey6, testValue6 },
                                             { (byte)GameParameter.IsOpen, isOpen },
                                             { (byte)GameParameter.IsVisible, isVisible },
                                             { (byte)GameParameter.MaxPlayers, maxPlayer },
                                             { (byte)GameParameter.LobbyProperties, lobbyProperties },
                                         };

                // peer 1: join
                var request = GetJoinRequest();
                request.Parameters[(byte)ParameterKey.DeleteCacheOnLeave] = deleteCacheOnLeave;
                request.Parameters[(byte)ParameterKey.EmptyRoomLiveTime] = emptyRoomLiveTime;
                request.Parameters[(byte)ParameterKey.PlayerTTL] = -1;
                request.Parameters[(byte)ParameterKey.LobbyName] = lobbyId;
                request.Parameters[(byte)ParameterKey.LobbyType] = (byte)AppLobbyType.SqlLobby;
                //request.Parameters[(byte)ParameterKey.SuppressRoomEvents] = true; // breaks the test
                request.Parameters[(byte)ParameterKey.GameProperties] = gameProperties;
                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());
                Assert.IsTrue(peerOne.WaitForNextResponse(WaitTimeout));
                var resp = peerOne.GetResponseList();
                Assert.AreEqual(0, resp[0].ReturnCode, resp[0].DebugMessage);

                // peer 1: receive own join event
                Assert.IsTrue(peerOne.WaitForNextEvent(WaitTimeout));
                var eventList = peerOne.GetEventList();
                Assert.AreEqual(1, eventList.Count);
                var eventData = eventList[0];
                Assert.AreEqual(LiteOpCode.Join, eventData.Code);
                Assert.AreEqual(1, eventData.Parameters[(byte)ParameterKey.ActorNr]);

                room = TestGameCache.Instance.GetRoomReference("testGame", litePeerOne);

                var game = (TestGame)room.Room;

                var actor0 = game.GetActors().ToArray()[0];
                actor0.Properties.Set(testKey1, testValue1);
                actor0.Properties.Set(testKey2, testValue2);
                actor0.Properties.Set(testKey3, testValue3);

                string msg;
                game.EventCache.AddEvent(0, new CustomEvent(1, 1, 1), out msg);
                game.EventCache.AddEvent(0, new CustomEvent(1, 2, "2"), out msg);
                game.EventCache.AddEvent(0, new CustomEvent(1, 3, true), out msg);
                game.EventCache.AddEvent(0, new CustomEvent(1, 4, new[] { "4" }), out msg);

                var dummyActor11 = new TestActor(11, "dummyActor11");
                var dummyActor12 = new TestActor(12, "dummyActor12");
                var dummyActor13 = new TestActor(13, "dummyActor13");

                game.AddActorToGroup(1, dummyActor11);
                game.AddActorToGroup(1, dummyActor12);
                game.AddActorToGroup(1, dummyActor13);

                var dummyActor21 = new TestActor(21, "dummyActor21");
                var dummyActor22 = new TestActor(22, "dummyActor22");
                var dummyActor23 = new TestActor(23, "dummyActor23");

                game.AddActorToGroup(2, dummyActor21);
                game.AddActorToGroup(2, dummyActor22);
                game.AddActorToGroup(2, dummyActor23);

                PeerHelper.SimulateDisconnect(litePeerOne);
                game.WaitRemovePeerFromGame();

                Assert.AreEqual(1, game.ActorsManager.InactiveActorsCount);

                game.ActorsManager.AddInactive(dummyActor11);
                game.ActorsManager.AddInactive(dummyActor12);
                game.ActorsManager.AddInactive(dummyActor13);

                game.ActorsManager.AddInactive(dummyActor21);
                game.ActorsManager.AddInactive(dummyActor22);
                game.ActorsManager.AddInactive(dummyActor23);

                Assert.AreEqual(7, game.ActorsManager.InactiveActorsCount);

                game.SetSuppressRoomEvents(suppressRoomEvents);
                game.SetSlice(slice);

                var actorNr = game.GetDisconnectedActors().ToArray()[0].ActorNr;

                var gameState = game.GetGameState();

                room.Dispose(); // dispose last room reference. Room will be destroyed after this line
                game.WaitForDispose();

                // JavaScriptSerializer has no easy option to suppress null, so we plan to use Json.Net
                // StackService and Json.Net are so far identical
                //var stateSerializedByStackService = gameState.SerializeToString();
                var stateSerializedByJsonNet = JsonConvert.SerializeObject(gameState, formatting: Formatting.Indented);
                //Assert.AreEqual(stateSerializedByStackService, stateSerializedByJsonNet);
                Console.WriteLine(stateSerializedByJsonNet);
                // Json.Net converts to own type ... that have to be converted to standard types
                // to get that working it need more work
                //var stateDeserializedByJsonNet = JsonConvert.DeserializeObject<Dictionary<string, object>>(stateSerializedByJsonNet);

                // StackService broke deserializing generic objects in earlier tests, won't be using that!

                // Best option for us currently - although also the slowest.
                var stateDeserializedByJScript = (new JavaScriptSerializer()).Deserialize<Dictionary<string, object>>(stateSerializedByJsonNet);

                // get new room with same name
                room = TestGameCache.Instance.GetRoomReference("testGame", litePeerOne);
                game = (TestGame)room.Room;
                Assert.AreEqual(game.ActorCounter, 0);

                IPluginHost gameSink = game;
                game.CreatePlugin();
                game.SetGameState(stateDeserializedByJScript);
                Assert.IsNotNull(game.ActorsManager.ExcludedActors);

                // checking restored game state
                Assert.AreEqual(7, game.ActorsManager.InactiveActorsCount);
                //Assert.AreEqual(propertiesCount, game.Properties.Count);
                Assert.AreEqual(4, game.EventCache.GetSliceSize(0));
                Assert.AreEqual(JsonConvert.SerializeObject(new CustomEvent(1, 1, 1)), JsonConvert.SerializeObject(game.EventCache.GetCustomEvent(0, 0)));
                Assert.AreEqual(JsonConvert.SerializeObject(new CustomEvent(1, 2, "2")), JsonConvert.SerializeObject(game.EventCache.GetCustomEvent(0, 1)));
                Assert.AreEqual(JsonConvert.SerializeObject(new CustomEvent(1, 3, true)), JsonConvert.SerializeObject(game.EventCache.GetCustomEvent(0, 2)));
                Assert.AreEqual(JsonConvert.SerializeObject(new CustomEvent(1, 4, new[] { "4" })), JsonConvert.SerializeObject(game.EventCache.GetCustomEvent(0, 3)));

                Assert.AreEqual(game.ActorCounter, 1);
                Assert.AreEqual(game.DeleteCacheOnLeave, deleteCacheOnLeave);
                Assert.AreEqual(game.EmptyRoomLiveTime, emptyRoomLiveTime);
                Assert.AreEqual(game.IsOpen, isOpen);
                Assert.AreEqual(game.IsVisible, isOpen);
                Assert.AreEqual(game.LobbyId, lobbyId);
                Assert.AreEqual(game.LobbyProperties, lobbyProperties);
                Assert.AreEqual(game.LobbyType, AppLobbyType.SqlLobby);
                Assert.AreEqual(game.MaxPlayers, maxPlayer);
                Assert.AreEqual(game.PlayerTTL, -1);
                Assert.AreEqual(game.SuppressRoomEvents, suppressRoomEvents);
                Assert.AreEqual(game.EventCache.Slice, slice);

                Assert.AreEqual(testValue1, game.Properties.GetProperty(testKey1).Value);
                Assert.AreEqual(testValue2, game.Properties.GetProperty(testKey2).Value);
                Assert.AreEqual(testValue3, game.Properties.GetProperty(testKey3).Value);
                Assert.AreEqual(testValue4, game.Properties.GetProperty(testKey4).Value);
                Assert.AreEqual(testValue5, game.Properties.GetProperty(testKey5).Value);

                // checking the custom game properties 
                var customGameProperties = gameSink.CustomGameProperties;
                Assert.AreEqual(lobbyProperties.Length, customGameProperties.Count);
                foreach (var key in lobbyProperties)
                {
                    Assert.AreEqual(gameProperties[key], customGameProperties[key]);
                }

                request = GetJoinRequest();
                request.Parameters[(byte)ParameterKey.ActorNr] = actorNr;
                request.Parameters[(byte)ParameterKey.JoinMode] = JoinModeConstants.RejoinOnly;

                // we expect the first rejoin to fail since the user ids don't match
                PeerHelper.InvokeOnOperationRequest(litePeerTwo, request, new SendParameters());
                Assert.IsTrue(peerTwo.WaitForNextResponse(WaitTimeout));
                resp = peerTwo.GetResponseList();
                Assert.AreEqual(-1, resp[0].ReturnCode, resp[0].DebugMessage);

                PeerHelper.SimulateDisconnect(litePeerTwo);

                litePeerTwo = new TestHivePeer(peerTwo.Protocol, peerTwo);

                // this try is expected to succeed, since user ids match
                litePeerTwo.SetUserId(litePeerOne.UserId);
                PeerHelper.InvokeOnOperationRequest(litePeerTwo, request, new SendParameters());
                Assert.IsTrue(peerTwo.WaitForNextResponse(WaitTimeout));
                resp = peerTwo.GetResponseList();
                Assert.AreEqual(0, resp[0].ReturnCode, resp[0].DebugMessage);

                Assert.AreEqual(6, game.ActorsManager.InactiveActorsCount);
                Assert.AreEqual(1, game.GetActors().ToList().Count);

                actor0 = game.GetActors().ToArray()[0];
                Assert.AreEqual(actorNr, actor0.ActorNr);

                Assert.AreEqual(testValue1, actor0.Properties.GetProperty(testKey1).Value);
                Assert.AreEqual(testValue2, actor0.Properties.GetProperty(testKey2).Value);
                Assert.AreEqual(testValue3, actor0.Properties.GetProperty(testKey3).Value);

            }
            finally
            {
                if (litePeerOne.Connected)
                {
                    PeerHelper.SimulateDisconnect(litePeerOne);
                }
                if (litePeerTwo.Connected)
                {
                    PeerHelper.SimulateDisconnect(litePeerTwo);
                }

                room?.Dispose();
            }
        }

        [Test]
        public void GameStateCompatibilityTest()
        {
            var peerOne = new DummyPeer();
            var litePeerOne = new TestHivePeer(peerOne.Protocol, peerOne, "User1");

            RoomReference room = null;

            try
            {
                const byte testKey1 = 1;
                const string testValue1 = "STRING1";
                const int testKey2 = 123;
                const double testValue2 = 123.4;
                const string testKey3 = "key3";
                const short testValue3 = 123;
                const string testKey4 = "key4";
                const string testValue4 = "val4";
                const string testKey5 = "key5";
                const bool testValue5 = true;
                const string testKey6 = "key6";
                const bool testValue6 = true;

                const bool deleteCacheOnLeave = true;
                const int emptyRoomLiveTime = 1000;
                const string lobbyId = "MyLobby";
                const int maxPlayer = 4;
                const bool isVisible = false;
                const bool isOpen = false;
                string[] lobbyProperties = { "key3", "key4", "key5" };
                const bool suppressRoomEvents = true;
                const int slice = 101;
                var gameProperties = new Hashtable
                                         {
                                             { testKey1, testValue1 },
                                             { testKey2, testValue2 },
                                             { testKey3, testValue3 },
                                             { testKey4, testValue4 },
                                             { testKey5, testValue5 },
                                             { testKey6, testValue6 },
                                             { (byte)GameParameter.IsOpen, isOpen },
                                             { (byte)GameParameter.IsVisible, isVisible },
                                             { (byte)GameParameter.MaxPlayers, maxPlayer },
                                             { (byte)GameParameter.LobbyProperties, lobbyProperties },
                                         };

                // peer 1: join
                var request = GetJoinRequest();
                request.Parameters[(byte)ParameterKey.DeleteCacheOnLeave] = deleteCacheOnLeave;
                request.Parameters[(byte)ParameterKey.EmptyRoomLiveTime] = emptyRoomLiveTime;
                request.Parameters[(byte)ParameterKey.PlayerTTL] = -1;
                request.Parameters[(byte)ParameterKey.LobbyName] = lobbyId;
                request.Parameters[(byte)ParameterKey.LobbyType] = (byte)AppLobbyType.SqlLobby;
                //request.Parameters[(byte)ParameterKey.SuppressRoomEvents] = true; // breaks the test
                request.Parameters[(byte)ParameterKey.GameProperties] = gameProperties;
                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());
                Assert.IsTrue(peerOne.WaitForNextResponse(WaitTimeout));
                var resp = peerOne.GetResponseList();
                Assert.AreEqual(0, resp[0].ReturnCode, resp[0].DebugMessage);

                // peer 1: receive own join event
                Assert.IsTrue(peerOne.WaitForNextEvent(WaitTimeout));
                var eventList = peerOne.GetEventList();
                Assert.AreEqual(1, eventList.Count);
                var eventData = eventList[0];
                Assert.AreEqual(LiteOpCode.Join, eventData.Code);
                Assert.AreEqual(1, eventData.Parameters[(byte)ParameterKey.ActorNr]);

                room = TestGameCache.Instance.GetRoomReference("testGame", litePeerOne);

                var game = (TestGame)room.Room;

                var actor0 = game.GetActors().ToArray()[0];
                actor0.Properties.Set(testKey1, testValue1);
                actor0.Properties.Set(testKey2, testValue2);
                actor0.Properties.Set(testKey3, testValue3);


                string msg;
                game.EventCache.AddEvent(0, new CustomEvent(1, 1, 1), out msg);
                game.EventCache.AddEvent(0, new CustomEvent(1, 2, "2"), out msg);
                game.EventCache.AddEvent(0, new CustomEvent(1, 3, true), out msg);
                game.EventCache.AddEvent(0, new CustomEvent(1, 4, new[] { "4" }), out msg);

                var dummyActor11 = new TestActor(11, "dummyActor11");
                var dummyActor12 = new TestActor(12, "dummyActor12");
                var dummyActor13 = new TestActor(13, "dummyActor13");

                game.AddActorToGroup(1, dummyActor11);
                game.AddActorToGroup(1, dummyActor12);
                game.AddActorToGroup(1, dummyActor13);

                var dummyActor21 = new TestActor(21, "dummyActor21");
                var dummyActor22 = new TestActor(22, "dummyActor22");
                var dummyActor23 = new TestActor(23, "dummyActor23");

                game.AddActorToGroup(2, dummyActor21);
                game.AddActorToGroup(2, dummyActor22);
                game.AddActorToGroup(2, dummyActor23);

                PeerHelper.SimulateDisconnect(litePeerOne);
                game.WaitRemovePeerFromGame();

                Assert.AreEqual(1, game.ActorsManager.InactiveActorsCount);

                game.ActorsManager.AddInactive(dummyActor11);
                game.ActorsManager.AddInactive(dummyActor12);
                game.ActorsManager.AddInactive(dummyActor13);

                game.ActorsManager.AddInactive(dummyActor21);
                game.ActorsManager.AddInactive(dummyActor22);
                game.ActorsManager.AddInactive(dummyActor23);

                Assert.AreEqual(7, game.ActorsManager.InactiveActorsCount);

                IPluginHost gameSink = game;

                game.SetSuppressRoomEvents(suppressRoomEvents);
                game.SetSlice(slice);

                var serializableGameState = gameSink.GetSerializableGameState();

                room.Dispose(); // dispose last room reference. Room will be destroyed after this line
                game.WaitForDispose();

                room = TestGameCache.Instance.GetRoomReference("testGame", litePeerOne);
                game = (TestGame)room.Room;
                gameSink = game;

                game.CreatePlugin();

                gameSink.SetGameState(serializableGameState);

                var serializableGameState0 = gameSink.GetSerializableGameState();
                CompareStates(serializableGameState0, serializableGameState);

                room.Dispose(); // dispose last room reference. Room will be destroyed after this line
                game.WaitForDispose();


                SerializableGameState fromOldJson;
                Dictionary<string, object> fromOldJsonDic;
                using (var sr = new StreamReader("JsonGameState.txt"))
                {
                    var line = sr.ReadToEnd();
                    line = line.Replace("Username", "Nickname");
                    fromOldJson = JsonConvert.DeserializeObject<SerializableGameState>(line);
                    fromOldJsonDic = (new JavaScriptSerializer()).Deserialize<Dictionary<string, object>>(line);
                }

                room = TestGameCache.Instance.GetRoomReference("testGame", litePeerOne);
                game = (TestGame)room.Room;
                gameSink = game;
                game.CreatePlugin();

                gameSink.SetGameState(fromOldJson);

                var serializableGameState2 = gameSink.GetSerializableGameState();

                room.Dispose(); // dispose last room reference. Room will be destroyed after this line
                game.WaitForDispose();

                room = TestGameCache.Instance.GetRoomReference("testGame", litePeerOne);
                game = (TestGame)room.Room;
                gameSink = game;
                game.CreatePlugin();

                ((HiveHostGame)gameSink).SetGameState(fromOldJsonDic);

                var serializableGameState3 = gameSink.GetSerializableGameState();

                CompareStates(serializableGameState2, serializableGameState);
                CompareStates(serializableGameState3, serializableGameState);
            }
            finally
            {
                if (litePeerOne.Connected)
                {
                    PeerHelper.SimulateDisconnect(litePeerOne);
                }

                room?.Dispose();
            }
        }

        private static void CompareStates(SerializableGameState state, SerializableGameState expected)
        {
            Assert.AreEqual(expected.ActorCounter, state.ActorCounter);
            for (var i = 0; i < state.ActorList.Count; ++i)
            {
                Assert.AreEqual(expected.ActorList[i].ActorNr, state.ActorList[i].ActorNr);
                Assert.AreEqual(expected.ActorList[i].Binary, state.ActorList[i].Binary);
                Assert.AreEqual(expected.ActorList[i].DEBUG_BINARY, state.ActorList[i].DEBUG_BINARY);
                Assert.AreEqual(expected.ActorList[i].UserId, state.ActorList[i].UserId);
                Assert.AreEqual(expected.ActorList[i].Nickname, state.ActorList[i].Nickname);
            }
            Assert.AreEqual(expected.RoomFlags, state.RoomFlags);
            Assert.AreEqual(expected.CustomProperties, state.CustomProperties);
            Assert.AreEqual(expected.DebugInfo, state.DebugInfo);
            Assert.AreEqual(expected.EmptyRoomTTL, state.EmptyRoomTTL);
            Assert.AreEqual(expected.IsOpen, state.IsOpen);
            Assert.AreEqual(expected.IsVisible, state.IsVisible);
            Assert.AreEqual(expected.LobbyId, state.LobbyId);
            Assert.AreEqual(expected.LobbyProperties, state.LobbyProperties);
            Assert.AreEqual(expected.LobbyType, state.LobbyType);
            Assert.AreEqual(expected.MaxPlayers, state.MaxPlayers);
            Assert.AreEqual(expected.PlayerTTL, state.PlayerTTL);
            Assert.AreEqual(expected.Slice, state.Slice);
            Assert.AreEqual(Serializer.DeserializeBase64((string)(expected.Binary["20"])), Serializer.DeserializeBase64((string)(state.Binary["20"])));
            Assert.AreEqual(Serializer.DeserializeBase64((string)(expected.Binary["19"])), Serializer.DeserializeBase64((string)(state.Binary["19"])));
            Assert.AreEqual(Serializer.DeserializeBase64((string)(expected.Binary["18"])), Serializer.DeserializeBase64((string)(state.Binary["18"])));
        }

        [Test]
        public void GameStateUpdateWellKnownProperties()
        {
            var peerOne = new DummyPeer();
            var litePeerOne = new TestHivePeer(peerOne.Protocol, peerOne, "User1");

            RoomReference room = null;

            try
            {
                const byte testKey1 = 1;
                const string testValue1 = "STRING1";
                const int testKey2 = 123;
                const double testValue2 = 123.4;
                const string testKey3 = "key3";
                const short testValue3 = 123;
                const string testKey4 = "key4";
                const string testValue4 = "val4";
                const string testKey5 = "key5";
                const bool testValue5 = true;
                const string testKey6 = "key6";
                const bool testValue6 = true;

                const bool deleteCacheOnLeave = true;
                const int emptyRoomLiveTime = 1000;
                const string lobbyId = "MyLobby";
                const int maxPlayer = 4;
                const bool isVisible = false;
                const bool isOpen = false;
                object[] lobbyProperties = { "key3", "key4", "key5" };
                const bool suppressRoomEvents = true;
                const int slice = 101;
                var gameProperties = new Hashtable
                                         {
                                             { testKey1, testValue1 },
                                             { testKey2, testValue2 },
                                             { testKey3, testValue3 },
                                             { testKey4, testValue4 },
                                             { testKey5, testValue5 },
                                             { testKey6, testValue6 },
                                             { (byte)GameParameter.IsOpen, isOpen },
                                             { (byte)GameParameter.IsVisible, isVisible },
                                             { (byte)GameParameter.MaxPlayers, maxPlayer },
                                             { (byte)GameParameter.LobbyProperties, lobbyProperties },
                                         };

                // peer 1: join
                var request = GetJoinRequest();
                request.Parameters[(byte)ParameterKey.DeleteCacheOnLeave] = deleteCacheOnLeave;
                request.Parameters[(byte)ParameterKey.EmptyRoomLiveTime] = emptyRoomLiveTime;
                request.Parameters[(byte)ParameterKey.PlayerTTL] = -1;
                request.Parameters[(byte)ParameterKey.LobbyName] = lobbyId;
                request.Parameters[(byte)ParameterKey.LobbyType] = (byte)AppLobbyType.SqlLobby;
                //request.Parameters[(byte)ParameterKey.SuppressRoomEvents] = true; // breaks the test
                request.Parameters[(byte)ParameterKey.GameProperties] = gameProperties;
                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());
                Assert.IsTrue(peerOne.WaitForNextResponse(WaitTimeout));
                var resp = peerOne.GetResponseList();
                Assert.AreEqual(0, resp[0].ReturnCode, resp[0].DebugMessage);

                // peer 1: receive own join event
                Assert.IsTrue(peerOne.WaitForNextEvent(WaitTimeout));
                var eventList = peerOne.GetEventList();
                Assert.AreEqual(1, eventList.Count);
                var eventData = eventList[0];
                Assert.AreEqual(LiteOpCode.Join, eventData.Code);
                Assert.AreEqual(1, eventData.Parameters[(byte)ParameterKey.ActorNr]);

                room = TestGameCache.Instance.GetRoomReference("testGame", litePeerOne);

                var game = (TestGame)room.Room;

                var actor0 = game.GetActors().ToArray()[0];
                actor0.Properties.Set(testKey1, testValue1);
                actor0.Properties.Set(testKey2, testValue2);
                actor0.Properties.Set(testKey3, testValue3);


                string msg;
                game.EventCache.AddEvent(0, new CustomEvent(1, 1, 1), out msg);
                game.EventCache.AddEvent(0, new CustomEvent(1, 2, "2"), out msg);
                game.EventCache.AddEvent(0, new CustomEvent(1, 3, true), out msg);
                game.EventCache.AddEvent(0, new CustomEvent(1, 4, new[] { "4" }), out msg);

                var dummyActor11 = new TestActor(11, "dummyActor11");
                var dummyActor12 = new TestActor(12, "dummyActor12");
                var dummyActor13 = new TestActor(13, "dummyActor13");

                game.AddActorToGroup(1, dummyActor11);
                game.AddActorToGroup(1, dummyActor12);
                game.AddActorToGroup(1, dummyActor13);

                var dummyActor21 = new TestActor(21, "dummyActor21");
                var dummyActor22 = new TestActor(22, "dummyActor22");
                var dummyActor23 = new TestActor(23, "dummyActor23");

                game.AddActorToGroup(2, dummyActor21);
                game.AddActorToGroup(2, dummyActor22);
                game.AddActorToGroup(2, dummyActor23);

                PeerHelper.SimulateDisconnect(litePeerOne);
                game.WaitRemovePeerFromGame();

                Assert.AreEqual(1, game.ActorsManager.InactiveActorsCount);

                game.ActorsManager.AddInactive(dummyActor11);
                game.ActorsManager.AddInactive(dummyActor12);
                game.ActorsManager.AddInactive(dummyActor13);

                game.ActorsManager.AddInactive(dummyActor21);
                game.ActorsManager.AddInactive(dummyActor22);
                game.ActorsManager.AddInactive(dummyActor23);

                Assert.AreEqual(7, game.ActorsManager.InactiveActorsCount);

                IPluginHost gameSink = game;

                game.SetSuppressRoomEvents(suppressRoomEvents);
                game.SetSlice(slice);

                var gamePropertiesBeforeRestore = game.Properties.GetProperties();

                var gameState = gameSink.GetSerializableGameState();

                Assert.AreEqual(isOpen, gameState.IsOpen);
                Assert.AreEqual(isVisible, gameState.IsVisible);
                Assert.AreEqual(maxPlayer, gameState.MaxPlayers);

                gameState.IsOpen = !gameState.IsOpen;
                gameState.IsVisible = !gameState.IsVisible;
                gameState.MaxPlayers = maxPlayer + 1;

                room.Dispose(); // dispose last room reference. Room will be destroyed after this line
                game.WaitForDispose();


                // get new room with same name
                room = TestGameCache.Instance.GetRoomReference("testGame", litePeerOne);
                game = (TestGame)room.Room;
                Assert.AreEqual(game.ActorCounter, 0);

                gameSink = game;
                game.CreatePlugin();

                gameSink.SetGameState(gameState);

                var gamePropertiesAfterSetGame = game.Properties.GetProperties();
                // we check only count of properties because they are expected to be different after SetGameState
                Assert.AreEqual(gamePropertiesBeforeRestore.Count, gamePropertiesAfterSetGame.Count);

                Assert.AreEqual(!isOpen, game.IsOpen);
                Assert.AreEqual(!isVisible, game.IsVisible);
                Assert.AreEqual(maxPlayer + 1, game.MaxPlayers);

                Assert.AreEqual(!isOpen, game.Properties.GetProperty((byte)GameParameter.IsOpen).Value);
                Assert.AreEqual(!isVisible, game.Properties.GetProperty((byte)GameParameter.IsVisible).Value);
                Assert.AreEqual(maxPlayer + 1, game.Properties.GetProperty((byte)GameParameter.MaxPlayers).Value);
            }
            finally
            {
                if (litePeerOne.Connected)
                {
                    PeerHelper.SimulateDisconnect(litePeerOne);
                }

                room?.Dispose();
            }
        }

        [Test]
        public void EventCacheGetUserResponse()
        {
            var peerOne = new DummyPeer();
            var peerTwo = new DummyPeer();
            var litePeerOne = new TestHivePeer(peerOne.Protocol, peerOne, "User1");
            var litePeerTwo = new TestHivePeer(peerTwo.Protocol, peerTwo, "User2");
            RoomReference room = null;
            try
            {
                const int maxPlayer = 4;
                const bool isVisible = true;
                const bool isOpen = true;

                var gameProperties = new Hashtable
                {
                    { (byte)GameParameter.IsOpen, isOpen },
                    { (byte)GameParameter.IsVisible, isVisible },
                    { (byte)GameParameter.MaxPlayers, maxPlayer },
                };

                // peer 1: join
                var request = GetJoinRequest();
                request.Parameters[(byte)ParameterKey.PlayerTTL] = -1;
                request.Parameters[(byte)ParameterKey.GameProperties] = gameProperties;

                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());
                Assert.IsTrue(peerOne.WaitForNextResponse(WaitTimeout));
                var resp = peerOne.GetResponseList();
                Assert.AreEqual(0, resp[0].ReturnCode, resp[0].DebugMessage);

                // peer 1: receive own join event
                Assert.IsTrue(peerOne.WaitForNextEvent(WaitTimeout));
                var eventList = peerOne.GetEventList();
                Assert.AreEqual(1, eventList.Count);
                var eventData = eventList[0];
                Assert.AreEqual(LiteOpCode.Join, eventData.Code);
                Assert.AreEqual(1, eventData.Parameters[(byte)ParameterKey.ActorNr]);

                room = TestGameCache.Instance.GetRoomReference("testGame", litePeerOne);

                var game = (TestGame)room.Room;

                var eventCache = game.EventCache;

                eventCache.SlicesCountLimit = 2;
                eventCache.CachedEventsCountLimit = 2;

                Thread.Sleep(100);
                var raiseEvent = GetRaiseEventRequest(null, (byte)CacheOperation.AddToRoomCache);
                raiseEvent.Parameters.Add((byte)ParameterKey.Data, "data");

                PeerHelper.InvokeOnOperationRequest(litePeerOne, raiseEvent, new SendParameters());
                PeerHelper.InvokeOnOperationRequest(litePeerOne, raiseEvent, new SendParameters());

                // this message should be added but cache should be discarded and game closed
                PeerHelper.InvokeOnOperationRequest(litePeerOne, raiseEvent, new SendParameters());
                //we do not block new events even in case of exceeding of limits
                Assert.IsFalse(peerOne.WaitForNextResponse(WaitTimeout));

                // add one more to make sure that this event will not be cached
                PeerHelper.InvokeOnOperationRequest(litePeerOne, raiseEvent, new SendParameters());

                Assert.That(game.IsOpen, Is.False);
                Assert.That(game.CacheDiscarded, Is.True);
                Assert.That(eventCache.Discarded, Is.True);
                Assert.That(eventCache.Count, Is.Zero);

                // checks that we can not join and get correct error code and debug message
                PeerHelper.InvokeOnOperationRequest(litePeerTwo, request, new SendParameters());
                Assert.IsTrue(peerTwo.WaitForNextResponse(WaitTimeout));
                resp = peerTwo.GetResponseList();
                Assert.That(resp[0].ReturnCode, Is.EqualTo((short)ErrorCode.GameClosed), resp[0].DebugMessage);
                Assert.That(resp[0].DebugMessage, Is.EqualTo(HiveErrorMessages.GameClosedCacheDiscarded));
            }
            finally
            {
                room?.Dispose();

                if (litePeerOne.Connected)
                {
                    PeerHelper.SimulateDisconnect(litePeerOne);
                }
            }
        }

        [Test]
        public void ActorEventCacheGetUserResponse()
        {
            var peerOne = new DummyPeer();
            var litePeerOne = new TestHivePeer(peerOne.Protocol, peerOne, "User1");

            var peerTwo = new DummyPeer();
            var litePeerTwo = new TestHivePeer(peerTwo.Protocol, peerTwo, "User2");
            RoomReference room = null;
            try
            {

                const int maxPlayer = 4;
                const bool isVisible = true;
                const bool isOpen = true;

                var gameProperties = new Hashtable
                {
                    { (byte)GameParameter.IsOpen, isOpen },
                    { (byte)GameParameter.IsVisible, isVisible },
                    { (byte)GameParameter.MaxPlayers, maxPlayer },
                };

                // peer 1: join
                var request = GetJoinRequest();
                request.Parameters[(byte)ParameterKey.PlayerTTL] = -1;
                request.Parameters[(byte)ParameterKey.GameProperties] = gameProperties;

                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());
                Assert.IsTrue(peerOne.WaitForNextResponse(WaitTimeout));
                var resp = peerOne.GetResponseList();
                Assert.AreEqual(0, resp[0].ReturnCode, resp[0].DebugMessage);

                // peer 1: receive own join event
                Assert.IsTrue(peerOne.WaitForNextEvent(WaitTimeout));
                var eventList = peerOne.GetEventList();
                Assert.AreEqual(1, eventList.Count);
                var eventData = eventList[0];
                Assert.AreEqual(LiteOpCode.Join, eventData.Code);
                Assert.AreEqual(1, eventData.Parameters[(byte)ParameterKey.ActorNr]);

                room = TestGameCache.Instance.GetRoomReference("testGame", litePeerOne);

                var game = (TestGame)room.Room;

                var actorEventCache = game.ActorEventCache;

                actorEventCache.CachedEventsCountLimit = 2;

                Thread.Sleep(100);
                var raiseEvent = GetRaiseEventRequest(0, new Hashtable(), (byte)CacheOperation.MergeCache, null, null);
                PeerHelper.InvokeOnOperationRequest(litePeerOne, raiseEvent, new SendParameters());

                raiseEvent = GetRaiseEventRequest(1, new Hashtable(), (byte)CacheOperation.MergeCache, null, null);
                PeerHelper.InvokeOnOperationRequest(litePeerOne, raiseEvent, new SendParameters());

                // this message will exceed limit, game will be closed and cache discarded
                raiseEvent = GetRaiseEventRequest(2, new Hashtable(), (byte)CacheOperation.MergeCache, null, null);
                PeerHelper.InvokeOnOperationRequest(litePeerOne, raiseEvent, new SendParameters());

                Assert.IsFalse(peerOne.WaitForNextResponse(WaitTimeout));

                Assert.That(game.IsOpen, Is.False);
                Assert.That(game.CacheDiscarded, Is.True);
                Assert.That(actorEventCache.Discarded, Is.True);
                Assert.That(actorEventCache.Count(), Is.Zero);

                // check that we can not join and get correct error code and debug message
                PeerHelper.InvokeOnOperationRequest(litePeerTwo, request, new SendParameters());
                Assert.IsTrue(peerTwo.WaitForNextResponse(WaitTimeout));
                resp = peerTwo.GetResponseList();
                Assert.That(resp[0].ReturnCode, Is.EqualTo((short)ErrorCode.GameClosed), resp[0].DebugMessage);
                Assert.That(resp[0].DebugMessage, Is.EqualTo(HiveErrorMessages.GameClosedCacheDiscarded));

            }
            finally
            {
                room?.Dispose();
                if (litePeerOne.Connected)
                {
                    PeerHelper.SimulateDisconnect(litePeerOne);
                }

                if (litePeerTwo.Connected)
                {
                    PeerHelper.SimulateDisconnect(litePeerTwo);
                }
            }
        }

        [Test]
        public void JoinCheckUserIds()
        {
            var peerOne = new DummyPeer();
            var peerTwo = new DummyPeer();
            var litePeerOne = new TestHivePeer(peerOne.Protocol, peerOne, "User1");
            var litePeerTwo = new TestHivePeer(peerTwo.Protocol, peerTwo, "User2");

            RoomReference room = null;

            try
            {
                // peer 1: joins / creates the room
                var request = GetJoinRequest();
                request.Parameters[(byte)ParameterKey.PlayerTTL] = -1;
                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());
                Assert.IsTrue(peerOne.WaitForNextResponse(WaitTimeout));
                var resp = peerOne.GetResponseList();
                Assert.AreEqual(0, resp[0].ReturnCode, resp[0].DebugMessage);

                // peer 1: receive own join event
                Assert.IsTrue(peerOne.WaitForNextEvent(WaitTimeout));
                var eventList = peerOne.GetEventList();
                Assert.AreEqual(1, eventList.Count);
                var eventData = eventList[0];
                Assert.AreEqual(LiteOpCode.Join, eventData.Code);
                Assert.AreEqual(1, eventData.Parameters[(byte)ParameterKey.ActorNr]);

                room = TestGameCache.Instance.GetRoomReference("testGame", litePeerOne);

                var game = (TestGame)room.Room;

                PeerHelper.SimulateDisconnect(litePeerOne);
                game.WaitRemovePeerFromGame();

                Assert.AreEqual(1, game.ActorsManager.InactiveActorsCount);

                IPluginHost gameSink = game;

                var propertiesCount = game.Properties.Count;
                var actorNr = game.GetDisconnectedActors().ToArray()[0].ActorNr;

                var gameState = gameSink.GetSerializableGameState();

                room.Dispose(); // dispose last room reference. Room will be destroyed after this line
                game.WaitForDispose();

                // get new room with same name
                room = TestGameCache.Instance.GetRoomReference("testGame", litePeerOne);
                game = (TestGame)room.Room;
                Assert.AreEqual(game.ActorCounter, 0);

                gameSink = game;
                game.CreatePlugin();
                gameSink.SetGameState(gameState);

                // checking restored game state
                Assert.AreEqual(1, game.ActorsManager.InactiveActorsCount);
                Assert.AreEqual(propertiesCount, game.Properties.Count);

                Assert.AreEqual(game.ActorCounter, 1);

                PeerHelper.SimulateDisconnect(litePeerTwo);
                Thread.Sleep(10);
                litePeerTwo = new TestHivePeer(peerTwo.Protocol, peerTwo);

                request = GetJoinRequest();
                request.Parameters[(byte)ParameterKey.JoinMode] = (byte)JoinModeConstants.RejoinOnly;
                request.Parameters[(byte)ParameterKey.ActorNr] = actorNr;

                // we expect the first rejoin to fail since the user ids don't match
                PeerHelper.InvokeOnOperationRequest(litePeerTwo, request, new SendParameters());
                Assert.IsTrue(peerTwo.WaitForNextResponse(WaitTimeout));
                resp = peerTwo.GetResponseList();
                Assert.AreEqual(-1, resp[0].ReturnCode, resp[0].DebugMessage);

                PeerHelper.SimulateDisconnect(litePeerTwo);
                Thread.Sleep(10);
                litePeerTwo = new TestHivePeer(peerTwo.Protocol, peerTwo);

                // this try is expected to succeed, since user ids match
                litePeerTwo.SetUserId(litePeerOne.UserId);
                PeerHelper.InvokeOnOperationRequest(litePeerTwo, request, new SendParameters());
                Assert.IsTrue(peerTwo.WaitForNextResponse(WaitTimeout));
                resp = peerTwo.GetResponseList();
                Assert.AreEqual(0, resp[0].ReturnCode, resp[0].DebugMessage);

                Assert.AreEqual(0, game.ActorsManager.InactiveActorsCount);
                Assert.AreEqual(1, game.GetActors().ToList().Count);
                Assert.AreEqual(actorNr, game.GetActors().ToArray()[0].ActorNr);
            }
            finally
            {
                room?.Dispose();
                if (litePeerOne.Connected)
                {
                    PeerHelper.SimulateDisconnect(litePeerOne);
                }
                if (litePeerTwo.Connected)
                {
                    PeerHelper.SimulateDisconnect(litePeerTwo);
                }
            }
        }

        /// for backwards compatibility
        [Test]
        public void JoinCheckUserIds_Default_No_Check()
        {
            var peerOne = new DummyPeer();
            var peerTwo = new DummyPeer();
            var litePeerOne = new TestHivePeer(peerOne.Protocol, peerOne, "User1");
            var litePeerTwo = new TestHivePeer(peerTwo.Protocol, peerTwo, "User1");

            try
            {
                // peer 1: joins / creates the room
                var request = GetJoinRequest();
                //request.Parameters[(byte)ParameterKey.CheckUserOnJoin] = true;
                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());
                Assert.IsTrue(peerOne.WaitForNextResponse(WaitTimeout));
                var resp = peerOne.GetResponseList();
                Assert.AreEqual(0, resp[0].ReturnCode, resp[0].DebugMessage);

                // peer 1: receive own join event
                Assert.IsTrue(peerOne.WaitForNextEvent(WaitTimeout));
                var eventList = peerOne.GetEventList();
                Assert.AreEqual(1, eventList.Count);
                var eventData = eventList[0];
                Assert.AreEqual(LiteOpCode.Join, eventData.Code);
                Assert.AreEqual(1, eventData.Parameters[(byte)ParameterKey.ActorNr]);


                // this try is expected to succeed, even if user ids match
                PeerHelper.InvokeOnOperationRequest(litePeerTwo, request, new SendParameters());
                Assert.IsTrue(peerTwo.WaitForNextResponse(WaitTimeout));
                resp = peerTwo.GetResponseList();
                Assert.AreEqual(0, resp[0].ReturnCode, resp[0].DebugMessage);
            }
            finally
            {
                if (litePeerOne.Connected)
                {
                    PeerHelper.SimulateDisconnect(litePeerOne);
                }
                if (litePeerTwo.Connected)
                {
                    PeerHelper.SimulateDisconnect(litePeerTwo);
                }
                // get new room with same name
                var room = TestGameCache.Instance.GetRoomReference("testGame", litePeerOne);
                var game = (TestGame)room.Room;
                game.CreatePlugin();
                room.Dispose();
                game.WaitForDispose();
            }
        }

        /// recommended to be used with saved games
        [Test]
        public void JoinCheckUserIds_FailOnMatch()
        {
            var peerOne = new DummyPeer();
            var peerTwo = new DummyPeer();
            var litePeerOne = new TestHivePeer(peerOne.Protocol, peerOne, "User1");
            var litePeerTwo = new TestHivePeer(peerTwo.Protocol, peerTwo, "User1");

            try
            {
                // peer 1: joins / creates the room
                var request = GetJoinRequest();
                request.Parameters[(byte)ParameterKey.CheckUserOnJoin] = true;
                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());
                Assert.IsTrue(peerOne.WaitForNextResponse(WaitTimeout));
                var resp = peerOne.GetResponseList();
                Assert.AreEqual(0, resp[0].ReturnCode, resp[0].DebugMessage);

                // peer 1: receive own join event
                Assert.IsTrue(peerOne.WaitForNextEvent(WaitTimeout));
                var eventList = peerOne.GetEventList();
                Assert.AreEqual(1, eventList.Count);
                var eventData = eventList[0];
                Assert.AreEqual(LiteOpCode.Join, eventData.Code);
                Assert.AreEqual(1, eventData.Parameters[(byte)ParameterKey.ActorNr]);


                // this try is expected to fail, if user ids match
                PeerHelper.InvokeOnOperationRequest(litePeerTwo, request, new SendParameters());
                Assert.IsTrue(peerTwo.WaitForNextResponse(WaitTimeout));
                resp = peerTwo.GetResponseList();
                Assert.AreEqual((short)ErrorCode.JoinFailedFoundActiveJoiner, resp[0].ReturnCode, resp[0].DebugMessage);
            }
            finally
            {
                if (litePeerOne.Connected)
                {
                    PeerHelper.SimulateDisconnect(litePeerOne);
                }
                if (litePeerTwo.Connected)
                {
                    PeerHelper.SimulateDisconnect(litePeerTwo);
                }
            }
        }

        [Test]
        public void CreateGame_WrongPropertyType()
        {
            var peerOne = new DummyPeer();
            var peerTwo = new DummyPeer();
            var litePeerOne = new TestHivePeer(peerOne.Protocol, peerOne, "User1");
            var litePeerTwo = new TestHivePeer(peerTwo.Protocol, peerTwo, "User2");

            try
            {
                var gameProperties = new Hashtable
                                         {
                                             { (byte)GameParameter.IsOpen, "xx" },
                                             //{ (byte)GameParameter.IsVisible, IsVisible },
                                             //{ (byte)GameParameter.MaxPlayer, MaxPlayer },
                                         };

                // peer 1: join is expected to succeed, even if user ids match
                var request = GetJoinRequest();
                request.Parameters[(byte)ParameterKey.GameProperties] = gameProperties;
                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());
                Assert.IsTrue(peerOne.WaitForNextResponse(WaitTimeout));
                var resp = peerOne.GetResponseList();
                Assert.AreEqual((short)ErrorCode.OperationInvalid, resp[0].ReturnCode, resp[0].DebugMessage);


                Assert.IsFalse(peerOne.WaitForNextResponse(WaitTimeout));
                //resp = peerOne.GetResponseList();
                //Assert.AreEqual(0, resp[0].ReturnCode, resp[0].DebugMessage);
            }
            finally
            {
                if (litePeerOne.Connected)
                {
                    PeerHelper.SimulateDisconnect(litePeerOne);
                }
                if (litePeerTwo.Connected)
                {
                    PeerHelper.SimulateDisconnect(litePeerTwo);
                }
            }
        }

        /// for backwards compatibility
        [Test]
        public void CreateGame_AllowToCreateClosedGame()
        {
            var peerOne = new DummyPeer();
            var peerTwo = new DummyPeer();
            var litePeerOne = new TestHivePeer(peerOne.Protocol, peerOne, "User1");
            var litePeerTwo = new TestHivePeer(peerTwo.Protocol, peerTwo, "User2");

            try
            {
                var gameProperties = new Hashtable
                                         {
                                             { (byte)GameParameter.IsOpen, false },
                                             //{ (byte)GameParameter.IsVisible, IsVisible },
                                             //{ (byte)GameParameter.MaxPlayer, MaxPlayer },
                                         };

                // peer 1: join is expected to succeed, even if user ids match
                var request = GetJoinRequest();
                request.Parameters[(byte)ParameterKey.GameProperties] = gameProperties;
                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());
                Assert.IsTrue(peerOne.WaitForNextResponse(WaitTimeout));
                var resp = peerOne.GetResponseList();
                Assert.AreEqual(0, resp[0].ReturnCode, resp[0].DebugMessage);

                // peer 1: receive own join event
                Assert.IsTrue(peerOne.WaitForNextEvent(WaitTimeout));
                var eventList = peerOne.GetEventList();
                Assert.AreEqual(1, eventList.Count);
                var eventData = eventList[0];
                Assert.AreEqual(LiteOpCode.Join, eventData.Code);
                Assert.AreEqual(1, eventData.Parameters[(byte)ParameterKey.ActorNr]);


                // this try is expected to fail, with game closed error code
                PeerHelper.InvokeOnOperationRequest(litePeerTwo, request, new SendParameters());
                Assert.IsTrue(peerTwo.WaitForNextResponse(WaitTimeout));
                resp = peerTwo.GetResponseList();
                Assert.AreEqual((short)ErrorCode.GameClosed, resp[0].ReturnCode, resp[0].DebugMessage);
            }
            finally
            {
                if (litePeerOne.Connected)
                {
                    PeerHelper.SimulateDisconnect(litePeerOne);
                }
                if (litePeerTwo.Connected)
                {
                    PeerHelper.SimulateDisconnect(litePeerTwo);
                }
            }
        }

        /// <summary>
        ///   The join and leave.
        /// </summary>
        [Test]
        public void JoinAndJoin()
        {
            var peerOne = new DummyPeer();
            var peerTwo = new DummyPeer();
            var litePeerOne = new TestHivePeer(peerOne.Protocol, peerOne);
            var litePeerTwo = new TestHivePeer(peerTwo.Protocol, peerTwo);

            try
            {
                // peer 1: join
                var request = GetJoinRequest();
                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());
                Assert.IsTrue(peerOne.WaitForNextResponse(WaitTimeout));

                var responseList = peerOne.GetResponseList();
                Assert.AreEqual(1, responseList.Count);
                var response = responseList[0];
                Assert.AreEqual(1, response.Parameters[(byte)ParameterKey.ActorNr]);

                // peer 1: receive own join event
                Assert.IsTrue(peerOne.WaitForNextEvent(WaitTimeout));
                var eventList = peerOne.GetEventList();
                Assert.AreEqual(1, eventList.Count);
                var eventData = eventList[0];
                Assert.AreEqual(LiteOpCode.Join, eventData.Code);
                Assert.AreEqual(1, eventData.Parameters[(byte)ParameterKey.ActorNr]);

                // peer 2: join 
                request = GetJoinRequest();
                PeerHelper.InvokeOnOperationRequest(litePeerTwo, request, new SendParameters());
                Assert.IsTrue(peerTwo.WaitForNextResponse(WaitTimeout));

                responseList = peerTwo.GetResponseList();
                Assert.AreEqual(1, responseList.Count);
                response = responseList[0];
                Assert.AreEqual(2, response.Parameters[(byte)ParameterKey.ActorNr]);

                // peer 2: receive own join event
                Assert.IsTrue(peerTwo.WaitForNextEvent(WaitTimeout));
                eventList = peerTwo.GetEventList();
                Assert.AreEqual(1, eventList.Count);
                eventData = eventList[0];
                Assert.AreEqual(LiteOpCode.Join, eventData.Code);
                Assert.AreEqual(2, eventData.Parameters[(byte)ParameterKey.ActorNr]);

                // peer 1: receive join event of peer 2
                Assert.IsTrue(peerOne.WaitForNextEvent(WaitTimeout));
                eventList = peerOne.GetEventList();
                Assert.AreEqual(1, eventList.Count);
                eventData = eventList[0];
                Assert.AreEqual(LiteOpCode.Join, eventData.Code);
                Assert.AreEqual(2, eventData.Parameters[(byte)ParameterKey.ActorNr]);

                PeerHelper.SimulateDisconnect(litePeerTwo);
                Thread.Sleep(10);
                litePeerTwo = new TestHivePeer(peerTwo.Protocol, peerTwo);

                // peer 2: join again
                request = GetJoinRequest();
                PeerHelper.InvokeOnOperationRequest(litePeerTwo, request, new SendParameters());
                Assert.IsTrue(peerTwo.WaitForNextResponse(WaitTimeout));
                responseList = peerTwo.GetResponseList();
                Assert.AreEqual(1, responseList.Count);

                // peer 2: do not receive own leave event, receive own join event
                Assert.IsTrue(peerTwo.WaitForNextEvent(WaitTimeout));
                eventList = peerTwo.GetEventList();
                Assert.AreEqual(1, eventList.Count);
                eventData = eventList[0];
                Assert.AreEqual(LiteOpCode.Join, eventData.Code);
                Assert.AreEqual(3, eventData.Parameters[(byte)ParameterKey.ActorNr]);

                // peer 1: receive leave and event of peer 2
                Assert.IsTrue(peerOne.WaitForNextEvent(WaitTimeout));
                eventList = peerOne.GetEventList();
                Assert.GreaterOrEqual(eventList.Count, 1);
                eventData = eventList[0];
                Assert.AreEqual(LiteOpCode.Leave, eventData.Code);
                Assert.AreEqual(2, eventData.Parameters[(byte)ParameterKey.ActorNr]);

                if (eventList.Count == 1)
                {
                    // waiting for join event
                    Assert.IsTrue(peerOne.WaitForNextEvent(WaitTimeout));
                    eventList = peerOne.GetEventList();
                    Assert.AreEqual(1, eventList.Count);
                    eventData = eventList[0];
                }
                else
                {
                    eventData = eventList[1];
                }

                Assert.AreEqual(LiteOpCode.Join, eventData.Code);
                Assert.AreEqual(3, eventData.Parameters[(byte)ParameterKey.ActorNr]);
            }
            finally
            {
                PeerHelper.SimulateDisconnect(litePeerOne);
                PeerHelper.SimulateDisconnect(litePeerTwo);
            }
        }

        /// <summary>
        ///   The join and leave.
        /// </summary>
        [Test]
        public void JoinAndLeave()
        {
            var peerOne = new DummyPeer();
            var peerTwo = new DummyPeer();
            var litePeerOne = new TestHivePeer(peerOne.Protocol, peerOne);
            var litePeerTwo = new TestHivePeer(peerTwo.Protocol, peerTwo);

            try
            {
                // peer 1: join
                var request = GetJoinRequest();
                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());
                Assert.IsTrue(peerOne.WaitForNextResponse(WaitTimeout));

                var responseList = peerOne.GetResponseList();
                Assert.AreEqual(1, responseList.Count);
                var response = responseList[0];
                Assert.AreEqual(1, response.Parameters[(byte)ParameterKey.ActorNr]);

                // peer 1: receive own join event
                Assert.IsTrue(peerOne.WaitForNextEvent(WaitTimeout));
                var eventList = peerOne.GetEventList();
                Assert.AreEqual(1, eventList.Count);
                var eventData = eventList[0];
                Assert.AreEqual(LiteOpCode.Join, eventData.Code);
                Assert.AreEqual(1, eventData.Parameters[(byte)ParameterKey.ActorNr]);

                // peer 2: join 
                request = GetJoinRequest();
                PeerHelper.InvokeOnOperationRequest(litePeerTwo, request, new SendParameters());
                Assert.IsTrue(peerTwo.WaitForNextResponse(WaitTimeout));

                responseList = peerTwo.GetResponseList();
                Assert.AreEqual(1, responseList.Count);
                response = responseList[0];
                Assert.AreEqual(2, response.Parameters[(byte)ParameterKey.ActorNr]);

                // peer 2: receive own join event
                Assert.IsTrue(peerTwo.WaitForNextEvent(WaitTimeout));
                eventList = peerTwo.GetEventList();
                Assert.AreEqual(1, eventList.Count);
                eventData = eventList[0];
                Assert.AreEqual(LiteOpCode.Join, eventData.Code);
                Assert.AreEqual(2, eventData.Parameters[(byte)ParameterKey.ActorNr]);

                // peer 1: receive join event of peer 2
                Assert.IsTrue(peerOne.WaitForNextEvent(WaitTimeout));
                eventList = peerOne.GetEventList();
                Assert.AreEqual(1, eventList.Count);
                eventData = eventList[0];
                Assert.AreEqual(LiteOpCode.Join, eventData.Code);
                Assert.AreEqual(2, eventData.Parameters[(byte)ParameterKey.ActorNr]);

                // peer 2: leave
                request = GetLeaveRequest();
                PeerHelper.InvokeOnOperationRequest(litePeerTwo, request, new SendParameters());
                Assert.IsTrue(peerTwo.WaitForNextResponse(WaitTimeout));
                responseList = peerTwo.GetResponseList();
                Assert.AreEqual(1, responseList.Count);

                // peer 2: do not receive own leave event
                eventList = peerTwo.GetEventList();
                Assert.AreEqual(0, eventList.Count);

                // peer 1: receive leave event of peer 2
                Assert.IsTrue(peerOne.WaitForNextEvent(WaitTimeout));
                eventList = peerOne.GetEventList();
                Assert.AreEqual(1, eventList.Count);
                eventData = eventList[0];
                Assert.AreEqual(LiteOpCode.Leave, eventData.Code);
                Assert.AreEqual(2, eventData.Parameters[(byte)ParameterKey.ActorNr]);
            }
            finally
            {
                PeerHelper.SimulateDisconnect(litePeerOne);
                PeerHelper.SimulateDisconnect(litePeerTwo);
            }
        }

        /// <summary>
        /// Expected flow:
        ///  - #PeersCount peers join a game
        ///  - check all peers receive response and join events
        ///  - peer#3 disconnects
        ///  - wait for game to remove peer
        ///  - check other peers received disconnect event
        ///  - peer#3 rejoins
        ///  - verify game disposed cleanup timer
        /// TBD check other peers revived join event
        ///  - peer#3 disconnects
        ///  - wait for game to remove peer
        ///  - check other peers received disconnect event
        ///  - wait for game to cleanup actor
        ///  - check other peers received leave event
        ///  
        /// </summary>
        [Test]
        public void JoinDisconnectRejoin()
        {
            const int peersCount = 5;
            const int playerTtl = 100;

            var nativePeer = new DummyPeer();
            var rejoiningPeer = new TestHivePeer(nativePeer.Protocol, nativePeer);
            RoomReference room = null;

            var nativePeers = new DummyPeer[peersCount];
            var litePeers = new TestHivePeer[peersCount];

            for (var i = 0; i < peersCount; ++i)
            {
                var peer = new DummyPeer();
                nativePeers[i] = peer;
                litePeers[i] = new TestHivePeer(peer.Protocol, peer);
            }

            try
            {
                OperationRequest request;
                for (var i = 0; i < peersCount; ++i)
                {
                    var peer = nativePeers[i];

                    request = GetJoinRequest();
                    request.Parameters[(byte)ParameterKey.PlayerTTL] = playerTtl;
                    PeerHelper.InvokeOnOperationRequest(litePeers[i], request, new SendParameters());
                    Assert.IsTrue(peer.WaitForNextResponse(WaitTimeout));
                }

                Thread.Sleep(50); // to get all events before check

                for (var i = 0; i < peersCount; ++i)
                {
                    var peer = nativePeers[i];

                    var responseList = peer.GetResponseList();
                    Assert.AreEqual(1, responseList.Count);

                    var response = responseList[0];
                    Assert.AreEqual(i + 1, response.Parameters[(byte)ParameterKey.ActorNr]);

                    // peer 1: receive own join event
                    Assert.IsTrue(peer.WaitForNextEvent(WaitTimeout));
                    var eventList = peer.GetEventList();

                    Assert.AreEqual(peersCount - i, eventList.Count);
                    var eventData = eventList[0];
                    Assert.AreEqual(LiteOpCode.Join, eventData.Code);
                    Assert.AreEqual(i + 1, eventData.Parameters[(byte)ParameterKey.ActorNr]);
                }

                var leaveLitePeer = litePeers[3];

                litePeers[3] = null;
                nativePeers[3] = null;

                room = TestGameCache.Instance.GetRoomReference("testGame", leaveLitePeer);
                Assert.AreNotEqual(null, room);
                var game = (TestGame)room.Room;

                PeerHelper.SimulateDisconnect(leaveLitePeer);

                game.WaitRemovePeerFromGame();

                // check that all others get disconnect notification
                for (var i = 0; i < peersCount; ++i)
                {
                    var peer = nativePeers[i];
                    if (peer != null)
                    {
                        Assert.IsTrue(peer.WaitForNextEvent(WaitTimeout));
                        var eventList = peer.GetEventList();

                        Assert.AreEqual(1, eventList.Count);
                        var eventData = eventList[0];
                        // TBD in client LiteEventCode.Disconnect
                        Assert.AreEqual(LiteOpCode.Leave, eventData.Code);
                        // Assert.AreEqual((byte)EventCode.Disconnect, eventData.Code); 
                        Assert.AreEqual(4, eventData.Parameters[(byte)ParameterKey.ActorNr]);
                        Assert.AreEqual(true, eventData.Parameters[(byte)ParameterKey.IsInactive]);
                    }
                }

                Assert.AreEqual(1, game.ActorsManager.InactiveActorsCount);
                Assert.AreEqual(peersCount - 1, game.GetActors().ToList().Count);

                var actorNr = game.ActorsManager.InactiveActors.ToArray()[0].ActorNr;

                request = GetJoinRequest();
                request.Parameters[(byte)ParameterKey.ActorNr] = actorNr;
                request.Parameters[(byte)ParameterKey.JoinMode] = JoinModeConstants.RejoinOnly;
                PeerHelper.InvokeOnOperationRequest(rejoiningPeer, request, new SendParameters());

                Assert.IsTrue(nativePeer.WaitForNextResponse(WaitTimeout));

                Assert.AreEqual(0, game.ActorsManager.InactiveActorsCount);
                Assert.AreEqual(peersCount, game.GetActors().ToList().Count);
                Assert.IsNotNull(game.GetActors().Where(actor => actorNr == actor.ActorNr));

                // check that all others got join notification
                for (var i = 0; i < peersCount; ++i)
                {
                    var peer = nativePeers[i];
                    if (peer != null)
                    {
                        Assert.IsTrue(peer.WaitForNextEvent(WaitTimeout));
                        var eventList = peer.GetEventList();

                        Assert.AreEqual(1, eventList.Count);
                        var eventData = eventList[0];
                        Assert.AreEqual(LiteOpCode.Join, eventData.Code);
                        Assert.AreEqual(4, eventData.Parameters[(byte)ParameterKey.ActorNr]);
                    }
                }

                Assert.AreEqual(false, game.WaitOnCleanupActor(2 * playerTtl));

                Assert.AreEqual(0, game.ActorsManager.InactiveActorsCount);
                Assert.AreEqual(peersCount, game.GetActors().ToList().Count);

                PeerHelper.SimulateDisconnect(rejoiningPeer);

                game.WaitRemovePeerFromGame();

                // check that all others get disconnect notification
                for (var i = 0; i < peersCount; ++i)
                {
                    var peer = nativePeers[i];
                    if (peer != null)
                    {
                        Assert.IsTrue(peer.WaitForNextEvent(WaitTimeout));
                        var eventList = peer.GetEventList();

                        Assert.AreEqual(1, eventList.Count);
                        var eventData = eventList[0];
                        // TBD in client LiteEventCode.Disconnect
                        Assert.AreEqual(LiteOpCode.Leave, eventData.Code);
                        Assert.AreEqual(true, eventData.Parameters[(byte)ParameterKey.IsInactive]);
                        Assert.AreEqual(4, eventData.Parameters[(byte)ParameterKey.ActorNr]);
                    }
                }

                Assert.AreEqual(1, game.ActorsManager.InactiveActorsCount);
                Assert.AreEqual(peersCount - 1, game.GetActors().ToList().Count);

                Assert.AreEqual(true, game.WaitOnCleanupActor(2 * playerTtl));

                // check that all others get leave notification
                for (var i = 0; i < peersCount; ++i)
                {
                    var peer = nativePeers[i];
                    if (peer != null)
                    {
                        Assert.IsTrue(peer.WaitForNextEvent(WaitTimeout));
                        var eventList = peer.GetEventList();

                        Assert.AreEqual(1, eventList.Count);
                        var eventData = eventList[0];
                        Assert.AreEqual(LiteEventCode.Leave, eventData.Code);

                        eventData.Parameters.TryGetValue((byte)ParameterKey.IsInactive, out var isInactive);
                        Assert.AreNotEqual(true, isInactive);
                        Assert.AreEqual(4, eventData.Parameters[(byte)ParameterKey.ActorNr]);
                    }
                }
            }
            finally
            {
                room?.Dispose();

                foreach (var p in litePeers)
                {
                    if (p != null)
                    {
                        PeerHelper.SimulateDisconnect(p);
                    }
                }

                if (rejoiningPeer.Connected)
                {
                    PeerHelper.SimulateDisconnect(rejoiningPeer);
                }
            }
        }

        [Test]
        public void JoinDisconnectRejoinWithGroups()
        {
            var peerOne = new DummyPeer();
            var peerTwo = new DummyPeer();
            var peerThree = new DummyPeer();
            var litePeerOne = new TestHivePeer(peerOne.Protocol, peerOne);
            var litePeerTwo = new TestHivePeer(peerTwo.Protocol, peerTwo);
            var litePeerThree = new TestHivePeer(peerThree.Protocol, peerThree);

            try
            {
                var parameter = new Dictionary<byte, object>
                {
                    {(byte) ParameterKey.PlayerTTL, 500000}
                };

                // join peers
                JoinPeer(peerOne, litePeerOne, 1, parameter);

                JoinPeer(peerTwo, litePeerTwo, 2);
                ReceiveJoinEvent(peerOne, 2);

                JoinPeer(peerThree, litePeerThree, 3);
                ReceiveJoinEvent(peerOne, 3);
                ReceiveJoinEvent(peerTwo, 3);

                // join peer2 and peer3 to group 100
                var groupsToJoin = new byte[] { 100 };
                var request = GetChangeGroups(groupsToJoin, null);
                PeerHelper.InvokeOnOperationRequest(litePeerTwo, request, new SendParameters());
                PeerHelper.InvokeOnOperationRequest(litePeerThree, request, new SendParameters());

                // currently no operation response for change group requests
                ////Assert.IsTrue(peerTwo.WaitForNextResponse(WaitTimeout), "Timed out waiting for response.");
                ////Assert.IsTrue(peerThree.WaitForNextResponse(WaitTimeout), "Timed out waiting for response.");

                // raise event on group 100
                request = GetRaiseEventRequest(1, null, null, null, null, 100);
                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());
                ReceiveEvent(peerTwo, 1);
                ReceiveEvent(peerThree, 1);

                // remove peer2 from game with Parameter IsComingBack = true
                // groups should be restored after rejoin
                request = GetLeaveRequest();
                request.Parameters = new Dictionary<byte, object>
                {
                    {(byte) ParameterKey.IsInactive, true}
                };
                PeerHelper.InvokeOnOperationRequest(litePeerTwo, request, new SendParameters());
                Assert.IsTrue(peerTwo.WaitForNextResponse(WaitTimeout));
                var responseList = peerTwo.GetResponseList();
                Assert.AreEqual(1, responseList.Count);
                Assert.AreEqual((byte)OperationCode.Leave, responseList[0].OperationCode);

                ReceiveEvent(peerOne, (byte)EventCode.Leave);
                ReceiveEvent(peerThree, (byte)EventCode.Leave);

                //reconnect
                PeerHelper.SimulateDisconnect(litePeerTwo);
                litePeerTwo = new TestHivePeer(peerTwo.Protocol, peerTwo);

                // raise event on group 100
                request = GetRaiseEventRequest(1, null, null, null, null, 100);
                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());
                Assert.IsFalse(peerTwo.WaitForNextEvent(WaitTimeout));
                ReceiveEvent(peerThree, 1);

                // rejoin peer2
                parameter.Clear();
                parameter.Add((byte)ParameterKey.ActorNr, 2);
                parameter[(byte)ParameterKey.JoinMode] = JoinModeConstants.RejoinOnly;
                JoinPeer(peerTwo, litePeerTwo, 2, parameter);
                ReceiveJoinEvent(peerOne, 2);
                ReceiveJoinEvent(peerThree, 2);

                // raise event on group 100
                request = GetRaiseEventRequest(1, null, null, null, null, 100);
                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());
                ReceiveEvent(peerTwo, 1);
                ReceiveEvent(peerThree, 1);
            }
            finally
            {
                PeerHelper.SimulateDisconnect(litePeerOne);
                PeerHelper.SimulateDisconnect(litePeerTwo);
                PeerHelper.SimulateDisconnect(litePeerThree);
            }
        }

        [Test]
        public void LessThenMaxEmptyRoomLiveTimeTest()
        {
            var peerOne = new DummyPeer();
            var peerTwo = new DummyPeer();
            var litePeerOne = new TestHivePeer(peerOne.Protocol, peerOne);
            var litePeerTwo = new TestHivePeer(peerTwo.Protocol, peerTwo);
            RoomReference room = null;

            const int roomTtl = 500;
            try
            {
                room = TestGameCache.Instance.GetRoomReference("testGame", litePeerOne);
                Assert.AreEqual(1000, room.Room.MaxEmptyRoomTTL);

                // peer 1: join
                var request = GetJoinRequest();
                request.Parameters.Add((byte)ParameterKey.EmptyRoomLiveTime, roomTtl);

                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());
                Assert.IsTrue(peerOne.WaitForNextResponse(WaitTimeout));

                Assert.AreEqual(roomTtl, room.Room.EmptyRoomLiveTime);
                PeerHelper.SimulateDisconnect(litePeerOne);
                room.Dispose();
                Thread.Sleep(2 * roomTtl);
            }
            finally
            {
                room?.Dispose();

                if (litePeerOne.Connected)
                {
                    PeerHelper.SimulateDisconnect(litePeerOne);
                }
                if (litePeerTwo.Connected)
                {
                    PeerHelper.SimulateDisconnect(litePeerTwo);
                }
            }
        }

        #region Raise Events Tests
        /// <summary>
        ///   Test for sending event to target actor numbers.
        /// </summary>
        [Test]
        public void RaiseEventActors()
        {
            var peerOne = new DummyPeer();
            var peerTwo = new DummyPeer();
            var peerThree = new DummyPeer();
            var litePeerOne = new TestHivePeer(peerOne.Protocol, peerOne);
            var litePeerTwo = new TestHivePeer(peerTwo.Protocol, peerTwo);
            var litePeerThree = new TestHivePeer(peerThree.Protocol, peerThree);

            try
            {
                // peer 1: join
                JoinPeer(peerOne, litePeerOne, 1);

                // peer 2: join 
                JoinPeer(peerTwo, litePeerTwo, 2);

                // peer 1: receive join event of peer 2
                ReceiveJoinEvent(peerOne, 2);

                // peer 2: join 
                JoinPeer(peerThree, litePeerThree, 3);

                // peer 1: receive join event of peer 3
                ReceiveJoinEvent(peerOne, 3);

                // peer 2: receive join event of peer 3
                ReceiveJoinEvent(peerTwo, 3);

                // peer 1: send to peer 1 + 3 (ordered)
                byte code = 100;
                var data = new Hashtable { { 1, "value1" } };
                var request = GetRaiseEventRequest(code, data, null, null, new[] { 1, 2, 3 });
                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());

                // Peer 1 + 2 + 3: Receive data
                var @event = ReceiveEvent(peerOne, code);
                Assert.AreEqual(code, @event.Code);
                var eventData = (Hashtable)@event.Parameters[(byte)ParameterKey.Data];
                Assert.AreEqual("value1", eventData[1]);

                @event = ReceiveEvent(peerTwo, code);
                Assert.AreEqual(code, @event.Code);
                eventData = (Hashtable)@event.Parameters[(byte)ParameterKey.Data];
                Assert.AreEqual("value1", eventData[1]);

                @event = ReceiveEvent(peerThree, code);
                Assert.AreEqual(code, @event.Code);
                eventData = (Hashtable)@event.Parameters[(byte)ParameterKey.Data];
                Assert.AreEqual("value1", eventData[1]);

                // peer 3: send to peer 3 + 2 + 1 (unordered)
                code++;
                request = GetRaiseEventRequest(code, data, null, null, new[] { 3, 2, 1 });
                PeerHelper.InvokeOnOperationRequest(litePeerThree, request, new SendParameters());

                // Peer 1 + 2 + 3: Receive data
                @event = ReceiveEvent(peerOne, code);
                Assert.AreEqual(code, @event.Code);
                eventData = (Hashtable)@event.Parameters[(byte)ParameterKey.Data];
                Assert.AreEqual("value1", eventData[1]);

                @event = ReceiveEvent(peerTwo, code);
                Assert.AreEqual(code, @event.Code);
                eventData = (Hashtable)@event.Parameters[(byte)ParameterKey.Data];
                Assert.AreEqual("value1", eventData[1]);

                @event = ReceiveEvent(peerThree, code);
                Assert.AreEqual(code, @event.Code);
                eventData = (Hashtable)@event.Parameters[(byte)ParameterKey.Data];
                Assert.AreEqual("value1", eventData[1]);

                // peer 3: send to peer 3 + 2 + 1 + 2 + 3 + 3 (unordered, duplicate)
                code++;
                request = GetRaiseEventRequest(code, data, null, null, new[] { 3, 2, 1, 2, 3, 3 });
                PeerHelper.InvokeOnOperationRequest(litePeerThree, request, new SendParameters());

                // Peer 1 + 2 + 3: Receive data
                @event = ReceiveEvent(peerOne, code);
                Assert.AreEqual(code, @event.Code);
                eventData = (Hashtable)@event.Parameters[(byte)ParameterKey.Data];
                Assert.AreEqual("value1", eventData[1]);

                var events = ReceiveEvents(peerTwo, 1);
                foreach (var ev in events)
                {
                    @event = ev;
                    Assert.AreEqual(code, @event.Code);
                    eventData = (Hashtable)@event.Parameters[(byte)ParameterKey.Data];
                    Assert.AreEqual("value1", eventData[1]);
                }

                events = ReceiveEvents(peerThree, 1);
                foreach (var ev in events)
                {
                    @event = ev;
                    Assert.AreEqual(code, @event.Code);
                    eventData = (Hashtable)@event.Parameters[(byte)ParameterKey.Data];
                    Assert.AreEqual("value1", eventData[1]);
                }
            }
            finally
            {
                PeerHelper.SimulateDisconnect(litePeerOne);
                PeerHelper.SimulateDisconnect(litePeerTwo);
                PeerHelper.SimulateDisconnect(litePeerThree);
            }
        }


        /// <summary>
        ///   Tests if existing and new peer receive cached events.
        /// </summary>
        [Test]
        public void RaiseEventCacheMerge()
        {
            var peerOne = new DummyPeer();
            var peerTwo = new DummyPeer();
            var peerThree = new DummyPeer();
            var litePeerOne = new TestHivePeer(peerOne.Protocol, peerOne);
            var litePeerTwo = new TestHivePeer(peerTwo.Protocol, peerTwo);
            var litePeerThree = new TestHivePeer(peerThree.Protocol, peerThree);

            try
            {
                // peer 1: join
                JoinPeer(peerOne, litePeerOne, 1);

                // peer 2: join 
                JoinPeer(peerTwo, litePeerTwo, 2);

                // peer 1: receive join event of peer 2
                ReceiveJoinEvent(peerOne, 2);

                // peer 1: send to all others
                const byte code = 100;
                var data = new Hashtable { { 1, "value1" } };
                var request = GetRaiseEventRequest(code, data, (byte)CacheOperation.MergeCache, null, null);
                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());

                // Peer 2: Receive data
                var @event = ReceiveEvent(peerTwo, code);
                Assert.AreEqual(code, @event.Code);
                var eventData = (Hashtable)@event.Parameters[(byte)ParameterKey.Data];
                Assert.AreEqual("value1", eventData[1]);

                // peer 3: join 
                request = GetJoinRequest();
                PeerHelper.InvokeOnOperationRequest(litePeerThree, request, new SendParameters());
                Assert.IsTrue(peerThree.WaitForNextResponse(WaitTimeout));

                var responseList = peerThree.GetResponseList();
                Assert.AreEqual(1, responseList.Count);
                var response = responseList[0];
                Assert.AreEqual(3, response.Parameters[(byte)ParameterKey.ActorNr]);

                // peer 3: receive own join event
                Assert.IsTrue(peerThree.WaitForNextEvent(WaitTimeout));
                var eventList = peerThree.GetEventList();
                if (eventList.Count < 2)
                {
                    Assert.IsTrue(peerThree.WaitForNextEvent(WaitTimeout));
                    eventList.AddRange(peerThree.GetEventList());
                }

                Assert.AreEqual(2, eventList.Count);
                @event = eventList[0];
                Assert.AreEqual(LiteOpCode.Join, @event.Code);
                Assert.AreEqual(3, @event.Parameters[(byte)ParameterKey.ActorNr]);

                @event = eventList[1];
                Assert.AreEqual(code, @event.Code);
                eventData = (Hashtable)@event.Parameters[(byte)ParameterKey.Data];
                Assert.AreEqual("value1", eventData[1]);

                // peer 1: receive join event of peer 3
                ReceiveJoinEvent(peerOne, 3);

                // peer 2: receive join event of peer 3
                ReceiveJoinEvent(peerTwo, 3);
            }
            finally
            {
                PeerHelper.SimulateDisconnect(litePeerOne);
                PeerHelper.SimulateDisconnect(litePeerTwo);
                PeerHelper.SimulateDisconnect(litePeerThree);
            }
        }

        /// <summary>
        ///   Tests if events are deleted when there is no content.
        /// </summary>
        [Test]
        public void RaiseEventCacheMerge2()
        {
            var peerOne = new DummyPeer();
            var peerTwo = new DummyPeer();
            var peerThree = new DummyPeer();
            var litePeerOne = new TestHivePeer(peerOne.Protocol, peerOne);
            var litePeerTwo = new TestHivePeer(peerTwo.Protocol, peerTwo);
            var litePeerThree = new TestHivePeer(peerThree.Protocol, peerThree);

            //            Thread.Sleep(1100);
            try
            {
                // peer 1: join
                JoinPeer(peerOne, litePeerOne, 1);

                // peer 2: join 
                JoinPeer(peerTwo, litePeerTwo, 2);

                // peer 1: receive join event of peer 2
                ReceiveJoinEvent(peerOne, 2);

                // peer 1: send to all others
                byte code = 100;
                var data = new Hashtable { { 1, "value1" } };
                var request = GetRaiseEventRequest(code, data, (byte)CacheOperation.MergeCache, null, null);
                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());

                // Peer 2: Receive data
                var @event = ReceiveEvent(peerTwo, code);
                Assert.AreEqual(code, @event.Code);
                var eventData = (Hashtable)@event.Parameters[(byte)ParameterKey.Data];
                Assert.AreEqual("value1", eventData[1]);

                code = 101;
                data = new Hashtable { { 1, "value2" } };
                request = GetRaiseEventRequest(code, data, (byte)CacheOperation.MergeCache, null, null);
                PeerHelper.InvokeOnOperationRequest(litePeerTwo, request, new SendParameters());

                // Peer 1: Receive data
                @event = ReceiveEvent(peerOne, code);
                Assert.AreEqual(code, @event.Code);
                eventData = (Hashtable)@event.Parameters[(byte)ParameterKey.Data];
                Assert.AreEqual("value2", eventData[1]);

                // Peer 1: delete event
                code = 100;
                request = GetRaiseEventRequest(code, null, (byte)CacheOperation.MergeCache, null, null);
                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());

                // Peer 2: Receive data
                @event = ReceiveEvent(peerTwo, code);
                Assert.AreEqual(code, @event.Code);
                Assert.IsFalse(@event.Parameters.ContainsKey((byte)ParameterKey.Data));

                // Peer 2: add more to event
                code = 101;
                data = new Hashtable { { 2, "value3" }, { 3, "value4" } };
                request = GetRaiseEventRequest(code, data, (byte)CacheOperation.MergeCache, null, null);
                PeerHelper.InvokeOnOperationRequest(litePeerTwo, request, new SendParameters());

                // Peer 1: Receive data
                @event = ReceiveEvent(peerOne, code);
                Assert.AreEqual(code, @event.Code);
                eventData = (Hashtable)@event.Parameters[(byte)ParameterKey.Data];
                Assert.AreEqual("value3", eventData[2]);
                Assert.AreEqual("value4", eventData[3]);
                Assert.IsFalse(eventData.ContainsKey(1));

                // Peer 2: remove from event
                code = 101;
                data = new Hashtable { { 2, null } };
                request = GetRaiseEventRequest(code, data, (byte)CacheOperation.MergeCache, null, null);
                PeerHelper.InvokeOnOperationRequest(litePeerTwo, request, new SendParameters());

                // Peer 1: Receive data
                @event = ReceiveEvent(peerOne, code);
                Assert.AreEqual(code, @event.Code);
                eventData = (Hashtable)@event.Parameters[(byte)ParameterKey.Data];
                Assert.IsNull(eventData[2]);
                Assert.IsFalse(eventData.ContainsKey(1));
                Assert.IsTrue(eventData.ContainsKey(2));
                Assert.IsFalse(eventData.ContainsKey(3));

                // peer 3: join 
                request = GetJoinRequest();
                PeerHelper.InvokeOnOperationRequest(litePeerThree, request, new SendParameters());
                Assert.IsTrue(peerThree.WaitForNextResponse(WaitTimeout));

                var responseList = peerThree.GetResponseList();
                Assert.AreEqual(1, responseList.Count);
                var response = responseList[0];
                Assert.AreEqual(3, response.Parameters[(byte)ParameterKey.ActorNr]);

                // peer 3: receive own join event and event 101 - event 100 was removed
                Assert.IsTrue(peerThree.WaitForNextEvent(WaitTimeout));
                var eventList = peerThree.GetEventList();
                if (eventList.Count < 2)
                {
                    Assert.IsTrue(peerThree.WaitForNextEvent(WaitTimeout));
                    eventList.AddRange(peerThree.GetEventList());
                }

                Assert.AreEqual(2, eventList.Count);
                @event = eventList[0];
                Assert.AreEqual(LiteOpCode.Join, @event.Code);
                Assert.AreEqual(3, @event.Parameters[(byte)ParameterKey.ActorNr]);

                code = 101;
                @event = eventList[1];
                Assert.AreEqual(code, @event.Code);
                eventData = (Hashtable)@event.Parameters[(byte)ParameterKey.Data];
                Assert.AreEqual("value2", eventData[1]);
                Assert.AreEqual("value4", eventData[3]);
                Assert.IsFalse(eventData.ContainsKey(2));

                // peer 1: receive join event of peer 3
                ReceiveJoinEvent(peerOne, 3);

                // peer 2: receive join event of peer 3
                ReceiveJoinEvent(peerTwo, 3);
            }
            finally
            {
                PeerHelper.SimulateDisconnect(litePeerOne);
                PeerHelper.SimulateDisconnect(litePeerTwo);
                PeerHelper.SimulateDisconnect(litePeerThree);
            }
        }

        /// <summary>
        ///   Tests if events are removed.
        /// </summary>
        [Test]
        public void RaiseEventCacheRemove()
        {
            var peerOne = new DummyPeer();
            var peerTwo = new DummyPeer();
            var peerThree = new DummyPeer();
            var litePeerOne = new TestHivePeer(peerOne.Protocol, peerOne);
            var litePeerTwo = new TestHivePeer(peerTwo.Protocol, peerTwo);
            var litePeerThree = new TestHivePeer(peerThree.Protocol, peerThree);

            try
            {
                // peer 1: join
                JoinPeer(peerOne, litePeerOne, 1);

                // peer 2: join 
                JoinPeer(peerTwo, litePeerTwo, 2);

                // peer 1: receive join event of peer 2
                ReceiveJoinEvent(peerOne, 2);

                // peer 1: send to all others
                byte code = 100;
                var data = new Hashtable { { 1, "value1" } };
                var request = GetRaiseEventRequest(code, data, (byte)CacheOperation.ReplaceCache, null, null);
                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());

                // Peer 2: Receive data
                var @event = ReceiveEvent(peerTwo, code);
                Assert.AreEqual(code, @event.Code);
                var eventData = (Hashtable)@event.Parameters[(byte)ParameterKey.Data];
                Assert.AreEqual("value1", eventData[1]);

                data = new Hashtable { { 2, "value2" } };
                request = GetRaiseEventRequest(code, data, (byte)CacheOperation.RemoveCache, null, null);
                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());

                // Peer 2: Receive data
                @event = ReceiveEvent(peerTwo, code);
                Assert.AreEqual(code, @event.Code);
                eventData = (Hashtable)@event.Parameters[(byte)ParameterKey.Data];
                Assert.AreEqual("value2", eventData[2]);
                Assert.IsFalse(eventData.ContainsKey(1));

                // Peer 1: send event 101
                code++;
                data = new Hashtable { { 3, "value3" } };
                request = GetRaiseEventRequest(code, data, (byte)CacheOperation.MergeCache, null, null);
                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());

                // Peer 2: Receive data
                @event = ReceiveEvent(peerTwo, code);
                Assert.AreEqual(code, @event.Code);
                eventData = (Hashtable)@event.Parameters[(byte)ParameterKey.Data];
                Assert.AreEqual("value3", eventData[3]);
                Assert.IsFalse(eventData.ContainsKey(1));
                Assert.IsFalse(eventData.ContainsKey(2));

                // peer 3: join 
                request = GetJoinRequest();
                PeerHelper.InvokeOnOperationRequest(litePeerThree, request, new SendParameters());
                Assert.IsTrue(peerThree.WaitForNextResponse(WaitTimeout));

                var responseList = peerThree.GetResponseList();
                Assert.AreEqual(1, responseList.Count);
                var response = responseList[0];
                Assert.AreEqual(3, response.Parameters[(byte)ParameterKey.ActorNr]);

                // peer 3: receive own join event and event 101 - event 100 was removed
                Assert.IsTrue(peerThree.WaitForNextEvent(WaitTimeout));
                var eventList = peerThree.GetEventList();
                if (eventList.Count < 2)
                {
                    Assert.IsTrue(peerThree.WaitForNextEvent(WaitTimeout));
                    eventList.AddRange(peerThree.GetEventList());
                }

                Assert.AreEqual(2, eventList.Count);
                @event = eventList[0];
                Assert.AreEqual(LiteOpCode.Join, @event.Code);
                Assert.AreEqual(3, @event.Parameters[(byte)ParameterKey.ActorNr]);

                @event = eventList[1];
                Assert.AreEqual(code, @event.Code);
                eventData = (Hashtable)@event.Parameters[(byte)ParameterKey.Data];
                Assert.AreEqual("value3", eventData[3]);
                Assert.IsFalse(eventData.ContainsKey(2));
                Assert.IsFalse(eventData.ContainsKey(1));

                // peer 1: receive join event of peer 3
                ReceiveJoinEvent(peerOne, 3);

                // peer 2: receive join event of peer 3
                ReceiveJoinEvent(peerTwo, 3);
            }
            finally
            {
                PeerHelper.SimulateDisconnect(litePeerOne);
                PeerHelper.SimulateDisconnect(litePeerTwo);
                PeerHelper.SimulateDisconnect(litePeerThree);
            }
        }

        /// <summary>
        ///   Tests if events are replaced.
        /// </summary>
        [Test]
        public void RaiseEventCacheReplace()
        {
            var peerOne = new DummyPeer();
            var peerTwo = new DummyPeer();
            var peerThree = new DummyPeer();
            var litePeerOne = new TestHivePeer(peerOne.Protocol, peerOne);
            var litePeerTwo = new TestHivePeer(peerTwo.Protocol, peerTwo);
            var litePeerThree = new TestHivePeer(peerThree.Protocol, peerThree);

            try
            {
                // peer 1: join
                JoinPeer(peerOne, litePeerOne, 1);

                // peer 2: join 
                JoinPeer(peerTwo, litePeerTwo, 2);

                // peer 1: receive join event of peer 2
                ReceiveJoinEvent(peerOne, 2);

                // peer 1: send to all others
                const byte code = 100;
                var data = new Hashtable { { 1, "value1" } };
                var request = GetRaiseEventRequest(code, data, (byte)CacheOperation.ReplaceCache, null, null);
                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());

                // Peer 2: Receive data
                var @event = ReceiveEvent(peerTwo, code);
                Assert.AreEqual(code, @event.Code);
                var eventData = (Hashtable)@event.Parameters[(byte)ParameterKey.Data];
                Assert.AreEqual("value1", eventData[1]);

                data = new Hashtable { { 2, "value2" } };
                request = GetRaiseEventRequest(code, data, (byte)CacheOperation.ReplaceCache, null, null);
                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());

                // Peer 2: Receive data
                @event = ReceiveEvent(peerTwo, code);
                Assert.AreEqual(code, @event.Code);
                eventData = (Hashtable)@event.Parameters[(byte)ParameterKey.Data];
                Assert.AreEqual("value2", eventData[2]);
                Assert.IsFalse(eventData.ContainsKey(1));

                // peer 3: join 
                request = GetJoinRequest();
                PeerHelper.InvokeOnOperationRequest(litePeerThree, request, new SendParameters());
                Assert.IsTrue(peerThree.WaitForNextResponse(WaitTimeout));

                var responseList = peerThree.GetResponseList();
                Assert.AreEqual(1, responseList.Count);
                var response = responseList[0];
                Assert.AreEqual(3, response.Parameters[(byte)ParameterKey.ActorNr]);

                // peer 3: receive own join event and event 101 - event 100 was removed
                Assert.IsTrue(peerThree.WaitForNextEvent(WaitTimeout));
                var eventList = peerThree.GetEventList();
                if (eventList.Count < 2)
                {
                    Assert.IsTrue(peerThree.WaitForNextEvent(WaitTimeout));
                    eventList.AddRange(peerThree.GetEventList());
                }

                Assert.AreEqual(2, eventList.Count);
                @event = eventList[0];
                Assert.AreEqual(LiteOpCode.Join, @event.Code);
                Assert.AreEqual(3, @event.Parameters[(byte)ParameterKey.ActorNr]);

                @event = eventList[1];
                Assert.AreEqual(code, @event.Code);
                eventData = (Hashtable)@event.Parameters[(byte)ParameterKey.Data];
                Assert.AreEqual("value2", eventData[2]);
                Assert.IsFalse(eventData.ContainsKey(1));

                // peer 1: receive join event of peer 3
                ReceiveJoinEvent(peerOne, 3);

                // peer 2: receive join event of peer 3
                ReceiveJoinEvent(peerTwo, 3);
            }
            finally
            {
                PeerHelper.SimulateDisconnect(litePeerOne);
                PeerHelper.SimulateDisconnect(litePeerTwo);
                PeerHelper.SimulateDisconnect(litePeerThree);
            }
        }

        /// <summary>
        ///   Tests if existing and new peer receive cached events.
        /// </summary>
        [Test]
        public void RaiseEventCacheRoom()
        {
            var peerOne = new DummyPeer();
            var peerTwo = new DummyPeer();
            var peerThree = new DummyPeer();
            var litePeerOne = new TestHivePeer(peerOne.Protocol, peerOne);
            var litePeerTwo = new TestHivePeer(peerTwo.Protocol, peerTwo);
            var litePeerThree = new TestHivePeer(peerThree.Protocol, peerThree);

            try
            {
                // peer 1: join
                var customJoinParams = new Dictionary<byte, object> { { (byte)ParameterKey.DeleteCacheOnLeave, true } };
                JoinPeer(peerOne, litePeerOne, 1, customJoinParams);

                // peer 2: join 
                JoinPeer(peerTwo, litePeerTwo, 2);

                // peer 1: receive join event of peer 2
                ReceiveJoinEvent(peerOne, 2);

                // peer 1: send to all others
                const byte code = 100;
                var data = new Hashtable { { 1, "value1" } };
                var request = GetRaiseEventRequest(code, data, (byte)CacheOperation.AddToRoomCache, null, null);
                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());

                // Peer 2: Receive data
                var @event = ReceiveEvent(peerTwo, code);
                Assert.AreEqual(code, @event.Code);
                var eventData = (Hashtable)@event.Parameters[(byte)ParameterKey.Data];
                Assert.AreEqual("value1", eventData[1]);

                // peer 3: join 
                request = GetJoinRequest();
                PeerHelper.InvokeOnOperationRequest(litePeerThree, request, new SendParameters());
                Assert.IsTrue(peerThree.WaitForNextResponse(WaitTimeout));

                var responseList = peerThree.GetResponseList();
                Assert.AreEqual(1, responseList.Count);
                var response = responseList[0];
                Assert.AreEqual(3, response.Parameters[(byte)ParameterKey.ActorNr]);

                // peer 3: receive own join event
                Assert.IsTrue(peerThree.WaitForNextEvent(WaitTimeout));
                var eventList = peerThree.GetEventList();
                if (eventList.Count < 2)
                {
                    Assert.IsTrue(peerThree.WaitForNextEvent(WaitTimeout));
                    eventList.AddRange(peerThree.GetEventList());
                }

                Assert.AreEqual(2, eventList.Count);
                @event = eventList[0];
                Assert.AreEqual(LiteOpCode.Join, @event.Code);
                Assert.AreEqual(3, @event.Parameters[(byte)ParameterKey.ActorNr]);

                @event = eventList[1];
                Assert.AreEqual(code, @event.Code);
                eventData = (Hashtable)@event.Parameters[(byte)ParameterKey.Data];
                Assert.AreEqual("value1", eventData[1]);

                // receive join event of peer 3
                ReceiveJoinEvent(peerOne, 3);
                ReceiveJoinEvent(peerTwo, 3);
            }
            finally
            {
                PeerHelper.SimulateDisconnect(litePeerOne);
                PeerHelper.SimulateDisconnect(litePeerTwo);
                PeerHelper.SimulateDisconnect(litePeerThree);
            }
        }

        /// <summary>
        ///   Tests if existing and new peer receive cached events.
        /// </summary>
        [Test]
        public void RaiseEventCacheRoomSlices()
        {
            var peerOne = new DummyPeer();
            var peerTwo = new DummyPeer();
            var peerThree = new DummyPeer();
            var peerFour = new DummyPeer();
            var litePeerOne = new TestHivePeer(peerOne.Protocol, peerOne);
            var litePeerTwo = new TestHivePeer(peerTwo.Protocol, peerTwo);
            var litePeerThree = new TestHivePeer(peerThree.Protocol, peerThree);
            var litePeerFour = new TestHivePeer(peerFour.Protocol, peerFour);
            RoomReference roomRef = null;
            try
            {
                roomRef = TestGameCache.Instance.GetRoomReference("testGame", null);
                var game = (TestGame)roomRef.Room;

                // peer 1: join
                var customJoinParams = new Dictionary<byte, object> { { (byte)ParameterKey.DeleteCacheOnLeave, true } };
                JoinPeer(peerOne, litePeerOne, 1, customJoinParams);

                Thread.Sleep(100);
                // peer 1: sends to all others - adding event to the room cache slice 0
                const byte code = 100;
                var request = GetRaiseEventRequest(code, "event in slice 0", (byte)CacheOperation.AddToRoomCache, null, null);
                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());
                game.WaitForRaisevent();

                // peer 1: increase slice index to 1
                request = GetRaiseEventRequest(null, (byte)CacheOperation.SliceIncreaseIndex);
                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());

                // peer 1: receive event CacheSliceChanged to slice 1
                var @event = ReceiveEvent(peerOne, (byte)EventCode.CacheSliceChanged);
                Assert.AreEqual(1, (int)@event.Parameters[(byte)ParameterKey.CacheSliceIndex]);

                // peer 1: sends to all others - adding event to the room cache slice 1
                request = GetRaiseEventRequest(code, "event in slice 1", (byte)CacheOperation.AddToRoomCache, null, null);
                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());
                game.WaitForRaisevent();

                // peer 2: joins 
                request = GetJoinRequest();
                PeerHelper.InvokeOnOperationRequest(litePeerTwo, request, new SendParameters());
                Assert.IsTrue(peerTwo.WaitForNextResponse(WaitTimeout));
                var responseList = peerTwo.GetResponseList();
                Assert.AreEqual(1, responseList.Count);
                Assert.AreEqual(2, responseList[0].Parameters[(byte)ParameterKey.ActorNr]);


                // peer 1: join event from actor 2 (NO CacheSliceChanged)
                ReceiveJoinEvent(peerOne, 2);

                // peer 2: receives 4 events:
                //// 1. its own join event
                var eventList = ReceiveEvents(peerTwo, 4);
                @event = eventList[0];
                Assert.AreEqual(LiteOpCode.Join, @event.Code);
                Assert.AreEqual(2, @event.Parameters[(byte)ParameterKey.ActorNr]);

                //// 2. custom event in slice 0
                Assert.AreEqual("event in slice 0", eventList[1].Parameters[(byte)ParameterKey.Data]);

                //// 3. event CacheSliceChanged to slice 1
                @event = eventList[2];
                Assert.AreEqual((byte)EventCode.CacheSliceChanged, @event.Code);
                Assert.AreEqual(1, (int)@event.Parameters[(byte)ParameterKey.CacheSliceIndex]);

                //// 4. custom event in slice 1
                Assert.AreEqual("event in slice 1", eventList[3].Parameters[(byte)ParameterKey.Data]);

                // peer 1: increase slice index to 2
                request = GetRaiseEventRequest(null, (byte)CacheOperation.SliceIncreaseIndex);
                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());

                // peer 1: receive event CacheSliceChanged to slice 2
                @event = ReceiveEvent(peerOne, (byte)EventCode.CacheSliceChanged);
                Assert.AreEqual(2, (int)@event.Parameters[(byte)ParameterKey.CacheSliceIndex]);

                // peer 2: receive event CacheSliceChanged to slice 2
                @event = ReceiveEvent(peerTwo, (byte)EventCode.CacheSliceChanged);
                Assert.AreEqual(2, (int)@event.Parameters[(byte)ParameterKey.CacheSliceIndex]);

                // peer 1: sends to all others - adding event to the room cache slice 2
                request = GetRaiseEventRequest(code, "event in slice 2", (byte)CacheOperation.AddToRoomCache, null, null);
                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());
                game.WaitForRaisevent();

                // peer 2: receive custom event in slice 2
                @event = ReceiveEvent(peerTwo, code);
                Assert.AreEqual("event in slice 2", @event.Parameters[(byte)ParameterKey.Data]);

                // peer 1: purge slice 0
                request = GetRaiseEventRequest(0, (byte)CacheOperation.SlicePurgeIndex);
                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());
                game.WaitForCacheOpEvent();

                // peer 3: join
                request = GetJoinRequest();
                PeerHelper.InvokeOnOperationRequest(litePeerThree, request, new SendParameters());
                Assert.IsTrue(peerThree.WaitForNextResponse(WaitTimeout));
                responseList = peerThree.GetResponseList();
                Assert.AreEqual(1, responseList.Count);
                Assert.AreEqual(3, responseList[0].Parameters[(byte)ParameterKey.ActorNr]);

                //// peer 3: receives only 5 events, the custom event in slice 0 was purged.
                //// join event
                eventList = ReceiveEvents(peerThree, 5);
                Assert.AreEqual(LiteOpCode.Join, eventList[0].Code);
                Assert.AreEqual(3, eventList[0].Parameters[(byte)ParameterKey.ActorNr]);

                //// CacheSliceEvent slice 1
                Assert.AreEqual((byte)EventCode.CacheSliceChanged, eventList[1].Code);
                Assert.AreEqual(1, (int)eventList[1].Parameters[(byte)ParameterKey.CacheSliceIndex]);

                //// custom event in slice 1
                Assert.AreEqual("event in slice 1", eventList[2].Parameters[(byte)ParameterKey.Data]);

                //// CacheSliceEvent slice 2
                Assert.AreEqual((byte)EventCode.CacheSliceChanged, eventList[3].Code);
                Assert.AreEqual(2, (int)eventList[3].Parameters[(byte)ParameterKey.CacheSliceIndex]);

                //// custom event in slice 2
                Assert.AreEqual("event in slice 2", eventList[4].Parameters[(byte)ParameterKey.Data]);

                // receive join event of peer 3
                ReceiveJoinEvent(peerOne, 3);
                ReceiveJoinEvent(peerTwo, 3);

                // peer 4: join request fails requesting purged slice
                request = GetJoinRequest(new Dictionary<byte, object> { { (byte)ParameterKey.CacheSliceIndex, 0 } });
                PeerHelper.InvokeOnOperationRequest(litePeerFour, request, new SendParameters());
                Assert.IsTrue(peerFour.WaitForNextResponse(WaitTimeout));
                responseList = peerFour.GetResponseList();
                Assert.AreEqual(1, responseList.Count);
                Assert.AreNotEqual(0, responseList[0].ReturnCode);

                PeerHelper.SimulateDisconnect(litePeerFour);
                Thread.Sleep(10);
                litePeerFour = new TestHivePeer(peerFour.Protocol, peerFour);
                // peer 4: join requesting slice 2 -> only events upwards that slice should be received
                request = GetJoinRequest(new Dictionary<byte, object> { { (byte)ParameterKey.CacheSliceIndex, 2 } });
                PeerHelper.InvokeOnOperationRequest(litePeerFour, request, new SendParameters());
                Assert.IsTrue(peerFour.WaitForNextResponse(WaitTimeout));
                responseList = peerFour.GetResponseList();
                Assert.AreEqual(1, responseList.Count);
                Assert.AreEqual(0, responseList[0].ReturnCode);
                Assert.AreEqual(4, responseList[0].Parameters[(byte)ParameterKey.ActorNr]);

                //// peer 3: receives only 3 events, requested slice 2 upwards.
                //// join event
                eventList = ReceiveEvents(peerFour, 3);
                Assert.AreEqual(LiteOpCode.Join, eventList[0].Code);
                Assert.AreEqual(4, eventList[0].Parameters[(byte)ParameterKey.ActorNr]);

                //// CacheSliceEvent slice 2
                Assert.AreEqual((byte)EventCode.CacheSliceChanged, eventList[1].Code);
                Assert.AreEqual(2, (int)eventList[1].Parameters[(byte)ParameterKey.CacheSliceIndex]);

                //// custom event in slice 2
                Assert.AreEqual("event in slice 2", eventList[2].Parameters[(byte)ParameterKey.Data]);

                // receive join event of peer 4
                ReceiveJoinEvent(peerOne, 4);
                ReceiveJoinEvent(peerTwo, 4);
                ReceiveJoinEvent(peerThree, 4);

                //// peer 1: purge slice 1
                //request = GetRaiseEventRequest(1, (byte)CacheOperation.SlicePurgeIndex);
                //PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());
                //game.WaitForCacheOpEvent();

            }
            finally
            {
                PeerHelper.SimulateDisconnect(litePeerOne);
                PeerHelper.SimulateDisconnect(litePeerTwo);
                PeerHelper.SimulateDisconnect(litePeerThree);
                PeerHelper.SimulateDisconnect(litePeerFour);
                roomRef?.Dispose();
            }
        }

        /// <summary>
        ///   Test for sending events to receiver groups.
        /// </summary>
        [Test]
        public void RaiseEventReceiverGroups()
        {
            var peerOne = new DummyPeer();
            var peerTwo = new DummyPeer();
            var peerThree = new DummyPeer();
            var litePeerOne = new TestHivePeer(peerOne.Protocol, peerOne);
            var litePeerTwo = new TestHivePeer(peerTwo.Protocol, peerTwo);
            var litePeerThree = new TestHivePeer(peerThree.Protocol, peerThree);

            try
            {
                // peer 1: join
                JoinPeer(peerOne, litePeerOne, 1);

                // peer 2: join 
                JoinPeer(peerTwo, litePeerTwo, 2);

                // peer 1: receive join event of peer 2
                ReceiveJoinEvent(peerOne, 2);

                // peer 2: join 
                JoinPeer(peerThree, litePeerThree, 3);

                // receive join event of peer 3
                ReceiveJoinEvent(peerOne, 3);

                // peer 2: receive join event of peer 3
                ReceiveJoinEvent(peerTwo, 3);

                // peer 1: send to all others
                byte code = 100;
                var data = new Hashtable { { 1, "value1" } };
                var request = GetRaiseEventRequest(code, data, null, null, null);
                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());

                // Peer 2 + 3: Receive data
                var @event = ReceiveEvent(peerTwo, code);
                Assert.AreEqual(code, @event.Code);
                var eventData = (Hashtable)@event.Parameters[(byte)ParameterKey.Data];
                Assert.AreEqual("value1", eventData[1]);

                @event = ReceiveEvent(peerThree, code);
                Assert.AreEqual(code, @event.Code);
                eventData = (Hashtable)@event.Parameters[(byte)ParameterKey.Data];
                Assert.AreEqual("value1", eventData[1]);

                // peer 1: send to all others again
                code++;
                request = GetRaiseEventRequest(code, data, null, ReceiverGroup.Others, null);
                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());

                // Peer 2 + 3: Receive data
                @event = ReceiveEvent(peerTwo, code);
                Assert.AreEqual(code, @event.Code);
                eventData = (Hashtable)@event.Parameters[(byte)ParameterKey.Data];
                Assert.AreEqual("value1", eventData[1]);

                @event = ReceiveEvent(peerThree, code);
                Assert.AreEqual(code, @event.Code);
                eventData = (Hashtable)@event.Parameters[(byte)ParameterKey.Data];
                Assert.AreEqual("value1", eventData[1]);

                // peer 1: send to all 
                code++;
                request = GetRaiseEventRequest(code, data, null, ReceiverGroup.All, null);
                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());

                // Peer 1 + 2 + 3: Receive data
                @event = ReceiveEvent(peerOne, code);
                Assert.AreEqual(code, @event.Code);
                eventData = (Hashtable)@event.Parameters[(byte)ParameterKey.Data];
                Assert.AreEqual("value1", eventData[1]);

                @event = ReceiveEvent(peerTwo, code);
                Assert.AreEqual(code, @event.Code);
                eventData = (Hashtable)@event.Parameters[(byte)ParameterKey.Data];
                Assert.AreEqual("value1", eventData[1]);

                @event = ReceiveEvent(peerThree, code);
                Assert.AreEqual(code, @event.Code);
                eventData = (Hashtable)@event.Parameters[(byte)ParameterKey.Data];
                Assert.AreEqual("value1", eventData[1]);

                // peer 1: send to master 
                code++;
                request = GetRaiseEventRequest(code, data, null, ReceiverGroup.MasterClient, null);
                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());

                // Peer 1: Receive data
                @event = ReceiveEvent(peerOne, code);
                Assert.AreEqual(code, @event.Code);
                eventData = (Hashtable)@event.Parameters[(byte)ParameterKey.Data];
                Assert.AreEqual("value1", eventData[1]);

                // peer 2: send to master 
                code++;
                request = GetRaiseEventRequest(code, data, null, ReceiverGroup.MasterClient, null);
                PeerHelper.InvokeOnOperationRequest(litePeerTwo, request, new SendParameters());

                // Peer 1: Receive data
                @event = ReceiveEvent(peerOne, code);
                Assert.AreEqual(code, @event.Code);
                eventData = (Hashtable)@event.Parameters[(byte)ParameterKey.Data];
                Assert.AreEqual("value1", eventData[1]);

                // peer 3: send to master 
                code++;
                request = GetRaiseEventRequest(code, data, null, ReceiverGroup.MasterClient, null);
                PeerHelper.InvokeOnOperationRequest(litePeerThree, request, new SendParameters());

                // Peer 1: Receive data
                @event = ReceiveEvent(peerOne, code);
                Assert.AreEqual(code, @event.Code);
                eventData = (Hashtable)@event.Parameters[(byte)ParameterKey.Data];
                Assert.AreEqual("value1", eventData[1]);

                // peer 1: leave
                request = GetLeaveRequest();
                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());

                // peer 2+3: receive leave event
                ReceiveEvent(peerTwo, (byte)EventCode.Leave);
                ReceiveEvent(peerThree, (byte)EventCode.Leave);

                // peer 3: send to master 
                code++;
                request = GetRaiseEventRequest(code, data, null, ReceiverGroup.MasterClient, null);
                PeerHelper.InvokeOnOperationRequest(litePeerThree, request, new SendParameters());

                // Peer 2: Receive data
                @event = ReceiveEvent(peerTwo, code);
                Assert.AreEqual(code, @event.Code);
                eventData = (Hashtable)@event.Parameters[(byte)ParameterKey.Data];
                Assert.AreEqual("value1", eventData[1]);
            }
            finally
            {
                PeerHelper.SimulateDisconnect(litePeerOne);
                PeerHelper.SimulateDisconnect(litePeerTwo);
                PeerHelper.SimulateDisconnect(litePeerThree);
            }
        }

        #endregion

        /// <summary>
        ///   Test for sending web rpc request
        /// </summary>
        [Test]
        public void HandleWebRpcOpTest()
        {
            var peerOne = new DummyPeer();
            var litePeerOne = new TestHivePeer(peerOne.Protocol, peerOne);
            litePeerOne.SetHttpRpcCallsLimit(10);

            var env = new Dictionary<string, object>
            {
                {"AppId", ApplicationBase.Instance.HwId},
                {"AppVersion", ""},
                {"Region", ""},
                {"Cloud", ""},
            };

            var mgr = new WebRpcManager(env);
            if (mgr.IsRpcEnabled)
            {
                litePeerOne.WebRpcHandler = mgr.GetWebRpcHandler();
            }

            HiveHttpTestListener listener = null;
            try
            {
                listener = new HiveHttpTestListener(new Uri("http://localhost:55557"));
                listener.Start();
                // peer 1: create game
                var parameters = new Dictionary<string, object>
                                     {
                                         { "p1", "v1" }, { "p2", "v2" }, { "p3", "v3" }
                                     };

                var request = new OperationRequest((byte)OperationCode.Rpc)
                {
                    Parameters = new Dictionary<byte, object>()
                };
                request.Parameters.Add((byte)ParameterKey.RpcCallParams, parameters);
                request.Parameters.Add((byte)ParameterKey.UriPath, "?key1=0&key2=xxx");

                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());
                Assert.IsTrue(peerOne.WaitForNextResponse(10000));

                var responseList = peerOne.GetResponseList();
                Assert.AreEqual(1, responseList.Count);

                var response = responseList[0];
                Assert.AreEqual(0, response.ReturnCode);
                Assert.AreEqual(123, response.Parameters[(byte)ParameterKey.RpcCallRetCode]);
                Assert.AreEqual("Hello World", response.Parameters[(byte)ParameterKey.RpcCallRetMessage]);
                var d = (Dictionary<string, object>)response.Parameters[(byte)ParameterKey.RpcCallParams];
                Assert.AreEqual(3, d.Count);
                Assert.AreEqual("value1", d["str1"]);
                Assert.AreEqual(2, d["str2"]);
                Assert.AreEqual(new byte[] { 1, 2, 3 }, d["str3"]);

                request.Parameters[(byte)ParameterKey.UriPath] = "nonExisting?key1=0&key2=xxx";
                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());
                Assert.IsTrue(peerOne.WaitForNextResponse(10000));

                responseList = peerOne.GetResponseList();
                Assert.AreEqual(1, responseList.Count);

                response = responseList[0];

                Assert.AreNotEqual(0, response.ReturnCode);
                Assert.IsFalse(string.IsNullOrEmpty(response.DebugMessage));
            }
            finally
            {
                listener?.Stop();
                PeerHelper.SimulateDisconnect(litePeerOne);
            }
        }


        /// <summary>
        /// Expected flow:
        ///  - #PeersCount peers join a game
        ///  - check all peers receive response and join events
        ///  - check that master client id is 1
        ///  - peer#0 disconnects
        ///  - wait for game to remove peer
        ///  - check that master client id is 2
        ///  - check other peers received disconnect event
        ///  - peer#0 rejoins
        ///  - check that master client id is 1 again
        ///  
        /// </summary>
        [Test]
        public void MasterClientIdTests()
        {
            const int peersCount = 2;
            const int playerTtl = 100;

            var nativePeer = new DummyPeer();
            var rejoiningPeer = new TestHivePeer(nativePeer.Protocol, nativePeer);
            RoomReference room = null;

            var nativePeers = new DummyPeer[peersCount];
            var litePeers = new TestHivePeer[peersCount];

            for (var i = 0; i < peersCount; ++i)
            {
                var peer = new DummyPeer();
                nativePeers[i] = peer;
                litePeers[i] = new TestHivePeer(peer.Protocol, peer);
            }

            try
            {
                OperationRequest request;
                for (var i = 0; i < peersCount; ++i)
                {
                    var peer = nativePeers[i];

                    request = GetJoinRequest();
                    request.Parameters[(byte)ParameterKey.PlayerTTL] = playerTtl;
                    PeerHelper.InvokeOnOperationRequest(litePeers[i], request, new SendParameters());
                    Assert.IsTrue(peer.WaitForNextResponse(WaitTimeout));
                }

                Thread.Sleep(50); // to get all events before check

                var leaveLitePeer = litePeers[0];

                litePeers[0] = null;
                nativePeers[0] = null;

                room = TestGameCache.Instance.GetRoomReference("testGame", leaveLitePeer);
                Assert.AreNotEqual(null, room);

                var game = (TestGame)room.Room;

                Assert.AreEqual(1, game.MasterClientId);

                PeerHelper.SimulateDisconnect(leaveLitePeer);

                game.WaitRemovePeerFromGame();

                Assert.AreEqual(2, game.MasterClientId);

                Assert.AreEqual(1, game.ActorsManager.InactiveActorsCount);
                Assert.AreEqual(peersCount - 1, game.GetActors().Count());

                var actorNr = game.GetDisconnectedActors().ToArray()[0].ActorNr;

                request = GetJoinRequest();
                request.Parameters[(byte)ParameterKey.ActorNr] = actorNr;
                request.Parameters[(byte)ParameterKey.JoinMode] = JoinModeConstants.RejoinOnly;
                PeerHelper.InvokeOnOperationRequest(rejoiningPeer, request, new SendParameters());

                Assert.IsTrue(nativePeer.WaitForNextResponse(WaitTimeout));

                Assert.AreEqual(0, game.ActorsManager.InactiveActorsCount);
                Assert.AreEqual(peersCount, game.GetActors().Count());
                Assert.IsNotNull(game.GetActors().Where(actor => actorNr == actor.ActorNr));

                Assert.AreEqual(2, game.MasterClientId);
            }
            finally
            {
                room?.Dispose();

                foreach (var p in litePeers)
                {
                    if (p != null)
                    {
                        PeerHelper.SimulateDisconnect(p);
                    }
                }

                if (rejoiningPeer.Connected)
                {
                    PeerHelper.SimulateDisconnect(rejoiningPeer);
                }
            }
        }

        [Test]
        public void MasterClientEventTests()
        {
            const int peersCount = 2;
            const int playerTtl = 100;

            var nativePeer = new DummyPeer();
            var rejoiningPeer = new TestHivePeer(nativePeer.Protocol, nativePeer);
            RoomReference room = null;

            var nativePeers = new DummyPeer[peersCount];
            var litePeers = new TestHivePeer[peersCount];

            for (var i = 0; i < peersCount; ++i)
            {
                var peer = new DummyPeer();
                nativePeers[i] = peer;
                litePeers[i] = new TestHivePeer(peer.Protocol, peer);
            }

            try
            {
                OperationRequest request;
                for (var i = 0; i < peersCount; ++i)
                {
                    var peer = nativePeers[i];

                    request = GetJoinRequest();
                    request.Parameters[(byte)ParameterKey.PlayerTTL] = playerTtl;
                    PeerHelper.InvokeOnOperationRequest(litePeers[i], request, new SendParameters());
                    Assert.IsTrue(peer.WaitForNextResponse(WaitTimeout));
                }

                Thread.Sleep(50); // to get all events before check

                CheckMasterClientEvent(litePeers[1], nativePeers[0]);

                room = TestGameCache.Instance.GetRoomReference("testGame", litePeers[0]);
                Assert.IsNotNull(room);

                var game = (TestGame)room.Room;

                Assert.AreEqual(1, game.MasterClientId);

                // master client leaving
                var leaveLitePeer = litePeers[0];

                PeerHelper.SimulateDisconnect(leaveLitePeer);

                game.WaitRemovePeerFromGame();

                Thread.Sleep(50); // to get all events before check
                Assert.AreEqual(2, game.MasterClientId);
                CheckMasterClientEvent(litePeers[1], nativePeers[1]);

                // rejoining
                request = GetJoinRequest();
                request.Parameters[(byte)ParameterKey.ActorNr] = 1;
                request.Parameters[(byte)ParameterKey.JoinMode] = JoinModeConstants.RejoinOnly;
                PeerHelper.InvokeOnOperationRequest(rejoiningPeer, request, new SendParameters());

                Assert.IsTrue(nativePeer.WaitForNextResponse(WaitTimeout));

                Thread.Sleep(50); // to get all events before check
                Assert.AreEqual(2, game.MasterClientId);
                CheckMasterClientEvent(rejoiningPeer, nativePeers[1]);
            }
            finally
            {
                room?.Dispose();

                foreach (var p in litePeers)
                {
                    if (p.Connected)
                    {
                        PeerHelper.SimulateDisconnect(p);
                    }
                }

                if (rejoiningPeer.Connected)
                {
                    PeerHelper.SimulateDisconnect(rejoiningPeer);
                }
            }
        }

        private static void CheckMasterClientEvent(TestHivePeer sender, DummyPeer receiver)
        {
            receiver.WaitForNextEvent(0);// to reset event state
            receiver.GetEventList();// cleaning events list
            var raiseEventRequest = GetRaiseEventRequest(0, "test", null, ReceiverGroup.MasterClient, null);
            PeerHelper.InvokeOnOperationRequest(sender, raiseEventRequest, new SendParameters());


            Assert.IsTrue(receiver.WaitForNextEvent(WaitTimeout));

            var eventList = receiver.GetEventList();

            Assert.AreEqual(1, eventList.Count);
            var eventData = eventList[0];
            Assert.AreEqual(0, eventData.Code);
            Assert.AreEqual("test", eventData.Parameters[(byte)ParameterKey.Data]);
        }

        #region Properties tests
        [Test]
        public void SetPropertiesCASTest()
        {
            var peerOne = new DummyPeer();
            var peerTwo = new DummyPeer();
            var litePeerOne = new TestHivePeer(peerTwo.Protocol, peerOne, "myuser1");
            var litePeerTwo = new TestHivePeer(peerTwo.Protocol, peerTwo, "myuser2");

            const string gamePropertyValue = "Value";
            const string gamePropertyValue2 = "Value2";
            const string gamePropertyValue3 = "Value3";
            const string gamePropertyKey = "Key";
            try
            {
                // peer 1: create game
                var request = GetJoinGameRequest();
                request[(byte)ParameterKey.PlayerTTL] = -1;
                request[(byte)ParameterKey.CheckUserOnJoin] = true;
                request[(byte)ParameterKey.CreateIfNotExists] = true;
                request[(byte)ParameterKey.EmptyRoomLiveTime] = 999;
                request[(byte)ParameterKey.GameId] = "SetPropertiesCASTest";
                request[(byte)ParameterKey.ActorProperties] = new Hashtable { { "TestKey", "TestVal1" } };
                request[(byte)ParameterKey.GameProperties] = new Hashtable { { gamePropertyKey, gamePropertyValue } };

                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());
                Assert.IsTrue(peerOne.WaitForNextResponse(WaitTimeout));

                var responseList = peerOne.GetResponseList();
                Assert.AreEqual(1, responseList.Count);
                var response = responseList[0];
                Assert.AreEqual(0, response.ReturnCode, response.DebugMessage);
                Assert.IsFalse(response.Parameters.ContainsKey((byte)ParameterKey.ActorProperties), "We don't expect any actor properties");

                request = GetJoinGameRequest();
                request[(byte)ParameterKey.ActorProperties] = new Hashtable { { "TestKey", "TestVal2" } };
                request[(byte)ParameterKey.GameId] = "SetPropertiesCASTest";
                PeerHelper.InvokeOnOperationRequest(litePeerTwo, request, new SendParameters());
                Assert.IsTrue(peerTwo.WaitForNextResponse(WaitTimeout));
                responseList = peerTwo.GetResponseList();
                Assert.AreEqual(1, responseList.Count);

                Assert.IsTrue(peerOne.WaitForNextEvent(WaitTimeout));
                Assert.IsTrue(peerTwo.WaitForNextEvent(WaitTimeout));

                // in order to get rid of Join events in list
                peerOne.GetEventList();
                peerTwo.GetEventList();


                var properties = new Hashtable { { gamePropertyKey, gamePropertyValue2 } };
                var properties2 = new Hashtable { { gamePropertyKey, gamePropertyValue3 } };
                var expected = new Hashtable { { gamePropertyKey, gamePropertyValue } };

                // set properties through first player
                request = GetSetGamePropertiesRequest(properties, expected);
                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());

                Assert.IsTrue(peerOne.WaitForNextResponse(WaitTimeout));
                responseList = peerOne.GetResponseList();
                Assert.AreEqual(1, responseList.Count);
                response = responseList[0];

                Assert.AreEqual((byte)OperationCode.SetProperties, response.OperationCode);
                Assert.AreEqual(0, response.ReturnCode, response.DebugMessage);

                Assert.IsTrue(peerTwo.WaitForNextEvent(WaitTimeout));

                var events = peerTwo.GetEventList();
                var singleEvent = events.Count == 1 ? events[0] : events[1];

                // check that response contains values we set
                Assert.AreEqual(LiteEventCode.PropertiesChanged, singleEvent.Code);
                var propertyValues = (Hashtable)singleEvent[(byte)ParameterKey.Properties];
                Assert.NotNull(propertyValues);
                Assert.AreEqual(gamePropertyValue2, propertyValues[gamePropertyKey]);

                // set properties through second player. Expected values are old
                request = GetSetGamePropertiesRequest(properties2, expected);
                PeerHelper.InvokeOnOperationRequest(litePeerTwo, request, new SendParameters());

                // check response for second peer
                Assert.IsTrue(peerTwo.WaitForNextResponse(WaitTimeout));

                responseList = peerTwo.GetResponseList();
                Assert.AreEqual(1, responseList.Count);
                response = responseList[0];
                Assert.AreEqual((byte)OperationCode.SetProperties, response.OperationCode);
                Assert.AreEqual((short)ErrorCode.OperationInvalid, response.ReturnCode);
            }
            finally
            {
                if (litePeerOne.Connected)
                {
                    PeerHelper.SimulateDisconnect(litePeerOne);
                }
                if (litePeerTwo.Connected)
                {
                    PeerHelper.SimulateDisconnect(litePeerTwo);
                }
            }
        }

        [Test]
        public void SetPropertiesCasExpectedKeyDoNotExistTest()
        {
            var peerOne = new DummyPeer();
            var litePeerOne = new TestHivePeer(peerOne.Protocol, peerOne, "myuser1");

            const string gamePropertyValue = "Value";
            const string gamePropertyValue2 = "Value2";
            const string gamePropertyKey = "Key";
            try
            {
                // peer 1: create game
                var request = GetJoinGameRequest();
                request[(byte)ParameterKey.PlayerTTL] = -1;
                request[(byte)ParameterKey.CheckUserOnJoin] = true;
                request[(byte)ParameterKey.CreateIfNotExists] = true;
                request[(byte)ParameterKey.EmptyRoomLiveTime] = 999;
                request[(byte)ParameterKey.GameId] = "SetPropertiesCASExpectedKeyDoNotExistTest";
                request[(byte)ParameterKey.ActorProperties] = new Hashtable { { "TestKey", "TestVal1" } };
                request[(byte)ParameterKey.GameProperties] = new Hashtable { { gamePropertyKey, gamePropertyValue } };

                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());
                Assert.IsTrue(peerOne.WaitForNextResponse(WaitTimeout));

                var responseList = peerOne.GetResponseList();
                Assert.AreEqual(1, responseList.Count);
                var response = responseList[0];
                Assert.AreEqual(0, response.ReturnCode, response.DebugMessage);
                Assert.IsFalse(response.Parameters.ContainsKey((byte)ParameterKey.ActorProperties), "We don't expect any actor properties");

                Assert.IsTrue(peerOne.WaitForNextEvent(WaitTimeout));

                // in order to get rid of Join events in list
                peerOne.GetEventList();

                var properties = new Hashtable { { gamePropertyKey, gamePropertyValue2 } };
                var expected = new Hashtable { { gamePropertyKey + "DoNotExits", gamePropertyValue } };

                // set properties through first player
                request = GetSetGamePropertiesRequest(properties, expected);
                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());

                Assert.IsTrue(peerOne.WaitForNextResponse(WaitTimeout));
                responseList = peerOne.GetResponseList();
                Assert.AreEqual(1, responseList.Count);
                response = responseList[0];

                Assert.AreEqual((byte)OperationCode.SetProperties, response.OperationCode);
                Assert.AreEqual((short)ErrorCode.OperationInvalid, response.ReturnCode, response.DebugMessage);
            }
            finally
            {
                if (litePeerOne.Connected)
                {
                    PeerHelper.SimulateDisconnect(litePeerOne);
                }
            }
        }

        [Test]
        public void SetProperties_WrongPropertyType()
        {
            var peerOne = new DummyPeer();
            var peerTwo = new DummyPeer();
            var litePeerOne = new TestHivePeer(peerOne.Protocol, peerOne, "User1");
            var litePeerTwo = new TestHivePeer(peerTwo.Protocol, peerTwo, "User2");

            try
            {
                // peer 1: join is expected to succeed, even if user ids match
                var request = GetJoinRequest();
                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());

                Assert.IsTrue(peerOne.WaitForNextResponse(WaitTimeout));
                var resp = peerOne.GetResponseList();
                Assert.AreEqual(0, resp[0].ReturnCode, resp[0].DebugMessage);

                var gameProperties = new Hashtable
                                         {
                                             { (byte)GameParameter.IsOpen, "xxx" },
                                         };

                request = GetSetGamePropertiesRequest(gameProperties);
                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());
                Assert.IsTrue(peerOne.WaitForNextResponse(WaitTimeout));
                resp = peerOne.GetResponseList();
                Assert.AreEqual((short)ErrorCode.OperationInvalid, resp[0].ReturnCode, resp[0].DebugMessage);
            }
            finally
            {
                if (litePeerOne.Connected)
                {
                    PeerHelper.SimulateDisconnect(litePeerOne);
                }
                if (litePeerTwo.Connected)
                {
                    PeerHelper.SimulateDisconnect(litePeerTwo);
                }
            }
        }

        #endregion

        #region Plugin tests
        [Test]
        public void PluginTestCreateJoinRaiseEventLeaveCloseGame()
        {
            PluginTestRoomTestBody();
        }

        [Test]
        public void PluginTestCreateJoinRaiseEventDisconnectCloseGame()
        {
            PluginTestRoomTestBody(1000);
        }

        [Test]
        public void PluginTestCreateJoinRaiseEventDisconnectLeaveCloseGame()
        {
            PluginTestRoomTestBody(1000);
        }

        private static void PluginTestRoomTestBody(int playerTtl = 0)
        {
            var peerOne = new DummyPeer();
            var peerTwo = new DummyPeer();
            var litePeerOne = new TestHivePeer(peerOne.Protocol, peerOne);
            var litePeerTwo = new TestHivePeer(peerTwo.Protocol, peerTwo);

            try
            {
                // peer 1: create game
                var request = GetCreateGameRequest();
                request.Parameters.Add((byte)ParameterKey.PlayerTTL, playerTtl);

                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());
                Assert.IsTrue(peerOne.WaitForNextResponse(WaitTimeout));

                var roomRef = TestGameCache.Instance.GetRoomReference("testGame", litePeerOne);
                var plugin = ((TestGame)roomRef.Room).GetPlugin();
                roomRef.Dispose();

                Assert.IsTrue(plugin.WaitForOnCreateEvent(1000));

                //// peer 2: join game 
                request = GetJoinGameRequest();
                PeerHelper.InvokeOnOperationRequest(litePeerTwo, request, new SendParameters());
                Assert.IsTrue(peerTwo.WaitForNextResponse(WaitTimeout));

                Assert.IsTrue(plugin.WaitForOnBeforeJoinEvent(1000));
                Assert.IsTrue(plugin.WaitForOnJoinEvent(1000));

                var data = new Hashtable { { 1, "value1" } };
                var raiseEventRequest = GetRaiseEventRequest(100, data, null, null, new[] { 1, 2, 3 });
                PeerHelper.InvokeOnOperationRequest(litePeerOne, raiseEventRequest, new SendParameters());

                Assert.IsTrue(plugin.WaitForOnRaiseEvent(1000));

                var roomProperties = new Hashtable { { "TestKey", "TestData" } };
                var setPropertiesRequest = new OperationRequest { OperationCode = (byte)OperationCode.SetProperties, Parameters = new Dictionary<byte, object>() };
                setPropertiesRequest.Parameters.Add((byte)ParameterKey.Properties, roomProperties);

                PeerHelper.InvokeOnOperationRequest(litePeerOne, setPropertiesRequest, new SendParameters());

                Assert.IsTrue(plugin.WaitForOnBeforeSetPropertiesEvent(1000));
                Assert.IsTrue(plugin.WaitForOnSetPropertiesEvent(1000));

                PeerHelper.SimulateDisconnect(litePeerOne);

                if (playerTtl == 0)
                {
                    Assert.IsTrue(plugin.WaitForOnLeaveEvent(1000));
                    Assert.IsFalse(plugin.WaitForOnDisconnectEvent(1000));
                }
                else if (playerTtl > 0)
                {
                    Assert.IsTrue(plugin.WaitForOnDisconnectEvent(1000));
                    Assert.IsTrue(plugin.WaitForOnLeaveEvent(playerTtl + 1000));
                }
                else
                {
                    Assert.IsFalse(plugin.WaitForOnLeaveEvent(1000));
                    Assert.IsTrue(plugin.WaitForOnDisconnectEvent(1000));
                }

                PeerHelper.SimulateDisconnect(litePeerTwo);

                Assert.IsTrue(plugin.WaitForOnBeforeCloseEvent(1000));
                Assert.IsTrue(plugin.WaitForOnCloseEvent(1000));
            }
            finally
            {
                if (litePeerOne.Connected)
                {
                    PeerHelper.SimulateDisconnect(litePeerOne);
                }

                if (litePeerTwo.Connected)
                {
                    PeerHelper.SimulateDisconnect(litePeerTwo);
                }
            }
        }

        [Test]
        public void PluginOnCreateForgotCallTest()
        {
            var peerOne = new DummyPeer();
            var litePeerOne = new TestHivePeer(peerOne.Protocol, peerOne);
            const string gameName = "OnCreateForgotCall";
            try
            {
                // peer 1: create game
                var request = GetCreateGameRequest();
                request.Parameters[(byte)ParameterKey.GameId] = gameName;

                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());
                // we should  get error response, because we forgot to call Continue
                Assert.IsTrue(peerOne.WaitForNextResponse(WaitTimeout));

                var response = peerOne.GetResponseList()[0];
                Assert.AreEqual((short)ErrorCode.PluginReportedError, response.ReturnCode);

                var plugin = ((TestGame)litePeerOne.LastCreatedRoom).GetPlugin();

                Assert.IsTrue(plugin.WaitForOnCreateEvent(1000));
                Assert.IsTrue(plugin.WaitForReportError(1000));
            }
            finally
            {
                if (litePeerOne.Connected)
                {
                    PeerHelper.SimulateDisconnect(litePeerOne);
                }
            }
        }

        [Test]
        public void PluginOnBeforeCloseForgotCallTest()
        {
            var peerOne = new DummyPeer();
            var litePeerOne = new TestHivePeer(peerOne.Protocol, peerOne);
            const string gameName = "OnBeforeCloseForgotCall";
            try
            {
                // peer 1: create game
                var request = GetCreateGameRequest();
                request.Parameters[(byte)ParameterKey.GameId] = gameName;

                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());
                // we should not get any response, because we forgot to call Continue
                Assert.IsTrue(peerOne.WaitForNextResponse(WaitTimeout));

                var roomRef = TestGameCache.Instance.GetRoomReference(gameName, litePeerOne);
                var plugin = ((TestGame)roomRef.Room).GetPlugin();
                roomRef.Dispose();

                Assert.IsTrue(plugin.WaitForOnCreateEvent(1000));

                PeerHelper.SimulateDisconnect(litePeerOne);

                Assert.IsTrue(plugin.WaitForOnBeforeCloseEvent(1000));
                Assert.IsTrue(plugin.WaitForReportError(1000));
            }
            finally
            {
                if (litePeerOne.Connected)
                {
                    PeerHelper.SimulateDisconnect(litePeerOne);
                }
            }
        }

        [Test]
        public void PluginOnCloseForgotCallTest()
        {
            var peerOne = new DummyPeer();
            var litePeerOne = new TestHivePeer(peerOne.Protocol, peerOne);
            const string gameName = "OnCloseForgotCall";
            try
            {
                // peer 1: create game
                var request = GetCreateGameRequest();
                request.Parameters[(byte)ParameterKey.GameId] = gameName;

                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());
                // we should not get any response, because we forgot to call Continue
                Assert.IsTrue(peerOne.WaitForNextResponse(WaitTimeout));

                var roomRef = TestGameCache.Instance.GetRoomReference(gameName, litePeerOne);
                var plugin = ((TestGame)roomRef.Room).GetPlugin();
                roomRef.Dispose();

                Assert.IsTrue(plugin.WaitForOnCreateEvent(1000));

                PeerHelper.SimulateDisconnect(litePeerOne);

                Assert.IsTrue(plugin.WaitForOnCloseEvent(1000));
                Assert.IsTrue(plugin.WaitForReportError(1000));
            }
            finally
            {
                if (litePeerOne.Connected)
                {
                    PeerHelper.SimulateDisconnect(litePeerOne);
                }
            }
        }

        #endregion

        #region Game close tests

        [Test]
        public void BeforeCloseGameExceptionInContinueTest()
        {
            this.CloseGameExceptionTestBody("BeforeCloseContinueException");
        }

        [Test]
        public void BeforeCloseGameExceptionTest()
        {
            this.CloseGameExceptionTestBody("BeforeCloseGameException");
        }

        [Test]
        public void OnCloseGameExceptionInContinueTest()
        {
            this.CloseGameExceptionTestBody("OnCloseContinueException");
        }

        [Test]
        public void OnCloseGameExceptionTest()
        {
            this.CloseGameExceptionTestBody("OnCloseGameException");
        }

        [Test]
        public void BeforeAndOnCloseGameExceptionInContinueTest()
        {
            this.CloseGameExceptionTestBody("CloseFatal");
        }

        [Test]
        public void BeforeAndOnCloseGameExceptionTest()
        {
            this.CloseGameExceptionTestBody("CloseFatalPlugin");
        }

        public void CloseGameExceptionTestBody(string propertyValue)
        {
            var peerOne = new DummyPeer();
            var litePeerOne = new TestHivePeer(peerOne.Protocol, peerOne, "User1");

            RoomReference room = null;

            try
            {
                var request = GetJoinRequest();
                request.Parameters[(byte)ParameterKey.GameProperties] = new Hashtable
                {
                    {"key", propertyValue},
                };

                PeerHelper.InvokeOnOperationRequest(litePeerOne, request, new SendParameters());
                Assert.IsTrue(peerOne.WaitForNextResponse(WaitTimeout));
                var resp = peerOne.GetResponseList();
                Assert.AreEqual(0, resp[0].ReturnCode, resp[0].DebugMessage);

                room = TestGameCache.Instance.GetRoomReference("testGame", null);
                var game = (TestGame)room.Room;

                PeerHelper.SimulateDisconnect(litePeerOne);

                Thread.Sleep(10);
                room.Dispose();

                Assert.IsTrue(game.WaitForDispose());

            }
            finally
            {
                room?.Dispose();

                if (litePeerOne.Connected)
                {
                    PeerHelper.SimulateDisconnect(litePeerOne);
                }
            }
        }

        #endregion

        #region Ban tests
        [Test]
        public void BanLeavingPlayerTest()
        {
            const string player1 = "BanUser";
            const string player2 = "User2";

            var peerOne = new DummyPeer();
            var peerTwo = new DummyPeer();
            var peerThree = new DummyPeer();
            var peer4 = new DummyPeer();
            var litePeerOne = new TestHivePeer(peerOne.Protocol, peerOne, player1);
            var litePeerThree = new TestHivePeer(peerThree.Protocol, peerThree, player1);
            var litePeer4 = new TestHivePeer(peer4.Protocol, peer4, player1);
            var litePeerTwo = new TestHivePeer(peerTwo.Protocol, peerTwo, player2);

            RoomReference room = null;

            try
            {
                // peer 1: joins / creates the room
                var parameters = new Dictionary<byte, object>
                {
                    [(byte) ParameterKey.PlayerTTL] = -1, 
                    [(byte) ParameterKey.CheckUserOnJoin] = true
                };

                JoinPeer(peerOne, litePeerOne, 1, parameters);
                JoinPeer(peerTwo, litePeerTwo, 2, parameters);

                room = TestGameCache.Instance.GetRoomReference("testGame", litePeerOne);

                var game = (TestGame)room.Room;

                PeerHelper.SimulateDisconnect(litePeerOne);
                game.WaitRemovePeerFromGame();

                IPluginHost gameSink = game;

                parameters.Clear();
                parameters[(byte)ParameterKey.UserId] = player1;


                var request = GetJoinRequest(parameters);
                PeerHelper.InvokeOnOperationRequest(litePeer4, request, new SendParameters());
                Assert.IsTrue(peer4.WaitForNextResponse(WaitTimeout));

                var responseList = peer4.GetResponseList();
                Assert.AreEqual(1, responseList.Count);
                var response = responseList[0];
                Assert.AreEqual((short)ErrorCode.JoinFailedFoundExcludedUserId, response.ReturnCode);
                Assert.IsTrue(response.DebugMessage.Contains(" is banned in this game"));

                PeerHelper.SimulateDisconnect(litePeerTwo);

                game.WaitRemovePeerFromGame();

                var gameState = gameSink.GetSerializableGameState();

                Assert.AreEqual(player1, gameState.ExcludedActors[0].UserId);
                Assert.AreEqual(RemoveActorReason.Banned, gameState.ExcludedActors[0].Reason);


                room.Dispose(); // dispose last room reference. Room will be destroyed after this line
                game.WaitForDispose();

                Thread.Sleep(1000);

                // get new room with same name
                room = TestGameCache.Instance.GetRoomReference("testGame", litePeerOne);
                game = (TestGame)room.Room;

                gameSink = game;
                game.CreatePlugin();

                gameSink.SetGameState(gameState);

                // checking restored game state
                // we check that excluded list restored
                Assert.AreEqual(player1, game.ActorsManager.ExcludedActors[0].UserId);
                Assert.AreEqual(RemoveActorReason.Banned, game.ActorsManager.ExcludedActors[0].Reason);

                request = GetJoinRequest(parameters);
                PeerHelper.InvokeOnOperationRequest(litePeerThree, request, new SendParameters());
                Assert.IsTrue(peerThree.WaitForNextResponse(WaitTimeout));

                responseList = peerThree.GetResponseList();
                Assert.AreEqual(1, responseList.Count);
                response = responseList[0];
                Assert.AreEqual((short)ErrorCode.JoinFailedFoundExcludedUserId, response.ReturnCode);
                Assert.IsTrue(response.DebugMessage.Contains(" is banned in this game"));
            }
            finally
            {
                room?.Dispose();
                if (litePeerOne.Connected)
                {
                    PeerHelper.SimulateDisconnect(litePeerOne);
                }
                if (litePeerTwo.Connected)
                {
                    PeerHelper.SimulateDisconnect(litePeerTwo);
                }
            }
        }
        #endregion
        #endregion

        #region Methods

        protected static OperationRequest GetCreateGameRequest()
        {
            return GetCreateGameRequest(null);
        }

        protected static OperationRequest GetCreateGameRequest(Dictionary<byte, object> customParameter)
        {
            var request = new OperationRequest
            {
                OperationCode = (byte)OperationCode.CreateGame,
                Parameters = new Dictionary<byte, object>()
            };
            request.Parameters.Add((byte)ParameterKey.GameId, "testGame");

            if (customParameter != null)
            {
                foreach (var keyValue in customParameter)
                {
                    request.Parameters.Add(keyValue.Key, keyValue.Value);
                }
            }

            return request;
        }

        protected static OperationRequest GetJoinGameRequest()
        {
            return GetJoinGameRequest(null);
        }

        protected static OperationRequest GetJoinGameRequest(Dictionary<byte, object> customParameter)
        {
            var request = new OperationRequest
            {
                OperationCode = (byte)OperationCode.JoinGame,
                Parameters = new Dictionary<byte, object>()
            };
            request.Parameters.Add((byte)ParameterKey.GameId, "testGame");

            if (customParameter != null)
            {
                foreach (var keyValue in customParameter)
                {
                    request.Parameters.Add(keyValue.Key, keyValue.Value);
                }
            }

            return request;
        }

        /// <summary>
        ///   The get join request.
        /// </summary>
        /// <returns>
        ///   a join request
        /// </returns>
        protected static OperationRequest GetJoinRequest()
        {
            return GetJoinRequest(null);
        }

        /// <summary>
        ///   The get join request.
        /// </summary>
        /// <returns>
        ///   a join request
        /// </returns>
        protected static OperationRequest GetJoinRequest(Dictionary<byte, object> customParameter)
        {
            if (customParameter == null)
            {
                customParameter = new Dictionary<byte, object>();
            }

            if (!customParameter.ContainsKey((byte)ParameterKey.JoinMode))
            {
                customParameter.Add((byte)ParameterKey.JoinMode, JoinModeConstants.CreateIfNotExists);
            }

            return GetJoinGameRequest(customParameter);
        }

        /// <summary>
        ///   The get leave request.
        /// </summary>
        /// <returns>
        ///   a leave request
        /// </returns>
        protected static OperationRequest GetLeaveRequest()
        {
            var request = new OperationRequest { OperationCode = LiteOpCode.Leave };
            return request;
        }

        /// <summary>
        ///   Creates a RaiseEvent request.
        /// </summary>
        /// <param name = "eventCode">
        ///   The event code.
        /// </param>
        /// <param name = "data">
        ///   The data.
        /// </param>
        /// <param name = "cache">
        ///   The cache.
        /// </param>
        /// <param name = "receiverGroup">
        ///   The receiver group.
        /// </param>
        /// <param name = "targetActors">
        ///   The target actors.
        /// </param>
        /// <param name="group"></param>
        /// <returns>
        ///   An <see cref = "OperationRequest" />.
        /// </returns>
        protected static OperationRequest GetRaiseEventRequest(byte eventCode, object data, byte? cache, ReceiverGroup? receiverGroup, int[] targetActors, byte? group = null)
        {
            var @params = new Dictionary<byte, object> { { (byte)ParameterKey.Code, eventCode } };
            if (data != null)
            {
                @params.Add((byte)ParameterKey.Data, data);
            }

            if (cache.HasValue)
            {
                @params.Add((byte)ParameterKey.Cache, cache.Value);
            }

            if (receiverGroup.HasValue)
            {
                @params.Add((byte)ParameterKey.ReceiverGroup, (byte)receiverGroup.Value);
            }

            if (targetActors != null)
            {
                @params.Add((byte)ParameterKey.Actors, targetActors);
            }

            if (group != null)
            {
                @params.Add((Byte)ParameterKey.Group, group);
            }

            return new OperationRequest { OperationCode = LiteOpCode.RaiseEvent, Parameters = @params };
        }

        protected static OperationRequest GetRaiseEventRequest(int? cacheIndex, byte? cache)
        {
            var @params = new Dictionary<byte, object>
            {
                {(byte) ParameterKey.Cache, cache},
                {(byte) ParameterKey.CacheSliceIndex, cacheIndex},
            };
            return new OperationRequest { OperationCode = LiteOpCode.RaiseEvent, Parameters = @params };
        }

        protected static OperationRequest GetChangeGroups(byte[] groupsToAdd, byte[] groupsToRemove)
        {
            var @params = new Dictionary<byte, object>
            {
                {(byte) ParameterKey.GroupsForAdd, groupsToAdd},
                {(byte) ParameterKey.GroupsForRemove, groupsToRemove}
            };
            return new OperationRequest { OperationCode = LiteOpCode.ChangeGroups, Parameters = @params };
        }

        protected static OperationRequest GetSetGamePropertiesRequest(Hashtable properties, Hashtable expectedValues = null)
        {
            var @params = new Dictionary<byte, object>
            {
                { (byte)ParameterKey.Properties, properties},
                { (byte)ParameterKey.ExpectedValues, expectedValues}
            };
            return new OperationRequest { OperationCode = LiteOpCode.SetProperties, Parameters = @params };
        }

        /// <summary>
        ///   Sends a join operation and verifies the response.
        /// </summary>
        /// <param name = "peer">
        ///   The peer.
        /// </param>
        /// <param name = "litePeer">
        ///   The lite peer.
        /// </param>
        /// <param name = "expectedNumber">
        ///   The expected number.
        /// </param>
        /// <param name="customParameters">
        ///   The custom Parameters.
        /// </param>
        private static void JoinPeer(DummyPeer peer, HivePeer litePeer, int expectedNumber, Dictionary<byte, object> customParameters = null)
        {
            var request = GetJoinRequest(customParameters);
            PeerHelper.InvokeOnOperationRequest(litePeer, request, new SendParameters());
            Assert.IsTrue(peer.WaitForNextResponse(WaitTimeout));

            var responseList = peer.GetResponseList();
            Assert.AreEqual(1, responseList.Count);
            var response = responseList[0];
            Assert.AreEqual(0, response.ReturnCode);
            Assert.AreEqual(expectedNumber, response.Parameters[(byte)ParameterKey.ActorNr]);

            // peer 1: receive own join event
            Assert.IsTrue(peer.WaitForNextEvent(WaitTimeout));
            var eventList = peer.GetEventList();
            Assert.AreEqual(1, eventList.Count);
            var eventData = eventList[0];
            Assert.AreEqual(LiteOpCode.Join, eventData.Code);
            Assert.AreEqual(expectedNumber, eventData.Parameters[(byte)ParameterKey.ActorNr]);
        }

        /// <summary>
        ///   Waiting for an event with the given event code.
        /// </summary>
        /// <param name = "peer">
        ///   The peer.
        /// </param>
        /// <param name = "expectedCode">
        ///   The expected code.
        /// </param>
        /// <returns>
        ///   The received <see cref = "EventData" />.
        /// </returns>
        private static EventData ReceiveEvent(DummyPeer peer, byte expectedCode)
        {
            Assert.IsTrue(peer.WaitForNextEvent(WaitTimeout), "Timed out waiting for event code {0}.", expectedCode);
            var eventList = peer.GetEventList();
            Assert.AreEqual(1, eventList.Count, "received unexpected number of events:");
            var eventData = eventList[0];
            Assert.AreEqual(expectedCode, eventData.Code);
            return eventData;
        }

        /// <summary>
        ///   Receive a certain amount of events.
        /// </summary>
        /// <param name = "peer">
        ///   The peer.
        /// </param>
        /// <param name = "count">
        ///   The count.
        /// </param>
        /// <returns>
        ///   An array of <see cref = "EventData" />.
        /// </returns>
        private static List<EventData> ReceiveEvents(DummyPeer peer, int count)
        {
            Assert.IsTrue(peer.WaitForNextEvent(WaitTimeout));
            var eventList = peer.GetEventList();
            while (eventList.Count < count)
            {
                Assert.IsTrue(peer.WaitForNextEvent(WaitTimeout), $"Timed out waiting for event count {count}, received only {eventList.Count}");
                eventList.AddRange(peer.GetEventList());
            }

            Assert.AreEqual(count, eventList.Count, "received unexpected number of events:");
            return eventList;
        }

        /// <summary>
        ///   Waiting for a join event of a certain actor.
        /// </summary>
        /// <param name = "peerOne">
        ///   The peer one.
        /// </param>
        /// <param name = "expectedNumber">
        ///   The expected actor number.
        /// </param>
        private static void ReceiveJoinEvent(DummyPeer peerOne, int expectedNumber)
        {
            var eventData = ReceiveEvent(peerOne, LiteOpCode.Join);
            Assert.AreEqual(expectedNumber, eventData.Parameters[(byte)ParameterKey.ActorNr]);
        }

        #endregion
    }
}