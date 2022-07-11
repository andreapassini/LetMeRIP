using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using ExitGames.Client.Photon;
using Photon.Realtime;
using NUnit.Framework;
using Photon.Hive.Operations;
using Photon.LoadBalancing.UnifiedClient;
using ErrorCode = Photon.Realtime.ErrorCode;
using EventCode = Photon.Realtime.EventCode;
using Hashtable = System.Collections.Hashtable;
using OperationCode = Photon.Realtime.OperationCode;
using ParameterCode = Photon.Realtime.ParameterCode;

namespace Photon.LoadBalancing.UnitTests.UnifiedTests
{
    public abstract partial class LBApiTestsImpl
    {
        #region EventCache tests

        [Test]
        public void EventCache_AddGlobalEventRemoveUsingEventIdOnly()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);

                // create room 
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                var createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                };

                var createGameResponse = client1.CreateGame(createGameRequest);

                this.ConnectAndAuthenticate(client1, createGameResponse.Address);

                // creation on GS
                client1.CreateGame(createGameRequest);

                // add cached message
                client1.SendRequest(new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Code, (byte) 1},
                        {ParameterCode.Cache, (byte)EventCaching.AddToRoomCacheGlobal},
                    }
                });

                // add cached message
                client1.SendRequest(new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Code, (byte) 2},
                        {ParameterCode.Cache, (byte)EventCaching.AddToRoomCacheGlobal},
                    }
                });

                // remove cached message
                client1.SendRequest(new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Code, (byte) 1},
                        {ParameterCode.Cache, (byte)EventCaching.RemoveFromRoomCache},
                    }
                });

                // second player joins and check that there is no removed event
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                this.ConnectClientToGame(client2, roomName);

                client2.WaitForEvent(EventCode.Join);

                client2.CheckThereIsNoEvent(1);
                client2.CheckThereIsEvent(2);
            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        // test for bug reported by customers
        [Test]
        public void EventCache_AddGlobalEventRemoveUsing0ActorId()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);

                // create room 
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                var createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                };

                var createGameResponse = client1.CreateGame(createGameRequest);

                this.ConnectAndAuthenticate(client1, createGameResponse.Address);

                // creation on GS
                client1.CreateGame(createGameRequest);
                var data = new Hashtable() { { (byte)7, 1 } };
                // add cached message
                client1.SendRequest(new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Code, (byte) 1},
                        {ParameterCode.Data, data},
                        {ParameterCode.Cache, (byte)EventCaching.AddToRoomCacheGlobal},
                    }
                });

                // remove cached message
                client1.SendRequest(new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Code, (byte) 1},
                        {ParameterCode.ActorList, new int[] {0} },
                        {ParameterCode.Data, data },
                        {ParameterCode.Cache, (byte)EventCaching.RemoveFromRoomCache},
                    }
                });

                // second player joins and check that there is no removed event
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                this.ConnectClientToGame(client2, roomName);

                client2.WaitForEvent(EventCode.Join);

                client2.CheckThereIsNoEvent(1);
            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [Test]
        public void EventCache_LimitExceeded()
        {
            if (this.IsOnline)
            {
                Assert.Ignore("Does not support online mode");
            }

            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("Does not support empty users Id");
            }

            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;
            UnifiedTestClient client3 = null;
            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);

                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client3 = this.CreateMasterClientAndAuthenticate(this.Player3);

                var cr = new CreateGameRequest()
                {
                    GameId = roomName,
                    PlayerTTL = -1,
                    SuppressRoomEvents = true,
                    CheckUserOnJoin = true
                };
                var createGameResponse = client1.CreateGame(cr);

                // switch client 1 to GS 
                this.ConnectAndAuthenticate(client1, createGameResponse.Address);

                client1.CreateGame(cr);

                client3.JoinGame(roomName);
                this.ConnectAndAuthenticate(client3, createGameResponse.Address);
                client3.JoinGame(roomName);

                // we do this trick here because we have to have token with valid GS and Game id
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                client2.JoinGame(roomName);

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

                this.ConnectAndAuthenticate(client2, createGameResponse.Address);
                client2.JoinGame(roomName, ErrorCode.GameClosed);

                this.ConnectAndAuthenticate(client2, this.MasterAddress);
                client2.JoinGame(roomName, ErrorCode.GameClosed);

                client3.LeaveGame(true);
                client3.Disconnect();

                this.ConnectAndAuthenticate(client3, createGameResponse.Address);
                client3.JoinGame(new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModes.RejoinOnly
                }, 32739);//EventCacheExceeded

            }
            finally
            {
                DisposeClients(client1, client2, client3);
            }
        }

        [Test]
        public void EventCache_SliceLimitExceeded()
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

                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                var createGameResponse = client1.CreateGame(roomName, true, true, 4);

                // switch client 1 to GS 
                this.ConnectAndAuthenticate(client1, createGameResponse.Address);

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


                this.UpdateTokensGSAndGame(client2, "localhost", roomName);

                this.ConnectAndAuthenticate(client2, createGameResponse.Address);
                client2.JoinGame(roomName);
            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [Test]
        public void EventCache_CleanForActorsWhoLeft()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;
            UnifiedTestClient client3 = null;
            UnifiedTestClient client4 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);

                // create room 
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                var createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                };

                var createGameResponse = client1.CreateGame(createGameRequest);

                this.ConnectAndAuthenticate(client1, createGameResponse.Address);

                // creation on GS
                client1.CreateGame(createGameRequest);


                // second player joins and check that there is no removed event
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                client3 = this.CreateMasterClientAndAuthenticate(this.Player3);

                this.ConnectClientToGame(client2, roomName);
                this.ConnectClientToGame(client3, roomName);

                AddEventCacheData(client1, client2, client3, null);

                client2.Disconnect();
                client3.Disconnect();

                Thread.Sleep(100);

                // remove cached message
                client1.SendRequest(new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Code, (byte) 1},
                        {ParameterCode.Cache, (byte)EventCaching.RemoveFromRoomCacheForActorsLeft},
                    }
                });


                client4 = this.CreateMasterClientAndAuthenticate("Player4");
                this.ConnectClientToGame(client4, roomName);

                client4.WaitForEvent(EventCode.Join);
                client4.CheckThereIsEvent(1);
                client4.CheckThereIsEvent(2);

                client4.CheckThereIsNoEvent(3);
                client4.CheckThereIsNoEvent(4);

           }
            finally
            {
                DisposeClients(client1, client2, client3, client4);
            }
        }

        [Test]
        public void EventCache_RemoveAllCachedEvents()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);

                // create room 
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                var createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                };

                var createGameResponse = client1.CreateGame(createGameRequest);

                this.ConnectAndAuthenticate(client1, createGameResponse.Address);

                // creation on GS
                client1.CreateGame(createGameRequest);
                var data = new Hashtable() { { (byte)7, 1 } };
                // add cached message
                client1.SendRequest(new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Code, (byte) 1},
                        {ParameterCode.Data, data},
                        {ParameterCode.Cache, (byte)EventCaching.AddToRoomCacheGlobal},
                    }
                });

                client1.SendRequest(new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Code, (byte) 2},
                        {ParameterCode.Data, data},
                        {ParameterCode.Cache, (byte)EventCaching.AddToRoomCacheGlobal},
                    }
                });

                // remove cached message
                client1.SendRequest(new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Cache, (byte)EventCaching.RemoveFromRoomCache},
                    }
                });

                // second player joins and check that there is no removed event
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                this.ConnectClientToGame(client2, roomName);

                client2.WaitForEvent(EventCode.Join);

                client2.CheckThereIsNoEvent(1);
            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [Test]
        public void EventCache_RemoveUsingData()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;
            UnifiedTestClient client3 = null;
            UnifiedTestClient client4 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);

                // create room 
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                var createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                };

                var createGameResponse = client1.CreateGame(createGameRequest);

                this.ConnectAndAuthenticate(client1, createGameResponse.Address);

                // creation on GS
                client1.CreateGame(createGameRequest);


                // second player joins and check that there is no removed event
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                client3 = this.CreateMasterClientAndAuthenticate(this.Player3);

                this.ConnectClientToGame(client2, roomName);
                this.ConnectClientToGame(client3, roomName);

                var eventsData = new Hashtable{{1, 1}};
                AddEventCacheData(client1, client2, client3, eventsData);

                client2.Disconnect();
                client3.Disconnect();

                // remove cached message
                client1.SendRequest(new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Data, eventsData},
                        {ParameterCode.Cache, (byte)EventCaching.RemoveFromRoomCache},
                    }
                });


                client4 = this.CreateMasterClientAndAuthenticate("Player4");
                this.ConnectClientToGame(client4, roomName);

                client4.WaitForEvent(EventCode.Join);
                client4.CheckThereIsNoEvent(1);
                client4.CheckThereIsEvent(2);

                client4.CheckThereIsNoEvent(3);
                client4.CheckThereIsNoEvent(4);

           }
            finally
            {
                DisposeClients(client1, client2, client3, client4);
            }
        }

        [Test]
        public void EventCache_RemoveUsingActorId()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;
            UnifiedTestClient client3 = null;
            UnifiedTestClient client4 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);

                // create room 
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                var createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                };

                var createGameResponse = client1.CreateGame(createGameRequest);

                this.ConnectAndAuthenticate(client1, createGameResponse.Address);

                // creation on GS
                client1.CreateGame(createGameRequest);


                // second player joins and check that there is no removed event
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                client3 = this.CreateMasterClientAndAuthenticate(this.Player3);

                this.ConnectClientToGame(client2, roomName);
                this.ConnectClientToGame(client3, roomName);

                var eventsData = new Hashtable{{1, 1}};
                AddEventCacheData(client1, client2, client3, eventsData);

                client2.Disconnect();
                client3.Disconnect();

                // remove cached message
                client1.SendRequest(new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.ActorList, new int[] {3}},
                        {ParameterCode.Cache, (byte)EventCaching.RemoveFromRoomCache},
                    }
                });


                client4 = this.CreateMasterClientAndAuthenticate("Player4");
                this.ConnectClientToGame(client4, roomName);

                client4.WaitForEvent(EventCode.Join);
                client4.CheckThereIsEvent(1);
                client4.CheckThereIsEvent(2);

                client4.CheckThereIsEvent(3);
                client4.CheckThereIsNoEvent(4);

           }
            finally
            {
                DisposeClients(client1, client2, client3, client4);
            }
        }

        [Test]
        public void EventCache_RemoveUsingEventCode()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;
            UnifiedTestClient client3 = null;
            UnifiedTestClient client4 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);

                // create room 
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                var createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                };

                var createGameResponse = client1.CreateGame(createGameRequest);

                this.ConnectAndAuthenticate(client1, createGameResponse.Address);

                // creation on GS
                client1.CreateGame(createGameRequest);


                // second player joins and check that there is no removed event
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                client3 = this.CreateMasterClientAndAuthenticate(this.Player3);

                this.ConnectClientToGame(client2, roomName);
                this.ConnectClientToGame(client3, roomName);

                var eventsData = new Hashtable{{1, 1}};
                AddEventCacheData(client1, client2, client3, eventsData);

                client2.Disconnect();
                client3.Disconnect();

                // remove cached message
                client1.SendRequest(new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Cache, (byte)EventCaching.RemoveFromRoomCache},
                        {ParameterCode.Code, (byte) 4},
                    }
                });


                client4 = this.CreateMasterClientAndAuthenticate("Player4");
                this.ConnectClientToGame(client4, roomName);

                client4.WaitForEvent(EventCode.Join);
                client4.CheckThereIsEvent(1);
                client4.CheckThereIsEvent(2);

                client4.CheckThereIsEvent(3);
                client4.CheckThereIsNoEvent(4);

           }
            finally
            {
                DisposeClients(client1, client2, client3, client4);
            }
        }

        [Test]
        public void EventCache_RemoveUsingDataAndActorId()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;
            UnifiedTestClient client3 = null;
            UnifiedTestClient client4 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);

                // create room 
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                var createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                };

                var createGameResponse = client1.CreateGame(createGameRequest);

                this.ConnectAndAuthenticate(client1, createGameResponse.Address);

                // creation on GS
                client1.CreateGame(createGameRequest);


                // second player joins and check that there is no removed event
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                client3 = this.CreateMasterClientAndAuthenticate(this.Player3);

                this.ConnectClientToGame(client2, roomName);
                this.ConnectClientToGame(client3, roomName);

                var eventsData = new Hashtable{{1, 1}};
                AddEventCacheData(client1, client2, client3, eventsData);

                client2.Disconnect();
                client3.Disconnect();

                // remove cached message
                client1.SendRequest(new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.ActorList, new int[] {2}},
                        {ParameterCode.Data, eventsData},
                        {ParameterCode.Cache, (byte)EventCaching.RemoveFromRoomCache},
                    }
                });


                client4 = this.CreateMasterClientAndAuthenticate("Player4");
                this.ConnectClientToGame(client4, roomName);

                client4.WaitForEvent(EventCode.Join);
                client4.CheckThereIsEvent(1);
                client4.CheckThereIsEvent(2);

                client4.CheckThereIsNoEvent(3);
                client4.CheckThereIsEvent(4);

           }
            finally
            {
                DisposeClients(client1, client2, client3, client4);
            }
        }

        [Test]
        public void EventCache_RemoveUsingDataAndEventCode()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;
            UnifiedTestClient client3 = null;
            UnifiedTestClient client4 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);

                // create room 
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                var createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                };

                var createGameResponse = client1.CreateGame(createGameRequest);

                this.ConnectAndAuthenticate(client1, createGameResponse.Address);

                // creation on GS
                client1.CreateGame(createGameRequest);


                // second player joins and check that there is no removed event
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                client3 = this.CreateMasterClientAndAuthenticate(this.Player3);

                this.ConnectClientToGame(client2, roomName);
                this.ConnectClientToGame(client3, roomName);

                var eventsData = new Hashtable{{1, 1}};
                AddEventCacheData(client1, client2, client3, eventsData);

                client2.Disconnect();
                client3.Disconnect();

                // remove cached message
                client1.SendRequest(new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Data, eventsData},
                        {ParameterCode.Cache, (byte)EventCaching.RemoveFromRoomCache},
                        {ParameterCode.Code, (byte) 3},
                    }
                });


                client4 = this.CreateMasterClientAndAuthenticate("Player4");
                this.ConnectClientToGame(client4, roomName);

                client4.WaitForEvent(EventCode.Join);
                client4.CheckThereIsEvent(1);
                client4.CheckThereIsEvent(2);

                client4.CheckThereIsNoEvent(3);
                client4.CheckThereIsEvent(4);
           }
            finally
            {
                DisposeClients(client1, client2, client3, client4);
            }
        }

        [Test]
        public void EventCache_RemoveUsingDataActorIdAndEventCode()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;
            UnifiedTestClient client3 = null;
            UnifiedTestClient client4 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);

                // create room 
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                var createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                };

                var createGameResponse = client1.CreateGame(createGameRequest);

                this.ConnectAndAuthenticate(client1, createGameResponse.Address);

                // creation on GS
                client1.CreateGame(createGameRequest);


                // second player joins and check that there is no removed event
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                client3 = this.CreateMasterClientAndAuthenticate(this.Player3);

                this.ConnectClientToGame(client2, roomName);
                this.ConnectClientToGame(client3, roomName);

                var eventsData = new Hashtable{{1, 1}};
                AddEventCacheData(client1, client2, client3, eventsData);

                client2.Disconnect();
                client3.Disconnect();

                // remove cached message
                client1.SendRequest(new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.ActorList, new int[] {3}},
                        {ParameterCode.Data, eventsData},
                        {ParameterCode.Cache, (byte)EventCaching.RemoveFromRoomCache},
                        {ParameterCode.Code, (byte) 4},
                    }
                });


                client4 = this.CreateMasterClientAndAuthenticate("Player4");
                this.ConnectClientToGame(client4, roomName);

                client4.WaitForEvent(EventCode.Join);
                client4.CheckThereIsEvent(1);
                client4.CheckThereIsEvent(2);

                client4.CheckThereIsEvent(3);
                client4.CheckThereIsNoEvent(4);

           }
            finally
            {
                DisposeClients(client1, client2, client3, client4);
            }
        }

        [Test]
        public void EventCache_RemoveUsingActorIdAndEventCode()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;
            UnifiedTestClient client3 = null;
            UnifiedTestClient client4 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);

                // create room 
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                var createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                };

                var createGameResponse = client1.CreateGame(createGameRequest);

                this.ConnectAndAuthenticate(client1, createGameResponse.Address);

                // creation on GS
                client1.CreateGame(createGameRequest);


                // second player joins and check that there is no removed event
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                client3 = this.CreateMasterClientAndAuthenticate(this.Player3);

                this.ConnectClientToGame(client2, roomName);
                this.ConnectClientToGame(client3, roomName);

                var eventsData = new Hashtable{{1, 1}};
                AddEventCacheData(client1, client2, client3, eventsData);

                client2.Disconnect();
                client3.Disconnect();

                // remove cached message
                client1.SendRequest(new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.ActorList, new int[] {3}},
                        {ParameterCode.Cache, (byte)EventCaching.RemoveFromRoomCache},
                        {ParameterCode.Code, (byte) 4},
                    }
                });


                client4 = this.CreateMasterClientAndAuthenticate("Player4");
                this.ConnectClientToGame(client4, roomName);

                client4.WaitForEvent(EventCode.Join);
                client4.CheckThereIsEvent(1);
                client4.CheckThereIsEvent(2);

                client4.CheckThereIsEvent(3);
                client4.CheckThereIsNoEvent(4);

           }
            finally
            {
                DisposeClients(client1, client2, client3, client4);
            }
        }

        [Test]
        public void EventCache_ActorCacheLimitExceeded()
        {
            if (this.IsOnline)
            {
                Assert.Ignore("Does not support online mode");
            }

            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;
            UnifiedTestClient client3 = null;
            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);

                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                client3 = this.CreateMasterClientAndAuthenticate(this.Player3);

                var createGameResponse = client1.CreateGame(roomName, true, true, 4);

                // switch client 1 to GS 
                this.ConnectAndAuthenticate(client1, createGameResponse.Address);

                client1.CreateGame(new CreateGameRequest()
                {
                    GameId = roomName,
                    PlayerTTL = -1,
                    SuppressRoomEvents = true,
                    CheckUserOnJoin = true
                });

                client3.JoinGame(roomName);
                this.ConnectAndAuthenticate(client3, createGameResponse.Address);

                client3.JoinGame(roomName);

                // exceeding limits
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

                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                client2.JoinGame(roomName, ErrorCode.GameClosed);

                this.UpdateTokensGSAndGame(client2, "localhost", roomName);
                this.ConnectAndAuthenticate(client2, createGameResponse.Address);
                client2.JoinGame(roomName, ErrorCode.GameClosed);

                client3.LeaveGame(true);
                client3.Disconnect();

                this.ConnectAndAuthenticate(client3, createGameResponse.Address);
                client3.JoinGame(new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = JoinModes.RejoinOnly
                }, 32739);//EventCacheExceeded

            }
            finally
            {
                DisposeClients(client1, client2, client3);
            }
        }

        [Test]
        public void EventCache_ActorMergeData()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;
            UnifiedTestClient client3 = null;
            UnifiedTestClient client4 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);

                // create room 
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                var createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                };

                var createGameResponse = client1.CreateGame(createGameRequest);

                this.ConnectAndAuthenticate(client1, createGameResponse.Address);

                // creation on GS
                client1.CreateGame(createGameRequest);


                // second player joins and check that there is no removed event
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                client3 = this.CreateMasterClientAndAuthenticate(this.Player3);

                this.ConnectClientToGame(client2, roomName);
                this.ConnectClientToGame(client3, roomName);

                var eventsData = new Hashtable{{1, 1}};
                var eventsData2 = new Hashtable
                {
                    {1, 2},
                    {2, 2}
                };
                AddActorEventCacheData(client1, client2, client3, eventsData);

                client2.Disconnect();
                client3.Disconnect();

                // update cached message
                client1.SendRequest(new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Data, eventsData2},
                        {ParameterCode.Cache, (byte)EventCaching.MergeCache},
                        {ParameterCode.Code, (byte)1},
                    }
                });


                client4 = this.CreateMasterClientAndAuthenticate("Player4");
                this.ConnectClientToGame(client4, roomName);

                client4.WaitForEvent(EventCode.Join);

                EventData ev;
                Assert.That(client4.TryWaitEvent(1, this.WaitTimeout, out ev), Is.True);

                Assert.That(ev[(byte)ParameterKey.Data], Is.EqualTo(eventsData2));
                client4.CheckThereIsEvent(2);
                client4.CheckThereIsEvent(3);
           }
            finally
            {
                DisposeClients(client1, client2, client3, client4);
            }
        }

        [Test]
        public void EventCache_ActorReplaceData()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;
            UnifiedTestClient client3 = null;
            UnifiedTestClient client4 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);

                // create room 
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                var createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                };

                var createGameResponse = client1.CreateGame(createGameRequest);

                this.ConnectAndAuthenticate(client1, createGameResponse.Address);

                // creation on GS
                client1.CreateGame(createGameRequest);


                // second player joins and check that there is no removed event
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                client3 = this.CreateMasterClientAndAuthenticate(this.Player3);

                this.ConnectClientToGame(client2, roomName);
                this.ConnectClientToGame(client3, roomName);

                var eventsData = new Hashtable{{1, 1}};
                var eventsData2 = new Hashtable
                {
                    {2, 2}
                };
                AddActorEventCacheData(client1, client2, client3, eventsData);

                client2.Disconnect();
                client3.Disconnect();

                // update cached message
                client1.SendRequest(new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Data, eventsData2},
                        {ParameterCode.Cache, (byte)EventCaching.ReplaceCache},
                        {ParameterCode.Code, (byte)1},
                    }
                });


                client4 = this.CreateMasterClientAndAuthenticate("Player4");
                this.ConnectClientToGame(client4, roomName);

                client4.WaitForEvent(EventCode.Join);

                EventData ev;
                Assert.That(client4.TryWaitEvent(1, this.WaitTimeout, out ev), Is.True);

                Assert.That(ev[(byte)ParameterKey.Data], Is.EqualTo(eventsData2));
                client4.CheckThereIsEvent(2);
                client4.CheckThereIsEvent(3);
           }
            finally
            {
                DisposeClients(client1, client2, client3, client4);
            }
        }

        [TestCase("UseMerge")]
        [TestCase("UseRemove")]
        public void EventCache_ActorRemoveEvent(string testCase)
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;
            UnifiedTestClient client3 = null;
            UnifiedTestClient client4 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);

                // create room 
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                var createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                };

                var createGameResponse = client1.CreateGame(createGameRequest);

                this.ConnectAndAuthenticate(client1, createGameResponse.Address);

                // creation on GS
                client1.CreateGame(createGameRequest);


                // second player joins and check that there is no removed event
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                client3 = this.CreateMasterClientAndAuthenticate(this.Player3);

                this.ConnectClientToGame(client2, roomName);
                this.ConnectClientToGame(client3, roomName);

                var eventsData = new Hashtable{{1, 1}};
                var eventsData2 = new Hashtable
                {
                    {1, 2},
                    {2, 2}
                };
                AddActorEventCacheData(client1, client2, client3, eventsData);

                client2.Disconnect();
                client3.Disconnect();

                // remove cached message
                if (testCase == "UseMerge")
                {
                    client1.SendRequest(new OperationRequest
                    {
                        OperationCode = OperationCode.RaiseEvent,
                        Parameters = new Dictionary<byte, object>
                        {
                            {ParameterCode.Data, null},
                            {ParameterCode.Cache, (byte)EventCaching.MergeCache},
                            {ParameterCode.Code, (byte)1},
                        }
                    });
                }
                else
                {
                    client1.SendRequest(new OperationRequest
                    {
                        OperationCode = OperationCode.RaiseEvent,
                        Parameters = new Dictionary<byte, object>
                        {
                            {ParameterCode.Cache, (byte)EventCaching.RemoveCache},
                            {ParameterCode.Code, (byte)1},
                        }
                    });
                }

                client4 = this.CreateMasterClientAndAuthenticate("Player4");
                this.ConnectClientToGame(client4, roomName);

                client4.WaitForEvent(EventCode.Join);

                client4.CheckThereIsNoEvent(1);
                client4.CheckThereIsEvent(2);
                client4.CheckThereIsEvent(3);
           }
            finally
            {
                DisposeClients(client1, client2, client3, client4);
            }
        }

        #endregion

        #region Helpers
        private static void AddEventCacheData(UnifiedTestClient client1, UnifiedTestClient client2, UnifiedTestClient client3, Hashtable data)
        {
            // add cached message
            client1.SendRequest(new OperationRequest
            {
                OperationCode = OperationCode.RaiseEvent,
                Parameters = new Dictionary<byte, object>
                {
                    {ParameterCode.Code, (byte) 1},
                    {ParameterCode.Cache, (byte)EventCaching.AddToRoomCacheGlobal},
                    {ParameterCode.Data, data},
                }
            });

            // add cached message
            client1.SendRequest(new OperationRequest
            {
                OperationCode = OperationCode.RaiseEvent,
                Parameters = new Dictionary<byte, object>
                {
                    {ParameterCode.Code, (byte) 2},
                    {ParameterCode.Cache, (byte)EventCaching.AddToRoomCache},
                    {ParameterCode.Data, null},
                }
            });

            // add cached message
            client2.SendRequest(new OperationRequest
            {
                OperationCode = OperationCode.RaiseEvent,
                Parameters = new Dictionary<byte, object>
                {
                    {ParameterCode.Code, (byte) 3},
                    {ParameterCode.Cache, (byte)EventCaching.AddToRoomCache},
                    {ParameterCode.Data, data},
                }
            });

            // add cached message
            client3.SendRequest(new OperationRequest
            {
                OperationCode = OperationCode.RaiseEvent,
                Parameters = new Dictionary<byte, object>
                {
                    {ParameterCode.Code, (byte) 4},
                    {ParameterCode.Cache, (byte)EventCaching.AddToRoomCache},
                    {ParameterCode.Data, data},
                }
            });

            Thread.Sleep(200);
        }

        private void AddActorEventCacheData(UnifiedTestClient client1, UnifiedTestClient client2, UnifiedTestClient client3, Hashtable data)
        {
            // add cached message
            client1.SendRequest(new OperationRequest
            {
                OperationCode = OperationCode.RaiseEvent,
                Parameters = new Dictionary<byte, object>
                {
                    {ParameterCode.Code, (byte) 1},
                    {ParameterCode.Cache, (byte)EventCaching.MergeCache},
                    {ParameterCode.Data, data},
                }
            });

            // add cached message
            client2.SendRequest(new OperationRequest
            {
                OperationCode = OperationCode.RaiseEvent,
                Parameters = new Dictionary<byte, object>
                {
                    {ParameterCode.Code, (byte) 2},
                    {ParameterCode.Cache, (byte)EventCaching.MergeCache},
                    {ParameterCode.Data, data},
                }
            });

            // add cached message
            client3.SendRequest(new OperationRequest
            {
                OperationCode = OperationCode.RaiseEvent,
                Parameters = new Dictionary<byte, object>
                {
                    {ParameterCode.Code, (byte) 3},
                    {ParameterCode.Cache, (byte)EventCaching.MergeCache},
                    {ParameterCode.Data, data},
                }
            });

            Thread.Sleep(200);
        }
        #endregion
    }
}
