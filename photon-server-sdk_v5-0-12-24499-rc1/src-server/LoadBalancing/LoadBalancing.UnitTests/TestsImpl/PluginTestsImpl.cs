using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using ExitGames.Client.Photon;
using Photon.Realtime;
using NUnit.Framework;
using Photon.Hive.Common.Lobby;
using Photon.LoadBalancing.UnifiedClient;
using Photon.LoadBalancing.UnitTests.UnifiedServer;
using Photon.UnitTest.Utils.Basic;
using ErrorCode = Photon.Common.ErrorCode;
using EventCode = Photon.Realtime.EventCode;
using Hashtable = System.Collections.Hashtable;
using OperationCode = Photon.Realtime.OperationCode;

namespace Photon.LoadBalancing.UnitTests
{
    public abstract class PluginTestsImpl : LoadBalancingUnifiedTestsBase
    {
        protected PluginTestsImpl(ConnectPolicy policy) : base(policy)
        {
        }

        [Test]
        public void PluginsBasicsTest()
        {
            UnifiedTestClient client = null;
            UnifiedTestClient client2 = null;
            var GameName = "TestGame";

            try
            {

                client = this.CreateMasterClientAndAuthenticate("User1");

                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.EmptyRoomTTL, 0},
                        {ParameterCode.PlayerTTL, 0},
                        {ParameterCode.CheckUserOnJoin, false},
                        {ParameterCode.CleanupCacheOnLeave, false},
                        {ParameterCode.SuppressRoomEvents, false},
                        {ParameterCode.LobbyName, "Default"},
                        {ParameterCode.LobbyType, (byte)0},
                        {ParameterCode.GameProperties, new Hashtable
                        {
                            {"GameProperty", "GamePropertyValue"},
                            {"GameProperty2", "GamePropertyValue2"},
                            {GamePropertyKey.IsVisible, false},
                            {GamePropertyKey.IsOpen, false},
                            {GamePropertyKey.MaxPlayers, 10},
                            {GamePropertyKey.PropsListedInLobby, new string[]{"LobbyProperty", "LobbyPropertyValue"}}
                        }
                        },
                        {ParameterCode.PlayerProperties, new Hashtable {{"Actor1Property1", "Actor1Property1Value"}}},
                        {ParameterCode.Plugins, new string[]{"BasicTestsPlugin"}},
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);

                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);

                response = client.SendRequestAndWaitForResponse(request);

                Assert.AreEqual("BasicTestsPlugin", response[201]);
                Assert.AreEqual("1.0", response[200]);

                client.CheckThereIsNoErrorInfoEvent();

                client.SendRequestAndWaitForResponse(new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Properties, new Hashtable {{GamePropertyKey.IsOpen, true}}}
                    }
                });

                client.CheckThereIsNoErrorInfoEvent();

                request = new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.PlayerProperties, new Hashtable {{"Actor2Property", "Actor2PropertyValue"}}},
                        {ParameterCode.GameProperties, new Hashtable()},
                        {ParameterCode.UserId, "User2"},
                        {ParameterCode.LobbyName, "Default"},
                    },
                };

                client2 = this.CreateMasterClientAndAuthenticate("User2");
                response = client2.SendRequestAndWaitForResponse(request);

                this.ConnectAndAuthenticate(client2, (string)response[ParameterCode.Address], client2.UserId);
                client2.SendRequestAndWaitForResponse(request);

                client.CheckThereIsNoErrorInfoEvent();
                client2.CheckThereIsNoErrorInfoEvent();

                request = new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.ActorNr, 1},
                        {
                            ParameterCode.Properties, new Hashtable
                            {
                                {"Actor1Property2", "Actor1Property2Value"}
                            }
                        },
                    }
                };

                client.SendRequestAndWaitForResponse(request);

                client.CheckThereIsNoErrorInfoEvent();

                request = new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Code, (byte)1},
                    }
                };

                client.SendRequest(request);

                client.CheckThereIsNoErrorInfoEvent();

                request = new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Code, (byte)2},
                        {ParameterCode.Cache, (byte)EventCaching.AddToRoomCache},
                    }
                };

                client.SendRequest(request);

                client.CheckThereIsNoErrorInfoEvent();

                request = new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Code, (byte)3},
                        {ParameterCode.Cache, (byte)EventCaching.AddToRoomCache},
                    }
                };

                client.SendRequest(request);

                client.CheckThereIsNoErrorInfoEvent();

                request = new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Code, (byte)4},
                        {ParameterCode.Cache, (byte)EventCaching.AddToRoomCache},
                    }
                };

                client.SendRequest(request);

                client.CheckThereIsNoErrorInfoEvent();

                request = new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Code, (byte)5},
                        {ParameterCode.Cache, (byte)EventCaching.AddToRoomCache},
                        {ParameterCode.ActorList, new[]{0, 1}},
                        {ParameterCode.Data, new[]{0, 1}},
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.ReceiverGroup, 123},
                        {ParameterCode.Group, 123},
                        {ParameterCode.EventForward, true},
                        {ParameterCode.CacheSliceIndex, 123},
                    }
                };

                client.SendRequest(request);

                client.CheckThereIsNoErrorInfoEvent();

                client2.SendRequestAndWaitForResponse(new OperationRequest
                {
                    OperationCode = OperationCode.Leave,
                    Parameters = new Dictionary<byte, object> { { ParameterCode.IsInactive, true } }
                });
                client.CheckThereIsNoErrorInfoEvent();
                client2.CheckThereIsNoErrorInfoEvent();

                client2.Disconnect();

                client.CheckThereIsNoErrorInfoEvent();

                client.Disconnect();
                // it looks like for netframework nunit became more smart and detects somehow exceptions
                // generated in plugins although they are caugth and handled
                // Use Assert.Pass to fix that

                Assert.Pass();
            }
            finally
            {
                if (client != null && client.Connected)
                {
                    client.Disconnect();
                    client.Dispose();
                }

                if (client2 != null && client2.Connected)
                {
                    client2.Disconnect();
                    client2.Dispose();
                }
            }
        }

        #region OnCreate Tests

        [Test]
        public void OnCreatePreConditionFail()
        {
            UnifiedTestClient client = null;
            var GameName = MethodBase.GetCurrentMethod().Name;

            try
            {
                client = this.CreateMasterClientAndAuthenticate("User1");

                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.Plugins, new string[]{"BasicTestsPlugin"}},
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);

                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);

                response = client.SendRequestAndWaitForResponse(request, (short)ErrorCode.PluginReportedError);

                Assert.That(response.DebugMessage, Is.Not.Null.Or.Empty);

                // it looks like for netframework nunit became more smart and detects somehow exceptions
                // generated in plugins although they are caugth and handled
                // Use Assert.Pass to fix that
                Assert.Pass();
            }
            finally
            {
                if (client != null && client.Connected)
                {
                    client.Disconnect();
                    client.Dispose();
                }
            }
        }

        [Test]
        public void OnCreatePostConditionFail()
        {
            UnifiedTestClient client = null;
            var GameName = "TestGame";

            try
            {
                client = this.CreateMasterClientAndAuthenticate("User1");

                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.EmptyRoomTTL, 0},
                        {ParameterCode.PlayerTTL, 0},
                        {ParameterCode.CheckUserOnJoin, false},
                        {ParameterCode.CleanupCacheOnLeave, false},
                        {ParameterCode.SuppressRoomEvents, false},
                        {ParameterCode.LobbyName, "Default"},
                        {ParameterCode.LobbyType, (byte) 0},
                        {
                            ParameterCode.GameProperties, new Hashtable
                            {
                                {"GameProperty", "GamePropertyValue"},
                                {"GameProperty2", "UnexpectedByPluginValue"},
                                {GamePropertyKey.IsVisible, false},
                                {GamePropertyKey.IsOpen, false},
                                {GamePropertyKey.MaxPlayers, 10},
                                {GamePropertyKey.PropsListedInLobby, new string[] {"LobbyProperty", "LobbyPropertyValue"}}
                            }
                        },
                        {ParameterCode.PlayerProperties, new Hashtable {{"Actor2Property", "Actor2PropertyValue"}}},
                        {ParameterCode.Plugins, new string[]{"BasicTestsPlugin"}},
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);

                // connect to gameserver
                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);

                // resend request to game server
                client.SendRequestAndWaitForResponse(request);


                Assert.AreEqual(OperationCode.CreateGame, response.OperationCode);
                Assert.AreEqual(0, response.ReturnCode);

                var ev = client.WaitForEvent();
                Assert.AreEqual(EventCode.Join, ev.Code);
                ev = client.WaitForEvent();
                Assert.AreEqual(EventCode.ErrorInfo, ev.Code);
                Assert.That((string)ev[ParameterCode.Info], Is.Not.Null.Or.Empty);
                // it looks like for netframework nunit became more smart and detects somehow exceptions
                // generated in plugins although they are caugth and handled
                // Use Assert.Pass to fix that
                Assert.Pass();
            }
            finally
            {
                if (client != null && client.Connected)
                {
                    client.Disconnect();
                    client.Dispose();
                }
            }
        }

        [Test]
        public void SetStateAfterContinueFailureTest()
        {
            UnifiedTestClient client = null;
            const string GameName = "SetStateAfterContinueFailureTest";

            try
            {
                client = this.CreateMasterClientAndAuthenticate("User1");

                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.EmptyRoomTTL, 0},
                        {ParameterCode.Plugins, new string[]{"SetStateAfterContinueTestPlugin"}},
                        {ParameterCode.PlayerTTL, int.MaxValue},
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);

                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);

                client.SendRequestAndWaitForResponse(request);

                client.CheckThereIsEvent(EventCode.Join);

                client.Disconnect();
                DisposeClient(client);

                Thread.Sleep(1000);

                client = this.CreateMasterClientAndAuthenticate("User1");

                request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.EmptyRoomTTL, 0},
                        {ParameterCode.Plugins, new string[]{"SetStateAfterContinueTestPlugin"}},
                        {ParameterCode.ActorNr, 1},
                    }
                };

                response = client.SendRequestAndWaitForResponse(request);

                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);

                client.SendRequestAndWaitForResponse(request);

                client.CheckThereIsEvent(EventCode.Join);

                client.CheckThereIsEvent(123);
            }
            finally
            {
                DisposeClient(client);
            }
        }

        [Test]
        public void OnCreateWithErrorPluginTest()
        {
            UnifiedTestClient client = null;
            var GameName = MethodBase.GetCurrentMethod().Name;

            try
            {
                client = this.CreateMasterClientAndAuthenticate("User1");

                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.Plugins, new string[]{"ErrorPlugin"}},
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);

                // connect to gameserver
                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);

                // resend request to game server
                client.SendRequestAndWaitForResponse(request, (short)ErrorCode.PluginReportedError);

                Assert.AreEqual(OperationCode.CreateGame, response.OperationCode);

                EventData ev;
                Assert.IsFalse(client.TryWaitForEvent(EventCode.Join, 3000, out ev));
            }
            finally
            {
                if (client != null && client.Connected)
                {
                    client.Disconnect();
                    client.Dispose();
                }
            }
        }

        [Test]
        public void OnCreateUsingStripedGameStateTest()
        {
            UnifiedTestClient client = null;
            var GameName = MethodBase.GetCurrentMethod().Name;

            try
            {
                client = this.CreateMasterClientAndAuthenticate("User1");

                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.Plugins, new string[]{"StripedGameStatePlugin"}},
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);

                // connect to gameserver
                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);

                // resend request to game server
                client.SendRequestAndWaitForResponse(request);

                Assert.AreEqual(OperationCode.CreateGame, response.OperationCode);

                EventData ev;
                Assert.IsTrue(client.TryWaitForEvent(EventCode.Join, 3000, out ev));
            }
            finally
            {
                if (client != null && client.Connected)
                {
                    client.Disconnect();
                    client.Dispose();
                }
            }
        }

        #endregion

        #region Join Tests

        [Test]
        public void OnBeforeJoinPostConditionFail()
        {
            UnifiedTestClient client = null;
            UnifiedTestClient client2 = null;
            const string GameName = "OnBeforeJoinPostConditionFail";

            try
            {
                client = this.CreateMasterClientAndAuthenticate("User1");

                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},

                        {ParameterCode.Plugins, new string[]{"JoinFailuresCheckPlugin"}},
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);

                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);
                client.SendRequestAndWaitForResponse(request);

                var ev = client.WaitForEvent();
                Assert.AreEqual(EventCode.Join, ev.Code);

                Thread.Sleep(200);
                client2 = this.CreateMasterClientAndAuthenticate("User2");

                request = new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.PlayerProperties, new Hashtable {{"Actor2Property", "UnexpectedValue"}}},
                    },
                };

                response = client2.SendRequestAndWaitForResponse(request);
                this.ConnectAndAuthenticate(client2, (string)response[ParameterCode.Address], client2.UserId);
                client2.SendRequestAndWaitForResponse(request);

                ev = client.WaitForEvent();
                Assert.AreEqual(EventCode.Join, ev.Code);
                ev = client.WaitForEvent();
                Assert.AreEqual(EventCode.ErrorInfo, ev.Code);
                var msg = (string) ev[ParameterCode.Info];
                Assert.That(msg, Is.Not.Null.Or.Empty);

                // it looks like for netframework nunit became more smart and detects somehow exceptions
                // generated in plugins although they are caugth and handled
                // Use Assert.Pass to fix that
                Assert.Pass();
            }
            finally
            {
                DisposeClient(client);
                DisposeClient(client2);
            }
        }

        [Test]
        public void OnBeforeJoinCallsFail()
        {
            UnifiedTestClient client = null;
            UnifiedTestClient client2 = null;
            const string GameName = "OnBeforeJoinCallsFail";

            try
            {
                client = this.CreateMasterClientAndAuthenticate("User1");

                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {
                            ParameterCode.GameProperties, new Hashtable
                            {
                                {"FailBeforeJoinPreCondition", "true"},
                            }
                        },
                        {ParameterCode.Plugins, new string[]{"JoinFailuresCheckPlugin"}},
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);
                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);
                client.SendRequestAndWaitForResponse(request);

                var ev = client.WaitForEvent();
                Assert.AreEqual((byte)Hive.Operations.EventCode.Join, ev.Code);

                Thread.Sleep(200);

                client2 = this.CreateMasterClientAndAuthenticate("User2");
                this.ConnectAndAuthenticate(client2, (string)response[ParameterCode.Address], client2.UserId);

                request = new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                    },
                };

                client2.SendRequestAndWaitForResponse(request, (short)ErrorCode.PluginReportedError);

                client.CheckThereIsNoErrorInfoEvent();

            }
            finally
            {
                DisposeClient(client);
                DisposeClient(client2);
            }
        }

        [Test]
        public void OnJoinCallsFail()
        {
            UnifiedTestClient client = null;
            UnifiedTestClient client2 = null;

            const string GameName = "OnJoinPreConditionFail";

            try
            {
                client = this.CreateMasterClientAndAuthenticate("User1");

                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {
                            ParameterCode.GameProperties, new Hashtable
                            {
                                {"FailBeforeOnJoin", "true"},
                            }
                        },
                        {ParameterCode.Plugins, new string[]{"JoinFailuresCheckPlugin"}},
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);
                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);
                client.SendRequestAndWaitForResponse(request);
                client.CheckThereIsEvent(EventCode.Join);

                Thread.Sleep(200);

                client2 = this.CreateMasterClientAndAuthenticate("User2");

                request = new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.PlayerProperties, new Hashtable {{"Actor2Property", "Actor2PropertyValue"}}},
                    },
                };

                response = client2.SendRequestAndWaitForResponse(request);
                this.ConnectAndAuthenticate(client2, (string)response[ParameterCode.Address], client2.UserId);
                client2.SendRequestAndWaitForResponse(request, (short)ErrorCode.PluginReportedError);

                client.CheckThereIsEvent(123);
                client.CheckThereIsNoEvent(EventCode.Leave);
                client2.CheckThereIsNoErrorInfoEvent();

                // it looks like for netframework nunit became more smart and detects somehow exceptions
                // generated in plugins although they are caugth and handled
                // Use Assert.Pass to fix that
                Assert.Pass();
            }
            finally
            {
                DisposeClient(client);
                DisposeClient(client2);
            }
        }

        [Test]
        public void OnJoinPostConditionFail()
        {
            UnifiedTestClient client = null;
            UnifiedTestClient client2 = null;
            const string GameName = "JoinTests";

            try
            {
                client = this.CreateMasterClientAndAuthenticate("User1");

                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.EmptyRoomTTL, 0},
                        {
                            ParameterCode.GameProperties, new Hashtable
                            {
                                {"FailAfterOnJoin", "true"},
                            }
                        },
                        {ParameterCode.Plugins, new string[]{"JoinFailuresCheckPlugin"}},
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);
                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);
                client.SendRequestAndWaitForResponse(request);

                client.CheckThereIsEvent(EventCode.Join);

                Thread.Sleep(200);

                client2 = this.CreateMasterClientAndAuthenticate("User2");

                request = new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                    },
                };

                response = client2.SendRequestAndWaitForResponse(request);
                this.ConnectAndAuthenticate(client2, (string)response[ParameterCode.Address], client2.UserId);
                client2.SendRequestAndWaitForResponse(request);

                client.CheckThereIsEvent(EventCode.Join);
                client.CheckThereIsErrorInfoEvent();
                client2.CheckThereIsEvent(EventCode.Join);
                client2.CheckThereIsErrorInfoEvent();
                // it looks like for netframework nunit became more smart and detects somehow exceptions
                // generated in plugins although they are caugth and handled
                // Use Assert.Pass to fix that
                Assert.Pass();
            }
            finally
            {
                DisposeClient(client);
                DisposeClient(client2);
            }
        }

        [Test]
        public void JoinLogicFailTest()
        {
            UnifiedTestClient client = null;
            UnifiedTestClient client2 = null;
            const string GameName = "JoinLogicFailTest";

            try
            {
                client = this.CreateMasterClientAndAuthenticate("User1");

                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.EmptyRoomTTL, 0},
                        {
                            ParameterCode.GameProperties, new Hashtable
                            {
                                {"FailAfterOnJoin", "true"},
                            }
                        },
                        {ParameterCode.Plugins, new string[]{"JoinFailuresCheckPlugin"}},
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);
                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);
                client.SendRequestAndWaitForResponse(request);

                client.CheckThereIsEvent(EventCode.Join);

                Thread.Sleep(200);

                client2 = this.CreateMasterClientAndAuthenticate("User2");

                request = new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.CacheSliceIndex, 125}
                    },
                };

                response = client2.SendRequestAndWaitForResponse(request);
                this.ConnectAndAuthenticate(client2, (string)response[ParameterCode.Address], client2.UserId);
                client2.SendRequestAndWaitForResponse(request, (short)ErrorCode.OperationInvalid);

                client.CheckThereIsEvent(123);// we sent event from OnLeave
                client.CheckThereIsNoErrorInfoEvent();
            }
            finally
            {
                DisposeClient(client);
                DisposeClient(client2);
            }
        }

        //[Test]
        //public void OnJoinBlockJoinEventTest()
        //{
        //    TestClientBase client = null;
        //    TestClientBase client2 = null;
        //    const string GameName = "JoinTests";

        //    try
        //    {
        //        client = this.CreateMasterClientAndAuthenticate("User1");

        //        var request = new OperationRequest
        //        {
        //            OperationCode = OperationCode.CreateGame,
        //            Parameters = new Dictionary<byte, object>
        //            {
        //                {ParameterCode.RoomName, GameName},
        //                {ParameterCode.EmptyRoomTTL, 0},
        //                {
        //                    ParameterCode.GameProperties, new Hashtable
        //                    {
        //                        {"BlockJoinEvents", "true"},
        //                    }
        //                },
        //                {ParameterCode.Plugins, new string[]{"JoinFailuresCheckPlugin"}},
        //            }
        //        };

        //        var response = client.SendRequestAndWaitForResponse(request);
        //        this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);
        //        client.SendRequestAndWaitForResponse(request);

        //        client.CheckThereIsEvent(EventCode.Join);

        //        client2 = this.CreateMasterClientAndAuthenticate("User2");

        //        request = new OperationRequest
        //        {
        //            OperationCode = OperationCode.JoinGame,
        //            Parameters = new Dictionary<byte, object>
        //            {
        //                {ParameterCode.RoomName, GameName},
        //            },
        //        };

        //        response = client2.SendRequestAndWaitForResponse(request);
        //        this.ConnectAndAuthenticate(client2, (string)response[ParameterCode.Address], client2.UserId);
        //        response = client2.SendRequestAndWaitForResponse(request);

        //        Assert.IsNotNull(response.Parameters);
        //        Assert.IsTrue(response.Parameters.ContainsKey(0));

        //        client.CheckThereIsNoEvent(EventCode.Join);
        //        client2.CheckThereIsNoEvent(EventCode.Join);
        //    }
        //    finally
        //    {
        //        DisposeClient(client);
        //        DisposeClient(client2);
        //    }
        //}

        //[Test]
        //public void OnJoinDoNotPublishCacheTest()
        //{
        //    TestClientBase client = null;
        //    TestClientBase client2 = null;
        //    const string GameName = "JoinTests";

        //    try
        //    {
        //        client = this.CreateMasterClientAndAuthenticate("User1");

        //        var request = new OperationRequest
        //        {
        //            OperationCode = OperationCode.CreateGame,
        //            Parameters = new Dictionary<byte, object>
        //            {
        //                {ParameterCode.RoomName, GameName},
        //                {ParameterCode.EmptyRoomTTL, 0},
        //                {ParameterCode.Plugins, new string[]{"JoinFailuresCheckPlugin"}},
        //            },
        //        };

        //        var response = client.SendRequestAndWaitForResponse(request);
        //        this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);
        //        client.SendRequestAndWaitForResponse(request);

        //        client.CheckThereIsEvent(EventCode.Join);

        //        request = new OperationRequest
        //        {
        //            OperationCode = OperationCode.RaiseEvent,
        //            Parameters = new Dictionary<byte, object>
        //            {
        //                {ParameterCode.Code, (byte) 1},
        //                {ParameterCode.Cache, (byte) EventCaching.AddToRoomCache},
        //            }
        //        };

        //        client.SendRequest(request);

        //        client2 = this.CreateMasterClientAndAuthenticate("User2");

        //        request = new OperationRequest
        //        {
        //            OperationCode = OperationCode.JoinGame,
        //            Parameters = new Dictionary<byte, object>
        //            {
        //                {ParameterCode.RoomName, GameName},
        //                {
        //                    ParameterCode.PlayerProperties, new Hashtable
        //                    {
        //                        {"Actor2Property", "Actor2PropertyValue"},
        //                        {"DoNotPublishCache", "true"},
        //                    }
        //                },
        //            },
        //        };

        //        response = client2.SendRequestAndWaitForResponse(request);
        //        this.ConnectAndAuthenticate(client2, (string)response[ParameterCode.Address], client2.UserId);
        //        client2.SendRequestAndWaitForResponse(request);

        //        client.CheckThereIsNoErrorInfoEvent();

        //        client2.CheckThereIsNoEvent(1);
        //        client2.CheckThereIsNoErrorInfoEvent();

        //        client2.Disconnect();

        //        client2.EventQueueClear();

        //        Thread.Sleep(1000);

        //        client2 = this.CreateMasterClientAndAuthenticate("User2");

        //        // now we should get cached events
        //        request = new OperationRequest
        //        {
        //            OperationCode = OperationCode.JoinGame,
        //            Parameters = new Dictionary<byte, object>
        //            {
        //                {ParameterCode.RoomName, GameName},
        //                {
        //                    ParameterCode.PlayerProperties, new Hashtable
        //                    {
        //                        {"Actor2Property", "Actor2PropertyValue"},
        //                    }
        //                },
        //            },
        //        };

        //        response = client2.SendRequestAndWaitForResponse(request);
        //        this.ConnectAndAuthenticate(client2, (string)response[ParameterCode.Address], client2.UserId);
        //        client2.SendRequestAndWaitForResponse(request);

        //        client.CheckThereIsNoErrorInfoEvent();
        //        client2.CheckThereIsEvent(1);
        //        client2.CheckThereIsNoErrorInfoEvent();

        //        client2.Disconnect();

        //    }
        //    finally
        //    {
        //        DisposeClient(client);
        //        DisposeClient(client2);
        //    }
        //}

        #endregion

        #region Join with Exceptions
        [Test]
        public void BeforeJoinBeforeContinueExceptionTest()
        {
            UnifiedTestClient client = null;
            UnifiedTestClient client2 = null;
            var GameName = MethodBase.GetCurrentMethod().Name;

            try
            {
                client = this.CreateMasterClientAndAuthenticate("User1");

                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.EmptyRoomTTL, 0},
                        {ParameterCode.Plugins, new string[]{"JoinExceptionsPlugin"}},
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);
                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);
                client.SendRequestAndWaitForResponse(request);

                client.CheckThereIsEvent(EventCode.Join);

                Thread.Sleep(200);

                client2 = this.CreateMasterClientAndAuthenticate("User2");

                request = new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.PlayerProperties, new Hashtable {{(byte)255, "BeforeJoinBeforeContinueFail"}}},
                    },
                };

                response = client2.SendRequestAndWaitForResponse(request);
                this.ConnectAndAuthenticate(client2, (string)response[ParameterCode.Address], client2.UserId);
                client2.SendRequestAndWaitForResponse(request, (short)ErrorCode.PluginReportedError);

                var ev = client.WaitForEvent();
                Assert.AreEqual(124, ev.Code);// we sent it from ReportError

                Assert.IsFalse(client.TryWaitForEvent(123, 3000, out ev));

                //Assert.AreEqual(EventCode.ErrorInfo, ev.Code);
                //Assert.IsNotNullOrEmpty((string)ev[ParameterCode.Info]);

                client.CheckThereIsNoErrorInfoEvent();

                client2.Disconnect();
                client2.Dispose();

                client2 = this.CreateMasterClientAndAuthenticate("User2");

                request = new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                    },
                };

                response = client2.SendRequestAndWaitForResponse(request);
                this.ConnectAndAuthenticate(client2, (string)response[ParameterCode.Address], client2.UserId);
                response = client2.SendRequestAndWaitForResponse(request);

                var actorsList = (int[])response.Parameters[ParameterCode.ActorList];
                // there is only two!!!
                Assert.AreEqual(2, actorsList.Length);

            }
            finally
            {
                DisposeClient(client);
                DisposeClient(client2);
            }
        }

        [Test]
        public void BeforeJoinContinueExceptionTest()
        {
            if (this.IsOnline)
            {
                Assert.Ignore("Test is not supported  in 'online' mode");
            }
            UnifiedTestClient client = null;
            UnifiedTestClient client2 = null;
            var GameName = MethodBase.GetCurrentMethod().Name;

            try
            {
                client = this.CreateMasterClientAndAuthenticate("User1");

                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.EmptyRoomTTL, 0},
                        {ParameterCode.Plugins, new string[]{"JoinExceptionsPlugin"}},
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);
                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);
                client.SendRequestAndWaitForResponse(request);

                client.CheckThereIsEvent(EventCode.Join);

                Thread.Sleep(200);

                client2 = this.CreateMasterClientAndAuthenticate("User2");

                request = new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.PlayerProperties, new Hashtable {{"ProcessBeforeJoinException", ""}}},
                    },
                };

                response = client2.SendRequestAndWaitForResponse(request);
                this.ConnectAndAuthenticate(client2, (string)response[ParameterCode.Address], client2.UserId);
                client2.SendRequestAndWaitForResponse(request, (short)ErrorCode.PluginReportedError);

                var ev = client.WaitForEvent();
                Assert.AreEqual(124, ev.Code);// we sent it from ReportError

                Assert.IsFalse(client.TryWaitForEvent(123, 3000, out ev));

                //Assert.AreEqual(EventCode.ErrorInfo, ev.Code);
                //Assert.IsNotNullOrEmpty((string)ev[ParameterCode.Info]);

                client.CheckThereIsNoErrorInfoEvent();

                client2.Disconnect();
                client2.Dispose();

                client2 = this.CreateMasterClientAndAuthenticate("User2");

                request = new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                    },
                };

                response = client2.SendRequestAndWaitForResponse(request);
                this.ConnectAndAuthenticate(client2, (string)response[ParameterCode.Address], client2.UserId);
                response = client2.SendRequestAndWaitForResponse(request);

                var actorsList = (int[])response.Parameters[ParameterCode.ActorList];
                // there is only two!!!
                Assert.AreEqual(2, actorsList.Length);

            }
            finally
            {
                DisposeClient(client);
                DisposeClient(client2);
            }
        }

        [Test]
        public void OnJoinBeforeContinueExceptionTest()
        {
            UnifiedTestClient client = null;
            UnifiedTestClient client2 = null;
            var GameName = MethodBase.GetCurrentMethod().Name;

            try
            {
                client = this.CreateMasterClientAndAuthenticate("User1");

                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.EmptyRoomTTL, 0},
                        {ParameterCode.Plugins, new string[]{"JoinExceptionsPlugin"}},
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);
                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);
                client.SendRequestAndWaitForResponse(request);

                client.CheckThereIsEvent(EventCode.Join);

                Thread.Sleep(200);

                client2 = this.CreateMasterClientAndAuthenticate("User2");

                request = new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.PlayerProperties, new Hashtable {{(byte)255, "OnJoinBeforeContinueFail"}}},
                    },
                };

                response = client2.SendRequestAndWaitForResponse(request);
                this.ConnectAndAuthenticate(client2, (string)response[ParameterCode.Address], client2.UserId);
                client2.SendRequestAndWaitForResponse(request, (short)ErrorCode.PluginReportedError);

                var ev = client.WaitForEvent();
                Assert.AreEqual(124, ev.Code);// we sent it from ReportError

                ev = client.WaitForEvent();
                Assert.AreEqual(123, ev.Code);// we sent it from OnLeave

                //Assert.IsFalse(client.TryWaitForEvent(EventCode.ErrorInfo, 3000, out ev));

                //Assert.AreEqual(EventCode.ErrorInfo, ev.Code);
                //Assert.IsNotNullOrEmpty((string)ev[ParameterCode.Info]);

                client.CheckThereIsNoErrorInfoEvent();

                client2.Disconnect();
                client2.Dispose();

                client2 = this.CreateMasterClientAndAuthenticate("User2");

                request = new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                    },
                };

                response = client2.SendRequestAndWaitForResponse(request);
                this.ConnectAndAuthenticate(client2, (string)response[ParameterCode.Address], client2.UserId);
                response = client2.SendRequestAndWaitForResponse(request);

                var actorsList = (int[]) response.Parameters[ParameterCode.ActorList];
                // there is only two!!!
                Assert.AreEqual(2, actorsList.Length);

            }
            finally
            {
                DisposeClient(client);
                DisposeClient(client2);
            }
        }

        [Test]
        public void BeforeJoinAfterContinueExceptionTest()
        {
            UnifiedTestClient client = null;
            UnifiedTestClient client2 = null;
            var GameName = MethodBase.GetCurrentMethod().Name;

            try
            {
                client = this.CreateMasterClientAndAuthenticate("User1");

                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.EmptyRoomTTL, 0},
                        {ParameterCode.Plugins, new string[]{"JoinExceptionsPlugin"}},
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);
                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);
                client.SendRequestAndWaitForResponse(request);

                client.CheckThereIsEvent(EventCode.Join);

                Thread.Sleep(200);

                client2 = this.CreateMasterClientAndAuthenticate("User2");

                request = new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.PlayerProperties, new Hashtable {{(byte)255, "BeforeJoinAfterContinueFail"}}},
                    },
                };

                response = client2.SendRequestAndWaitForResponse(request);
                this.ConnectAndAuthenticate(client2, (string)response[ParameterCode.Address], client2.UserId);
                client2.SendRequestAndWaitForResponse(request);

                // message from ReportError should be get twice
                var ev = client.WaitForEvent();
                Assert.AreEqual(EventCode.Join, ev.Code);

                ev = client.WaitForEvent();
                Assert.AreEqual(124, ev.Code);// we sent it from ReportError

                client.CheckThereIsNoErrorInfoEvent();
            }
            finally
            {
                DisposeClient(client);
                DisposeClient(client2);
            }
        }

        #endregion

        #region Leave Tests

        [TestCase("OnLeaveFailsInPlugins")]
        [TestCase("OnLeaveNullsPluginHost")]
        public void Leave_ExceptionInOnLeave(string testCase)
        {
            this.Leave_ExceptionInOnLeaveBody(testCase);
        }

        protected virtual void Leave_ExceptionInOnLeaveBody(string testCase)
        {
            UnifiedTestClient client = null;
            UnifiedTestClient client2 = null;
            var GameName = RandomizeString(testCase);

            try
            {
                client = this.CreateMasterClientAndAuthenticate("User1");

                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},

                        {ParameterCode.Plugins, new string[] {"OnLeaveExceptionsPlugin"}},
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);

                this.ConnectAndAuthenticate(client, (string) response[ParameterCode.Address], client.UserId);
                client.SendRequestAndWaitForResponse(request);

                var ev = client.WaitForEvent();
                Assert.AreEqual(EventCode.Join, ev.Code);

                client.Disconnect();

                Thread.Sleep(1000);
                client2 = this.CreateMasterClientAndAuthenticate("User2");

                request = new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.PlayerProperties, new Hashtable {{"Actor2Property", "UnexpectedValue"}}},
                    },
                };

                client2.SendRequestAndWaitForResponse(request, (short) ErrorCode.GameIdNotExists);
            }
            finally
            {
                DisposeClient(client);
                DisposeClient(client2);
            }
        }

        #endregion

        #region Setting Properites to Actor during Join
        [Test]
        public void SetPropertiesToActorOnCreateTest([Values("SetOnCreateBeforeContinue", "SetOnCreateAfterContinue")] string gameName)
        {
            this.SetPropertiesToActorOnCreateTestBody(gameName);
        }

        protected virtual void SetPropertiesToActorOnCreateTestBody(string gameName)
        {
            UnifiedTestClient client = null;
            var GameName = gameName;

            try
            {
                client = this.CreateMasterClientAndAuthenticate("User1");

                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.EmptyRoomTTL, 0},
                        {ParameterCode.Plugins, new string[]{"ActorPropertiesBroadcastDuringJoin"}},
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);
                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);
                if (gameName.Contains("AfterContinue"))
                {
                    client.SendRequestAndWaitForResponse(request);
                    client.CheckThereIsEvent(EventCode.Join);
                }
                else
                {
                    client.SendRequestAndWaitForResponse(request, (short)ErrorCode.PluginReportedError);
                    client.CheckThereIsNoEvent(EventCode.Join);
                }
            }
            finally
            {
                DisposeClient(client);
            }
        }

        [Test]
        public void SetPropertiesToActorOnJoinTest(
            [Values(
                "SetBeforeJoinBeforeContinue",
                "SetBeforeJoinAfterContinue",
                "SetOnJoinBeforeContinue",
                "SetOnJoinAfterContinue"
            )] string gameName)
        {
            this.SetPropertiesToActorOnJoinTestBody(gameName);
        }

        protected virtual void SetPropertiesToActorOnJoinTestBody(string gameName)
        {
            UnifiedTestClient client = null;
            UnifiedTestClient client2 = null;
            var GameName = gameName;

            try
            {
                client = this.CreateMasterClientAndAuthenticate("User1");

                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.EmptyRoomTTL, 0},
                        {ParameterCode.Plugins, new string[]{"ActorPropertiesBroadcastDuringJoin"}},
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);
                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);
                client.SendRequestAndWaitForResponse(request);

                client.CheckThereIsEvent(EventCode.Join);

                Thread.Sleep(200);

                client2 = this.CreateMasterClientAndAuthenticate("User2");

                request = new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                    },
                };

                response = client2.SendRequestAndWaitForResponse(request);
                this.ConnectAndAuthenticate(client2, (string)response[ParameterCode.Address], client2.UserId);
                if (gameName.Contains("BeforeContinue"))
                {
                    client2.SendRequestAndWaitForResponse(request, (short)ErrorCode.PluginReportedError);

                    var ev = client.WaitForEvent();
                    Assert.AreEqual(124, ev.Code); // we sent it from ReportError
                    client.CheckThereIsErrorInfoEvent();
                }
                else
                {
                    client2.SendRequestAndWaitForResponse(request);
                    client.CheckThereIsNoErrorInfoEvent();
                }
            }
            finally
            {
                DisposeClient(client);
                DisposeClient(client2);
            }
        }

        #endregion

        #region Scheduling Tests
        [Test]
        public void ScheduleBroadcastEvent()
        {
            UnifiedTestClient client = null;
            string GameName = MethodBase.GetCurrentMethod().Name;

            try
            {
                client = this.CreateMasterClientAndAuthenticate("User1");

                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.EmptyRoomTTL, 0},
                        {
                            ParameterCode.GameProperties, new Hashtable
                            {
                                {"EventCode", 1},
                                {"Interval", 25}, // ms 
                                {"EventSize", 100}, // bytes
                            }
                        },
                        {ParameterCode.Plugins, new string[]{"ScheduleBroadcastTestPlugin"}},
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);
                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);
                client.SendRequestAndWaitForResponse(request);

                client.CheckThereIsEvent(EventCode.Join);
                client.CheckThereIsEvent(1);

                client.OpSetPropertiesOfRoom(new Hashtable
                                                 {
                                                    {"EventCode", 2},
                                                    {"Interval", 25}, // ms 
                                                    {"EventSize", 100}, // bytes
                                                 });

                //client.CheckThereIsEventAndFailOnTimout(EventCode.PropertiesChanged);

                client.CheckThereIsEvent(2);

            }
            finally
            {
                DisposeClient(client);
            }
        }

        [Test]
        public void ScheduleSetProperties()
        {
            UnifiedTestClient client = null;
            string GameName = MethodBase.GetCurrentMethod().Name;

            try
            {
                client = this.CreateMasterClientAndAuthenticate("User1");

                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.EmptyRoomTTL, 0},
                        {
                            ParameterCode.GameProperties, new Hashtable
                            {
                                {"EventCode", 1},
                                {"Interval", 25}, // ms 
                                {"RoomIndex", 1}, // start value
                            }
                        },
                        {ParameterCode.Plugins, new string[]{"ScheduleSetPropertiesTestPlugin"}},
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);
                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);
                client.SendRequestAndWaitForResponse(request);

                client.CheckThereIsEvent(EventCode.Join);
                client.CheckThereIsEvent(EventCode.PropertiesChanged);

            }
            finally
            {
                DisposeClient(client);
            }
        }

        #endregion

        #region MasterClientId Tests

        [Test]
        public void MasterClientIdChange()
        {
            UnifiedTestClient client = null;
            UnifiedTestClient client2 = null;
            const string GameName = "MasterClientIdTests";

            try
            {
                client = this.CreateMasterClientAndAuthenticate("User1");

                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},

                        {ParameterCode.Plugins, new string[]{"MasterClientIdPlugin"}},
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);

                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);
                client.SendRequestAndWaitForResponse(request);

                var ev = client.WaitForEvent();
                Assert.AreEqual(EventCode.Join, ev.Code);

                client2 = this.CreateMasterClientAndAuthenticate("User2");

                request = new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.PlayerProperties, new Hashtable {{"Actor2Property", "UnexpectedValue"}}},
                    },
                };

                response = client2.SendRequestAndWaitForResponse(request);
                this.ConnectAndAuthenticate(client2, (string)response[ParameterCode.Address], client2.UserId);
                client2.SendRequestAndWaitForResponse(request);

                ev = client.WaitForEvent();
                Assert.AreEqual(EventCode.Join, ev.Code);

                client.CheckThereIsNoErrorInfoEvent();

                client.Disconnect();

                client2.CheckThereIsEvent(123);

            }
            finally
            {
                DisposeClient(client);
                DisposeClient(client2);
            }
        }

        #endregion

        #region Set properties tests

        private static void CheckActorPropertyValue(UnifiedTestClient client, string propertyName, string expectedPropertyValue)
        {
            // check property value
            var request = new OperationRequest
            {
                OperationCode = OperationCode.GetProperties,
                Parameters = new Dictionary<byte, object>
                {
                    {ParameterCode.ActorList, new int[] {1}},
                    {ParameterCode.PlayerProperties, new string[] {propertyName}},
                    {ParameterCode.Properties, (byte) 2}
                }
            };

            var response = client.SendRequestAndWaitForResponse(request);

            var properties = (Hashtable)response[ParameterCode.PlayerProperties];
            Assert.IsNotNull(properties);
            var actor1Properties = (Hashtable)properties[1];
            Assert.IsNotNull(actor1Properties);
            Assert.AreEqual(expectedPropertyValue, actor1Properties[propertyName]);
        }

        [Test]
        public void BeforeSetGamePropertiesPreCheck()
        {
            UnifiedTestClient client = null;
            UnifiedTestClient client2 = null;
            const string GameName = "SetProperties";

            try
            {
                client = this.CreateMasterClientAndAuthenticate("User1");

                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {
                            ParameterCode.GameProperties, new Hashtable
                            {
                                {"GameProperty", "GamePropertyValue"},
                                {"GameProperty2", "GamePropertyValue2"},
                            }
                        },
                        {ParameterCode.Plugins, new string[]{"SetPropertiesCheckPlugin"}},
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);

                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);

                client.SendRequestAndWaitForResponse(request);

                client.CheckThereIsNoErrorInfoEvent();

                request = new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                    },
                };

                client2 = this.CreateMasterClientAndAuthenticate("User2");

                response = client2.SendRequestAndWaitForResponse(request);

                this.ConnectAndAuthenticate(client2, (string)response[ParameterCode.Address], client2.UserId);

                client2.SendRequestAndWaitForResponse(request);
                client.CheckThereIsNoErrorInfoEvent();
                client2.CheckThereIsNoErrorInfoEvent();

                // just set property in order to check it value later
                request = new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.ActorNr, 1},
                        {
                            ParameterCode.Properties, new Hashtable
                            {
                                {"ActorProperty", "PropertyValue"}
                            }
                        },
                    }
                };

                client.SendRequestAndWaitForResponse(request);
                client.CheckThereIsNoErrorInfoEvent();


                request = new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.ActorNr, 1},
                        {
                            ParameterCode.Properties, new Hashtable
                            {
                                {"ActorProperty", "BeforeSetPropertiesPreCheckFail"}
                            }
                        },
                    }
                };

                client.SendRequestAndWaitForResponse(request, (short)ErrorCode.PluginReportedError);

                client.CheckThereIsNoErrorInfoEvent();
                client2.CheckThereIsNoErrorInfoEvent();

                CheckActorPropertyValue(client, "ActorProperty", "PropertyValue");
            }
            finally
            {
                DisposeClient(client);
                DisposeClient(client2);
            }
        }

        [Test]
        public void BeforeSetGamePropertiesExceptionInContinueCheck()
        {
            if (this.IsOnline)
            {
                Assert.Ignore("This test does not support 'Online' mode");
            }

            UnifiedTestClient client = null;
            UnifiedTestClient client2 = null;
            const string GameName = "SetProperties";

            try
            {
                client = this.CreateMasterClientAndAuthenticate("User1");

                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {
                            ParameterCode.GameProperties, new Hashtable
                            {
                                {"GameProperty", "GamePropertyValue"},
                                {"GameProperty2", "GamePropertyValue2"},
                            }
                        },
                        {ParameterCode.Plugins, new string[]{"SetPropertiesCheckPlugin"}},
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);

                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);

                client.SendRequestAndWaitForResponse(request);

                client.CheckThereIsNoErrorInfoEvent();

                request = new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                    },
                };

                client2 = this.CreateMasterClientAndAuthenticate("User2");

                response = client2.SendRequestAndWaitForResponse(request);

                this.ConnectAndAuthenticate(client2, (string)response[ParameterCode.Address], client2.UserId);

                client2.SendRequestAndWaitForResponse(request);
                client.CheckThereIsNoErrorInfoEvent();
                client2.CheckThereIsNoErrorInfoEvent();

                // just set property in order to check it value later
                request = new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.ActorNr, 1},
                        {
                            ParameterCode.Properties, new Hashtable
                            {
                                {"ActorProperty", "PropertyValue"}
                            }
                        },
                    }
                };

                client.SendRequestAndWaitForResponse(request);
                client.CheckThereIsNoErrorInfoEvent();


                request = new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.ActorNr, 1},
                        {
                            ParameterCode.Properties, new Hashtable
                            {
                                {"ActorProperty", "BeforeSetPropertiesExceptionInContinue"}
                            }
                        },
                    }
                };

                client.SendRequestAndWaitForResponse(request, (short)ErrorCode.InternalServerError);

                client.CheckThereIsNoErrorInfoEvent();
                client2.CheckThereIsNoErrorInfoEvent();

                CheckActorPropertyValue(client, "ActorProperty", "PropertyValue");
            }
            finally
            {
                DisposeClient(client);
                DisposeClient(client2);
            }
        }

        [Test]
        public void BeforeSetGamePropertiesPostCheck()
        {
            UnifiedTestClient client = null;
            UnifiedTestClient client2 = null;
            const string GameName = "SetProperties";

            try
            {
                client = this.CreateMasterClientAndAuthenticate("User1");

                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {
                            ParameterCode.GameProperties, new Hashtable
                            {
                                {"GameProperty", "GamePropertyValue"},
                                {"GameProperty2", "GamePropertyValue2"},
                            }
                        },
                        {ParameterCode.Plugins, new string[]{"SetPropertiesCheckPlugin"}},
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);

                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);

                client.SendRequestAndWaitForResponse(request);

                client.CheckThereIsNoErrorInfoEvent();

                request = new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                    },
                };

                client2 = this.CreateMasterClientAndAuthenticate("User2");
                response = client2.SendRequestAndWaitForResponse(request);

                this.ConnectAndAuthenticate(client2, (string)response[ParameterCode.Address], client2.UserId);

                client2.SendRequestAndWaitForResponse(request);
                client.CheckThereIsNoErrorInfoEvent();
                client2.CheckThereIsNoErrorInfoEvent();

                request = new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.ActorNr, 1},
                        {
                            ParameterCode.Properties, new Hashtable
                            {
                                {"ActorProperty", "BeforeSetPropertiesPostCheckFail"}
                            }
                        },
                    }
                };

                client.SendRequestAndWaitForResponse(request);

                client.CheckThereIsErrorInfoEvent();
                client2.CheckThereIsErrorInfoEvent();

                CheckActorPropertyValue(client, "ActorProperty", "BeforeSetPropertiesPostCheckFail");
            }
            finally
            {
                DisposeClient(client);
                DisposeClient(client2);
            }
        }

        [Test]
        public void OnSetPropertiesPreCheck()
        {
            UnifiedTestClient client = null;
            UnifiedTestClient client2 = null;
            const string GameName = "SetProperties";

            try
            {
                client = this.CreateMasterClientAndAuthenticate("User1");

                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {
                            ParameterCode.GameProperties, new Hashtable
                            {
                                {"GameProperty", "GamePropertyValue"},
                                {"GameProperty2", "GamePropertyValue2"},
                            }
                        },
                        {ParameterCode.Plugins, new string[]{"SetPropertiesCheckPlugin"}},
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);
                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);
                client.SendRequestAndWaitForResponse(request);

                client.CheckThereIsNoErrorInfoEvent();

                request = new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                    },
                };

                client2 = this.CreateMasterClientAndAuthenticate("User2");

                response = client2.SendRequestAndWaitForResponse(request);

                this.ConnectAndAuthenticate(client2, (string)response[ParameterCode.Address], client2.UserId);

                client2.SendRequestAndWaitForResponse(request);

                client.CheckThereIsNoErrorInfoEvent();
                client2.CheckThereIsNoErrorInfoEvent();

                var propertyValue = "PropertyValue";
                var propertyKey = "ActorProperty";
                // just set property in order to check it value later
                request = new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.ActorNr, 1},
                        {
                            ParameterCode.Properties, new Hashtable
                            {
                                {propertyKey, propertyValue}
                            }
                        },
                    }
                };

                client.SendRequestAndWaitForResponse(request);
                client.CheckThereIsNoErrorInfoEvent();


                request = new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.ActorNr, 1},
                        {
                            ParameterCode.Properties, new Hashtable
                            {
                                {propertyKey, "OnSetPropertiesPreCheckFail"}
                            }
                        },
                    }
                };

                client.SendRequestAndWaitForResponse(request, (short)ErrorCode.PluginReportedError);

                client.CheckThereIsNoEvent(EventCode.PropertiesChanged);
                client2.CheckThereIsNoEvent(EventCode.PropertiesChanged);
                client.CheckThereIsNoErrorInfoEvent();
                client2.CheckThereIsNoErrorInfoEvent();

                CheckActorPropertyValue(client, propertyKey, "OnSetPropertiesPreCheckFail");
            }
            finally
            {
                DisposeClient(client);
                DisposeClient(client2);
            }
        }

        [Test]
        public void OnSetPropertiesFailTest()
        {
            UnifiedTestClient client = null;
            UnifiedTestClient client2 = null;
            const string GameName = "SetProperties";

            try
            {
                client = this.CreateMasterClientAndAuthenticate("User1");

                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {
                            ParameterCode.GameProperties, new Hashtable
                            {
                                {"GameProperty", "GamePropertyValue"},
                                {"GameProperty2", "GamePropertyValue2"},
                            }
                        },
                        {ParameterCode.Plugins, new string[]{"SetPropertiesCheckPlugin"}},
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);
                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);
                client.SendRequestAndWaitForResponse(request);

                client.CheckThereIsNoErrorInfoEvent();

                request = new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                    },
                };

                client2 = this.CreateMasterClientAndAuthenticate("User2");

                response = client2.SendRequestAndWaitForResponse(request);

                this.ConnectAndAuthenticate(client2, (string)response[ParameterCode.Address], client2.UserId);

                client2.SendRequestAndWaitForResponse(request);

                client.CheckThereIsNoErrorInfoEvent();
                client2.CheckThereIsNoErrorInfoEvent();

                const string propertyValue = "PropertyValue";
                const string propertyKey = "ActorProperty";
                // just set property in order to check it value later
                request = new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.ActorNr, 1},
                        {
                            ParameterCode.Properties, new Hashtable
                            {
                                {propertyKey, propertyValue}
                            }
                        },
                    }
                };

                client.SendRequestAndWaitForResponse(request);
                client.CheckThereIsNoErrorInfoEvent();


                request = new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.ActorNr, 1},
                        {
                            ParameterCode.Properties, new Hashtable
                            {
                                {propertyKey, "OnSetPropertiesPreCheckFail"}
                            }
                        },
                    }
                };

                client.SendRequestAndWaitForResponse(request, (short)ErrorCode.PluginReportedError);

                client.CheckThereIsNoEvent(EventCode.PropertiesChanged);
                client2.CheckThereIsNoEvent(EventCode.PropertiesChanged);
                client.CheckThereIsNoErrorInfoEvent();
                client2.CheckThereIsNoErrorInfoEvent();

                CheckActorPropertyValue(client, propertyKey, "OnSetPropertiesPreCheckFail");

                const string newPropertyValue = "NewPropertyValue";
                request = new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.ActorNr, 1},
                        {ParameterCode.Broadcast, true},
                        {
                            ParameterCode.Properties, new Hashtable
                            {
                                {propertyKey, newPropertyValue}
                            }
                        },
                    }
                };

                client.SendRequestAndWaitForResponse(request);

                client.CheckThereIsNoEvent(EventCode.PropertiesChanged);
                client2.CheckThereIsEvent(EventCode.PropertiesChanged);
                client.CheckThereIsNoErrorInfoEvent();
                client2.CheckThereIsNoErrorInfoEvent();

                CheckActorPropertyValue(client, propertyKey, newPropertyValue);

            }
            finally
            {
                DisposeClient(client);
                DisposeClient(client2);
            }
        }

        [Test]
        public void OnSetGamePropertiesPostCheck()
        {
            UnifiedTestClient client = null;
            UnifiedTestClient client2 = null;
            const string GameName = "SetProperties";

            try
            {
                client = this.CreateMasterClientAndAuthenticate("User1");

                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {
                            ParameterCode.GameProperties, new Hashtable
                            {
                                {"GameProperty", "GamePropertyValue"},
                                {"GameProperty2", "GamePropertyValue2"},
                            }
                        },
                        {ParameterCode.Plugins, new string[]{"SetPropertiesCheckPlugin"}},
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);

                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);

                client.SendRequestAndWaitForResponse(request);

                client.CheckThereIsNoErrorInfoEvent();

                request = new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                    },
                };

                client2 = this.CreateMasterClientAndAuthenticate("User2");
                response = client2.SendRequestAndWaitForResponse(request);

                this.ConnectAndAuthenticate(client2, (string)response[ParameterCode.Address], client2.UserId);

                client2.SendRequestAndWaitForResponse(request);

                client.CheckThereIsNoErrorInfoEvent();
                client2.CheckThereIsNoErrorInfoEvent();

                request = new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.ActorNr, 1},
                        {
                            ParameterCode.Properties, new Hashtable
                            {
                                {"ActorProperty", "BeforeSetPropertiesPostCheckFail"}
                            }
                        },
                    }
                };

                client.SendRequestAndWaitForResponse(request);

                client.CheckThereIsErrorInfoEvent();

                CheckActorPropertyValue(client, "ActorProperty", "BeforeSetPropertiesPostCheckFail");
            }
            finally
            {
                DisposeClient(client);
                DisposeClient(client2);
            }
        }

        [Test]
        public void OnSetGamePropertiesExceptionInContinueCheck()
        {
            if (this.IsOnline)
            {
                Assert.Ignore("This test does not support 'Online' mode");
            }

            UnifiedTestClient client = null;
            UnifiedTestClient client2 = null;
            const string GameName = "OnSetProperties";

            try
            {
                client = this.CreateMasterClientAndAuthenticate("User1");

                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {
                            ParameterCode.GameProperties, new Hashtable
                            {
                                {"GameProperty", "GamePropertyValue"},
                                {"GameProperty2", "GamePropertyValue2"},
                            }
                        },
                        {ParameterCode.Plugins, new []{"SetPropertiesCheckPlugin"}},
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);

                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);

                client.SendRequestAndWaitForResponse(request);

                client.CheckThereIsNoErrorInfoEvent();

                request = new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                    },
                };

                client2 = this.CreateMasterClientAndAuthenticate("User2");

                response = client2.SendRequestAndWaitForResponse(request);

                this.ConnectAndAuthenticate(client2, (string)response[ParameterCode.Address], client2.UserId);

                client2.SendRequestAndWaitForResponse(request);
                client.CheckThereIsNoErrorInfoEvent();
                client2.CheckThereIsNoErrorInfoEvent();

                // just set property in order to check it value later
                request = new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.ActorNr, 1},
                        {
                            ParameterCode.Properties, new Hashtable
                            {
                                {"ActorProperty", "PropertyValue"}
                            }
                        },
                    }
                };

                client.SendRequestAndWaitForResponse(request);
                client.CheckThereIsNoErrorInfoEvent();


                request = new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.ActorNr, 1},
                        {
                            ParameterCode.Properties, new Hashtable
                            {
                                {"ActorProperty", "OnSetPropertiesExceptionInContinue"}
                            }
                        },
                    }
                };

                client.SendRequestAndWaitForResponse(request, (short)ErrorCode.InternalServerError);

                client.CheckThereIsNoErrorInfoEvent();
                client2.CheckThereIsNoErrorInfoEvent();

                CheckActorPropertyValue(client, "ActorProperty", "OnSetPropertiesExceptionInContinue");
            }
            finally
            {
                DisposeClient(client);
                DisposeClient(client2);
            }
        }

        [Test]
        public void OnSetGamePropertiesCASFailureCheck()
        {
            UnifiedTestClient client = null;
            UnifiedTestClient client2 = null;
            const string GameName = "SetPropertiesCAS";

            try
            {
                client = this.CreateMasterClientAndAuthenticate("User1");

                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {
                            ParameterCode.GameProperties, new Hashtable
                            {
                                {"GameProperty", "GamePropertyValue"},
                                {"GameProperty2", "GamePropertyValue2"},
                            }
                        },
                        {ParameterCode.Plugins, new string[]{"SetPropertiesCheckPlugin"}},
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);

                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);

                client.SendRequestAndWaitForResponse(request);

                client.CheckThereIsNoErrorInfoEvent();

                request = new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                    },
                };

                client2 = this.CreateMasterClientAndAuthenticate("User2");
                response = client2.SendRequestAndWaitForResponse(request);

                this.ConnectAndAuthenticate(client2, (string)response[ParameterCode.Address], client2.UserId);

                client2.SendRequestAndWaitForResponse(request);

                client.CheckThereIsNoErrorInfoEvent();
                client2.CheckThereIsNoErrorInfoEvent();

                const string PropertyValue = "PropertyValue";
                request = new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.ActorNr, 1},
                        {
                            ParameterCode.Properties, new Hashtable
                            {
                                {"ActorProperty", PropertyValue}
                            }
                        },
                    }
                };

                client.SendRequestAndWaitForResponse(request);

                client.CheckThereIsNoErrorInfoEvent();

                CheckActorPropertyValue(client, "ActorProperty", PropertyValue);

                request = new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.ActorNr, 1},
                        {
                            ParameterCode.Properties, new Hashtable
                            {
                                {"ActorProperty", "AnotherValueForCASCheck"}
                            }
                        },
                        {
                            ParameterCode.ExpectedValues, new Hashtable
                            {
                                {"ActorProperty", "NonExistingValue"}
                            }
                        },
                    }
                };

                client.SendRequestAndWaitForResponse(request, (short)ErrorCode.OperationInvalid);

                client.CheckThereIsEvent(124);
                client.CheckThereIsNoErrorInfoEvent();

                CheckActorPropertyValue(client, "ActorProperty", PropertyValue);
            }
            finally
            {
                DisposeClient(client);
                DisposeClient(client2);
            }
        }

        [Test]
        public void OnSetGamePropertiesCASNotificationCheck()
        {
            UnifiedTestClient client = null;
            UnifiedTestClient client2 = null;
            const string GameName = "SetPropertiesCAS";

            try
            {
                client = this.CreateMasterClientAndAuthenticate("User1");

                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {
                            ParameterCode.GameProperties, new Hashtable
                            {
                                {"GameProperty", "GamePropertyValue"},
                                {"GameProperty2", "GamePropertyValue2"},
                            }
                        },
                        {ParameterCode.Plugins, new string[]{"SetPropertiesCheckPlugin"}},
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);

                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);

                client.SendRequestAndWaitForResponse(request);

                client.CheckThereIsNoErrorInfoEvent();

                request = new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                    },
                };

                client2 = this.CreateMasterClientAndAuthenticate("User2");
                response = client2.SendRequestAndWaitForResponse(request);

                this.ConnectAndAuthenticate(client2, (string)response[ParameterCode.Address], client2.UserId);

                client2.SendRequestAndWaitForResponse(request);

                client.CheckThereIsNoErrorInfoEvent();
                client2.CheckThereIsNoErrorInfoEvent();

                const string PropertyValue = "PropertyValue";
                request = new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.ActorNr, 1},
                        {
                            ParameterCode.Properties, new Hashtable
                            {
                                {"ActorProperty", PropertyValue}
                            }
                        },
                    }
                };

                client.SendRequestAndWaitForResponse(request);

                client.CheckThereIsNoErrorInfoEvent();

                CheckActorPropertyValue(client, "ActorProperty", PropertyValue);

                const string AnotherProperty = "AnotherValueForCASCheck";
                request = new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.ActorNr, 1},
                        {
                            ParameterCode.Properties, new Hashtable
                            {
                                {"ActorProperty", AnotherProperty}
                            }
                        },
                        {
                            ParameterCode.ExpectedValues, new Hashtable
                            {
                                {"ActorProperty", PropertyValue}
                            }
                        },
                    }
                };

                client.SendRequestAndWaitForResponse(request);

                client.CheckThereIsEvent(EventCode.PropertiesChanged);
                client2.CheckThereIsEvent(EventCode.PropertiesChanged);

                client.CheckThereIsNoEvent(124);
                client.CheckThereIsNoErrorInfoEvent();

                CheckActorPropertyValue(client, "ActorProperty", AnotherProperty);
            }
            finally
            {
                DisposeClient(client);
                DisposeClient(client2);
            }
        }

        [Test]
        public void UpdateGamePropertiesOnMasterFromPlugin()
        {
            UnifiedTestClient client = null;
            UnifiedTestClient client2 = null;
            UnifiedTestClient client3 = null;
            const string GameName = "SetPropertiesCAS";

            try
            {
                client = this.CreateMasterClientAndAuthenticate("User1");

                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.Plugins, new string[] {"ChangeGamePropertiesOnJoinPlugin"}},
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);

                this.ConnectAndAuthenticate(client, (string) response[ParameterCode.Address], client.UserId);

                client.SendRequestAndWaitForResponse(request);

                client.CheckThereIsNoErrorInfoEvent();

                request = new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                    },
                };

                client2 = this.CreateMasterClientAndAuthenticate("User2");
                response = client2.SendRequestAndWaitForResponse(request);

                this.ConnectAndAuthenticate(client2, (string) response[ParameterCode.Address], client2.UserId);

                client2.SendRequestAndWaitForResponse(request);

                client.CheckThereIsNoErrorInfoEvent();
                client2.CheckThereIsNoErrorInfoEvent();

                Thread.Sleep(100);

                client3 = this.CreateMasterClientAndAuthenticate("User3");

                client3.JoinRandomGame(null, 0, null, MatchmakingMode.RandomMatching, "", AppLobbyType.Default, "", (short)ErrorCode.NoMatchFound);
            }
            finally
            {
                DisposeClient(client);
                DisposeClient(client2);
                DisposeClient(client3);
            }
        }
        #endregion

        #region Raise Event tests
        [Test]
        public void RaiseEventCacheManagmentTest()
        {
            UnifiedTestClient client = null;
            const string GameName = "RaiseEvent";

            try
            {
                client = this.CreateMasterClientAndAuthenticate("User1");

                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.EmptyRoomTTL, 0},
                        {ParameterCode.Plugins, new string[]{"RaiseEventChecksPlugin"}},
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);

                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);

                client.SendRequestAndWaitForResponse(request);

                client.CheckThereIsEvent(EventCode.Join);

                request = new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Code, (byte) 1},
                        {ParameterCode.Cache, (byte) EventCaching.AddToRoomCache},
                        {ParameterCode.Data, new Hashtable{{0, 1}}},// expected in cache
                    }
                };

                client.SendRequest(request);

                client.CheckThereIsNoErrorInfoEvent();

                request = new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Code, (byte) 2},
                        {ParameterCode.Cache, (byte) EventCaching.SliceIncreaseIndex},
                        {ParameterCode.Data, new Hashtable{{0, 2}}},// expected сaches count
                    }
                };

                client.SendRequest(request);

                client.CheckThereIsNoErrorInfoEvent();

                request = new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Code, (byte) 1},
                        {ParameterCode.Cache, (byte) EventCaching.AddToRoomCache},
                        {ParameterCode.Data, new Hashtable{{0, 1}}},// expected in cache
                    }
                };

                client.SendRequest(request);

                request = new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Code, (byte) 1},
                        {ParameterCode.Cache, (byte) EventCaching.AddToRoomCache},
                        {ParameterCode.Data, new Hashtable{{0, 2}}},// expected in cache
                    }
                };

                client.SendRequest(request);

                client.CheckThereIsNoErrorInfoEvent();

                request = new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Code, (byte) 2},
                        {ParameterCode.Cache, (byte) EventCaching.SlicePurgeIndex},
                        {ParameterCode.CacheSliceIndex, 0},
                        {ParameterCode.Data, new Hashtable{{0, 1}}},// expected сaches count
                    }
                };

                client.SendRequest(request);

                client.CheckThereIsNoErrorInfoEvent();

                for (var i = 0; i < 5; ++i)
                {
                    request = new OperationRequest
                    {
                        OperationCode = OperationCode.RaiseEvent,
                        Parameters = new Dictionary<byte, object>
                        {
                            {ParameterCode.Code, (byte) 2},
                            {ParameterCode.Cache, (byte) EventCaching.SliceIncreaseIndex},
                            {ParameterCode.Data, new Hashtable{{0, 2 + i}}},// expected сaches count
                        }
                    };

                    client.SendRequest(request);

                    client.CheckThereIsNoErrorInfoEvent();
                }

                request = new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Code, (byte) 3},
                        {ParameterCode.Cache, (byte) EventCaching.SliceSetIndex},
                        {ParameterCode.CacheSliceIndex, 10},
                        {ParameterCode.Data, new Hashtable{{0, 10}}},// expected index
                    }
                };

                client.SendRequest(request);

                client.CheckThereIsNoErrorInfoEvent();

                request = new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Code, (byte) 3},
                        {ParameterCode.Cache, (byte) EventCaching.SliceSetIndex},
                        {ParameterCode.CacheSliceIndex, 5},
                        {ParameterCode.Data, new Hashtable{{0, 5}}},// expected index
                    }
                };

                client.SendRequest(request);

                client.CheckThereIsNoErrorInfoEvent();

                request = new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                        {
                            {ParameterCode.Code, (byte) 2},
                            {ParameterCode.Cache, (byte) EventCaching.SlicePurgeUpToIndex},
                            {ParameterCode.CacheSliceIndex, 4},
                            {ParameterCode.Data, new Hashtable{{0, 4}}},// expected сaches count
                        }
                };

                client.SendRequest(request);

                client.CheckThereIsNoErrorInfoEvent();
            }
            finally
            {
                DisposeClient(client);
            }
        }
        #endregion

        #region Plugin Custom Type tests

        protected class CustomPluginType
        {
            public int intField;
            public byte byteField;
            public string stringField;

            public CustomPluginType()
            {
            }

            public CustomPluginType(byte[] bytes)
            {
                using (var s = new MemoryStream(bytes))
                using (var br = new BinaryReader(s))
                {
                    this.intField = br.ReadInt32();
                    this.byteField = br.ReadByte();
                    this.stringField = br.ReadString();
                }
            }

            public byte[] Serialize()
            {
                using (var s = new MemoryStream())
                using (var bw = new BinaryWriter(s))
                {
                    bw.Write(this.intField);
                    bw.Write(this.byteField);
                    bw.Write(this.stringField);

                    return s.ToArray();
                }
            }
            static public byte[] Serialize(object o)
            {
                return ((CustomPluginType) o).Serialize();
            }

            static public CustomPluginType Deserialize(byte[] data)
            {
                return new CustomPluginType(data);
            }
        }

        [Test]
        public void PluginCustomTypeTest()
        {
            UnifiedTestClient client = null;
            const string GameName = "CustomType";

            try
            {
                client = this.CreateMasterClientAndAuthenticate("User1");

                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.EmptyRoomTTL, 0},
                        {ParameterCode.Plugins, new string[]{"CustomTypeCheckPlugin"}},
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);

                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);

                client.SendRequestAndWaitForResponse(request);

                client.CheckThereIsEvent(EventCode.Join);

                var customObj = new CustomPluginType
                {
                    byteField = 1,
                    intField = 2,
                    stringField = "3",
                };
                request = new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Code, (byte) 1},
                        {ParameterCode.Data, new Hashtable{{0, customObj}}},// expected in cache
                    }
                };

                client.SendRequest(request);

                var ev = client.WaitForEvent();
                Assert.AreEqual(123, ev.Code);

                customObj = (CustomPluginType)ev[0];
                Assert.AreEqual(2, customObj.byteField);
                Assert.AreEqual(3, customObj.intField);
                Assert.AreEqual("4", customObj.stringField);
            }
            finally
            {
                DisposeClient(client);
            }
        }
        #endregion

        #region Sync/Assync Http Request test

        [TestCase("NewHttp")]
        [TestCase("OldHttp")]
        public void SyncAsyncHttpRequestTest(string httpVersion)
        {
            this.SyncAsyncHttpRequestTestBody(httpVersion);
        }

        protected virtual void SyncAsyncHttpRequestTestBody(string httpVersion)
        {
            UnifiedTestClient client = null;
            const string GameName = "SyncAsyncHttpRequestTest";

            try
            {
                client = this.CreateMasterClientAndAuthenticate("User1");

                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.EmptyRoomTTL, 0},
                        {ParameterCode.Plugins, new string[]{httpVersion == "NewHttp" ? "SyncAsyncHttpTestPlugin" : "SyncAsyncHttpTestPluginOldHttp"}},
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);

                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);

                client.SendRequestAndWaitForResponse(request);

                client.CheckThereIsEvent(EventCode.Join);

                // we send event which will be handled in sync http request
                var reRequest = new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Code, (byte) 0},
                        {ParameterCode.Data, new Hashtable{{0, 1}}},// expected in cache
                    }
                };

                client.SendRequest(reRequest);

                reRequest.Parameters[ParameterCode.Code] = (byte)3;
                client.SendRequest(reRequest);

                // we get response for first raise event request
                var ev = client.WaitForEvent();
                Assert.AreEqual(0, ev.Code);

                // and now for second raise event request
                ev = client.WaitForEvent();
                Assert.AreEqual(3, ev.Code);

                reRequest.Parameters[ParameterCode.Code] = (byte)1;
                client.SendRequest(reRequest);

                reRequest.Parameters[ParameterCode.Code] = (byte)3;
                client.SendRequest(reRequest);


                // we get response for first raise event request
                ev = client.WaitForEvent();
                Assert.AreEqual(3, ev.Code);

                // and now for second raise event request
                ev = client.WaitForEvent();
                Assert.AreEqual(1, ev.Code);
            }
            finally
            {
                DisposeClient(client);
            }
        }

        [Test]
        public void DeferringFromOnCreate()
        {
            this.DeferringFromOnCreate("FirstCase");
        }

        [Test]
        public void DeferringOnCreateFromHttpCallback()
        {
            this.DeferringFromOnCreate("SecondCase");
        }

        protected virtual void DeferringFromOnCreate(string testCase)
        {
            UnifiedTestClient client = null;
            var GameName = testCase;

            try
            {
                client = this.CreateMasterClientAndAuthenticate("User1");

                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.EmptyRoomTTL, 0},
                        {ParameterCode.Plugins, new string[]{"SyncAsyncHttpTestPlugin"}},// this test for new http only
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);

                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);

                client.SendRequestAndWaitForResponse(request, (short)ErrorCode.PluginReportedError);
            }
            finally
            {
                DisposeClient(client);
            }
        }

        [Test]
        public void HttpRequestNullCallInfoTest()
        {
            HttpRequestNullCallInfoTestBody();
        }

        protected virtual void HttpRequestNullCallInfoTestBody()
        {
            UnifiedTestClient client = null;
            string GameName = MethodBase.GetCurrentMethod().Name;

            try
            {
                client = this.CreateMasterClientAndAuthenticate("User1");

                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.EmptyRoomTTL, 0},
                        {ParameterCode.Plugins, new string[]{"HttpRequestNullCallInfoPlugin" }},
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);

                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);

                client.SendRequestAndWaitForResponse(request);

                client.CheckThereIsEvent(EventCode.Join);

                // Raise 0 event - all good
                var reRequest = new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Code, (byte) 0},
                        {ParameterCode.Data, new Hashtable{{0, 1}}},// expected in cache
                        {ParameterCode.ReceiverGroup, (byte) 1 }
                    }
                };

                client.SendRequest(reRequest);

                var ev = client.WaitForEvent();
                Assert.AreEqual(0, ev.Code);

                // raise 1 event - forgot to process callInfo
                reRequest.Parameters[ParameterCode.Code] = (byte)1;
                client.SendRequest(reRequest);

                client.CheckThereIsErrorInfoEvent();

                // raise 2 event - passing null info to sync request
                reRequest.Parameters[ParameterCode.Code] = (byte)2;
                client.SendRequest(reRequest);

                client.CheckThereIsErrorInfoEvent();

            }
            finally
            {
                DisposeClient(client);
            }
        }
        #endregion

        #region Custom Type mapper

        [Test]
        public void CustomTypeMapperPluginTest()
        {
            UnifiedTestClient client = null;
            const string GameName = "CustomTypeMapperPlugin";

            try
            {
                client = this.CreateMasterClientAndAuthenticate("User1");

                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.EmptyRoomTTL, 0},
                        {ParameterCode.Plugins, new string[]{"CustomTypeMapperPlugin"}},
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);

                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);

                client.SendRequestAndWaitForResponse(request);

                client.CheckThereIsEvent(EventCode.Join);

                const string json = "{\"array\":[1,2,3],\"null\":null,\"boolean\":true,\"number\":123,\"string\":\"Hello World\"}";
                //"{\"array\":[null, \"hause\",1,2,3],\"null\":null,\"boolean\":true,\"number\":123,\"object\":{\"a\":\"b\",\"c\":\"d\",\"e\":\"f\"},\"string\":\"Hello World\"}";

                // we send event which will be handled in sync http request
                var reRequest = new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Code, (byte) 0},
                        {ParameterCode.Data, new Hashtable{{0, json}}},// expected in cache
                    }
                };

                client.SendRequest(reRequest);

                // we get response for first raise event request
                var ev = client.WaitForEvent();
                Assert.AreEqual(123, ev.Code);

                var data = (Dictionary<string, object>)ev.Parameters[1];

                Assert.IsNull(data["null"]);
                Assert.IsTrue((bool) data["boolean"]);
                Assert.AreEqual("Hello World", data["string"]);
                Assert.AreEqual(123, data["number"]);
                Assert.AreEqual(new object[]{1, 2, 3},data["array"]);
            }
            finally
            {
                DisposeClient(client);
            }
        }
        #endregion

        #region Strict mode test
        [TestCase("NewHttp")]
        [TestCase("OldHttp")]
        public void RaiseEventStrictModeTest(string httpVersion)
        {
            UnifiedTestClient client = null;
            const string GameName = "RaiseEventStrictModeTest";

            try
            {
                client = this.CreateMasterClientAndAuthenticate("User1");

                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.EmptyRoomTTL, 0},
                        {ParameterCode.Plugins, new string[]{httpVersion == "NewHttp" ? "StrictModeFailurePlugin" : "StrictModeFailurePluginOldHttp"}},
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);

                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);

                client.SendRequestAndWaitForResponse(request);

                client.CheckThereIsEvent(EventCode.Join);

                request = new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Code, (byte) 0},
                        {ParameterCode.Cache, (byte) EventCaching.AddToRoomCache},
                        {ParameterCode.Data, new Hashtable{{0, 1}}},// expected in cache
                    }
                };

                client.SendRequest(request);

                client.CheckThereIsNoErrorInfoEvent();

                request = new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Code, (byte) 1},
                        {ParameterCode.Cache, (byte) EventCaching.SliceIncreaseIndex},
                        {ParameterCode.Data, new Hashtable{{0, 2}}},// expected сaches count
                    }
                };

                client.SendRequest(request);

                client.CheckThereIsNoErrorInfoEvent();

                request = new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Code, (byte) 2},
                        {ParameterCode.Cache, (byte) EventCaching.AddToRoomCache},
                        {ParameterCode.Data, new Hashtable{{0, 1}}},// expected in cache
                    }
                };

                client.SendRequest(request);

                request = new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Code, (byte) 3},
                        {ParameterCode.Cache, (byte) EventCaching.AddToRoomCache},
                        {ParameterCode.Data, new Hashtable{{0, 2}}},// expected in cache
                    }
                };

                client.SendRequestAndWaitForResponse(request, (short)ErrorCode.PluginReportedError);

                client.CheckThereIsErrorInfoEvent();

                request = new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Code, (byte) 4},
                        {ParameterCode.Cache, (byte) EventCaching.SlicePurgeIndex},
                        {ParameterCode.CacheSliceIndex, 0},
                        {ParameterCode.Data, new Hashtable{{0, 1}}},// expected сaches count
                    }
                };

                client.SendRequest(request);

                client.CheckThereIsErrorInfoEvent();

                request = new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Code, (byte) 5},
                        {ParameterCode.Cache, (byte) EventCaching.SlicePurgeIndex},
                        {ParameterCode.CacheSliceIndex, 0},
                        {ParameterCode.Data, new Hashtable{{0, 1}}},// expected сaches count
                    }
                };

                client.SendRequest(request);

                client.CheckThereIsErrorInfoEvent();
            }
            finally
            {
                DisposeClient(client);
            }
        }

        [TestCase("NewHttp")]
        [TestCase("OldHttp")]
        public void BeforeSetPropertiesStrictModeTest(string httpVersion)
        {
            BeforeSetPropertiesStrictModeTestBody(httpVersion);
        }

        protected virtual void BeforeSetPropertiesStrictModeTestBody(string httpVersion)
        {
            UnifiedTestClient client = null;
            const string GameName = "SetPropertiesStrictMode";

            try
            {
                client = this.CreateMasterClientAndAuthenticate("User1");

                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.EmptyRoomTTL, 0},
                        {ParameterCode.Plugins, new string[]{httpVersion == "NewHttp" ? "StrictModeFailurePlugin" : "StrictModeFailurePluginOldHttp"}},
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);

                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);

                client.SendRequestAndWaitForResponse(request);

                client.CheckThereIsEvent(EventCode.Join);

                request = new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.ActorNr, 0},
                        {ParameterCode.Properties, new Hashtable()}
                    }
                };

                client.SendRequest(request);

                client.CheckThereIsNoErrorInfoEvent();

                request = new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.ActorNr, 1},
                        {ParameterCode.Properties, new Hashtable()}
                    }
                };
                client.SendRequest(request);

                client.CheckThereIsNoErrorInfoEvent();

                request = new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Code, 2},
                        {ParameterCode.Properties, new Hashtable()}
                    }
                };
                client.SendRequest(request);

                request = new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.ActorNr, 3},
                        {ParameterCode.Properties, new Hashtable()}
                    }
                };
                client.SendRequest(request);

                client.CheckThereIsErrorInfoEvent();

                request = new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.ActorNr, 4},
                        {ParameterCode.Properties, new Hashtable()}
                    }
                };
                client.SendRequestAndWaitForResponse(request);

                client.CheckThereIsErrorInfoEvent();
            }
            finally
            {
                DisposeClient(client);
            }
        }

        [Test]
        public void OnSetPropertiesForgotCall()
        {
            UnifiedTestClient client = null;

            const string gameName = "OnSetPropertiesForgotCall";
            try
            {
                client = this.CreateMasterClientAndAuthenticate("User1");

                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, gameName},
                        {ParameterCode.EmptyRoomTTL, 0},
                        {ParameterCode.Plugins, new string[]{"StrictModeFailurePlugin"}},// no http used in this test
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);

                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);

                client.SendRequestAndWaitForResponse(request);

                client.CheckThereIsEvent(EventCode.Join);

                request = new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.ActorNr, 0},
                        {ParameterCode.Properties, new Hashtable()}
                    }
                };

                client.SendRequest(request);

                client.CheckThereIsErrorInfoEvent();
            }
            finally
            {
                DisposeClient(client);
            }
        }

        [TestCase("BeforeJoinForgotCall")]
        [TestCase("OnJoinForgotCall")]
        public void OnJoinStrictModeFail(string gameName)
        {
            UnifiedTestClient client = null;
            UnifiedTestClient client2 = null;

            try
            {
                client = this.CreateMasterClientAndAuthenticate("User1");

                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, gameName},
                        {ParameterCode.EmptyRoomTTL, 0},
                        {ParameterCode.Plugins, new string[] {"StrictModeFailurePlugin"}},// here we do not use http
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);

                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);

                client.SendRequestAndWaitForResponse(request);

                client.CheckThereIsEvent(EventCode.Join);

                Thread.Sleep(100);
                client2 = this.CreateMasterClientAndAuthenticate("User2");

                request = new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, gameName},
                    }
                };

                response = client2.SendRequestAndWaitForResponse(request);

                this.ConnectAndAuthenticate(client2, (string)response[ParameterCode.Address], client2.UserId);

                client2.SendRequest(request);

                Thread.Sleep(100);
                CheckErrorEvent(client, gameName);
            }
            finally
            {
                DisposeClient(client);
                DisposeClient(client2);
            }
        }

        [Test]
        public void OnLeaveForgotCall()
        {
            UnifiedTestClient client = null;
            UnifiedTestClient client2 = null;
            const string gameName = "OnLeaveForgotCall";
            try
            {
                client = this.CreateMasterClientAndAuthenticate("User1");

                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, gameName},
                        {ParameterCode.EmptyRoomTTL, 0},
                        {ParameterCode.Plugins, new string[] {"StrictModeFailurePlugin"}},// in this test we do not use http
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);

                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);

                client.SendRequestAndWaitForResponse(request);

                client.CheckThereIsEvent(EventCode.Join);

                Thread.Sleep(100);
                client2 = this.CreateMasterClientAndAuthenticate("User2");

                request = new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, gameName},
                    }
                };

                response = client2.SendRequestAndWaitForResponse(request);

                this.ConnectAndAuthenticate(client2, (string)response[ParameterCode.Address], client2.UserId);

                client2.SendRequest(request);

                Thread.Sleep(100);
                client.CheckThereIsNoErrorInfoEvent();

                client2.Disconnect();
                Thread.Sleep(100);
                client.CheckThereIsErrorInfoEvent();
            }
            finally
            {
                DisposeClient(client);
                DisposeClient(client2);
            }
        }

        //[Test]
        //public void OnCreateForgotCall()
        //{
        //    TestClientBase client = null;
        //    TestClientBase client2 = null;
        //    const string gameName = "OnCreateForgotCall";
        //    try
        //    {
        //        client = this.CreateMasterClientAndAuthenticate("User1");

        //        var request = new OperationRequest
        //        {
        //            OperationCode = OperationCode.CreateGame,
        //            Parameters = new Dictionary<byte, object>
        //            {
        //                {ParameterCode.RoomName, gameName},
        //                {ParameterCode.EmptyRoomTTL, 0},
        //                {ParameterCode.Plugins, new string[] {"StrictModeFailurePlugin"}},
        //            }
        //        };

        //        var response = client.SendRequestAndWaitForResponse(request);

        //        this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);

        //        client.SendRequestAndWaitForResponse(request);

        //        client.CheckThereIsEvent(EventCode.Join);

        //    }
        //    finally
        //    {
        //        DisposeClient(client);
        //    }
        //}

        #endregion

        #region Callback Exceptions test
        [TestCase("NewHttp")]
        [TestCase("OldHttp")]
        public void HttpAndTimerCallbackExceptionTest(string httpVersion)
        {
            UnifiedTestClient client = null;
            const string GameName = "HttpAndTimerCallbackExceptionTest";

            try
            {
                client = this.CreateMasterClientAndAuthenticate("User1");

                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.EmptyRoomTTL, 0},
                        {ParameterCode.Plugins, new string[]{httpVersion == "NewHttp" ? "StrictModeFailurePlugin" : "StrictModeFailurePluginOldHttp"}},
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);

                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);

                client.SendRequestAndWaitForResponse(request);

                client.CheckThereIsEvent(EventCode.Join);

                request = new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Code, (byte) 5},
                        {ParameterCode.Cache, (byte) EventCaching.AddToRoomCache},
                        {ParameterCode.Data, new Hashtable{{0, 1}}},// expected in cache
                    }
                };

                client.SendRequest(request);

                client.CheckThereIsErrorInfoEvent(this.WaitTimeout);

                request = new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Code, (byte) 6},
                        {ParameterCode.Cache, (byte) EventCaching.SliceIncreaseIndex},
                        {ParameterCode.Data, new Hashtable{{0, 2}}},// expected сaches count
                    }
                };

                client.SendRequestAndWaitForResponse(request, (short)ErrorCode.PluginReportedError);

                client.CheckThereIsErrorInfoEvent(this.WaitTimeout);

                request = new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Code, (byte) 7},
                        {ParameterCode.Cache, (byte) EventCaching.AddToRoomCache},
                        {ParameterCode.Data, new Hashtable{{0, 1}}},// expected in cache
                    }
                };

                client.SendRequest(request);
                client.CheckThereIsErrorInfoEvent(this.WaitTimeout);

                request = new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Code, (byte) 8},
                        {ParameterCode.Cache, (byte) EventCaching.AddToRoomCache},
                        {ParameterCode.Data, new Hashtable{{0, 2}}},// expected in cache
                    }
                };

                client.SendRequest(request);
                client.CheckThereIsErrorInfoEvent(this.WaitTimeout);
            }
            finally
            {
                DisposeClient(client);
            }
        }

        [Test]
        public void WrongUrlExceptionTest()
        {
            UnifiedTestClient client = null;
            const string GameName = "WrongUrlExceptionTest";

            try
            {
                client = this.CreateMasterClientAndAuthenticate("User1");

                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.EmptyRoomTTL, 0},
                        {ParameterCode.Plugins, new string[]{"WrongUrlTestPlugin"}},
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);

                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);

                client.SendRequestAndWaitForResponse(request);

                client.CheckThereIsEvent(EventCode.Join);

                request = new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Code, (byte) 5},
                        {ParameterCode.Cache, (byte) EventCaching.AddToRoomCache},
                        {ParameterCode.Data, new Hashtable{{0, 1}}},// expected in cache
                    }
                };

                client.SendRequest(request);

                client.CheckThereIsErrorInfoEvent();
            }
            finally
            {
                DisposeClient(client);
            }
        }

        #endregion

        #region ErrorPlugin test
        [Test]
        public void ErrorPluginTest()
        {
            UnifiedTestClient client = null;
            UnifiedTestClient client2 = null;
            var GameName = MethodBase.GetCurrentMethod().Name;

            try
            {
                client = this.CreateMasterClientAndAuthenticate("User1");

                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.EmptyRoomTTL, 0},
                        {ParameterCode.Plugins, new string[]{"ErrorPlugin"}},
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);
                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);
                client.SendRequestAndWaitForResponse(request, (short)ErrorCode.PluginReportedError);

                Thread.Sleep(200);

                client2 = this.CreateMasterClientAndAuthenticate("User2");

                request = new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.PlayerProperties, new Hashtable {{(byte)255, "ErrorPlugin"}}},
                    },
                };

                client2.SendRequestAndWaitForResponse(request, (short)ErrorCode.GameIdNotExists);
            }
            finally
            {
                DisposeClient(client);
                DisposeClient(client2);
            }
        }

        [TestCase("NullRefPlugin")]
        [TestCase("ExceptionPlugin")]
        public void FailedToCreatePluginTest(string pluginName)
        {
            UnifiedTestClient client = null;
            UnifiedTestClient client2 = null;
            var GameName = MethodBase.GetCurrentMethod().Name;
            try
            {
                client = this.CreateMasterClientAndAuthenticate("User1");

                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.EmptyRoomTTL, 0},
                        {ParameterCode.Plugins, new string[]{pluginName}},
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);
                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);
                client.SendRequestAndWaitForResponse(request, (short)ErrorCode.PluginMismatch);

                Thread.Sleep(200);

                client2 = this.CreateMasterClientAndAuthenticate("User2");

                request = new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.PlayerProperties, new Hashtable {{(byte)255, pluginName}}},
                    },
                };

                client2.SendRequestAndWaitForResponse(request, (short)ErrorCode.GameIdNotExists);
            }
            finally
            {
                DisposeClient(client);
                DisposeClient(client2);
            }
        }

        #endregion

        #region CacheOps

        [Test]
        public void PluginCacheOpsTest()
        {
            this.PluginCacheOpsTestBody();
        }

        protected virtual void PluginCacheOpsTestBody()
        {
            UnifiedTestClient client = null;
            UnifiedTestClient client2 = null;
            UnifiedTestClient client3 = null;
            var GameName = MethodBase.GetCurrentMethod().Name;

            const byte AddEventFromServer = 1;
            const byte AddEventFromThisActor = 2;
            const byte RemoveEventsForActorsLeft = 3;
            const byte RemoveEventsForActor1 = 4;
            const byte RemoveAllEvents = 5;
            try
            {
                client = this.CreateMasterClientAndAuthenticate("User1");

                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.EmptyRoomTTL, 0},
                        {ParameterCode.Plugins, new string[] {"CacheOpPlugin"}},
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);
                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address]);
                client.SendRequestAndWaitForResponse(request);

                // send operations to setup cache
                SendRaiseEvent(client, AddEventFromServer);

                client.CheckThereIsNoErrorInfoEvent(1000);


                SendRaiseEvent(client, AddEventFromThisActor);

                client.CheckThereIsNoErrorInfoEvent(1000);

                //client 2 joins
                client2 = this.CreateMasterClientAndAuthenticate("User2");

                request = new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.PlayerProperties, new Hashtable {{(byte)255, "CacheOpPlugin" } }},
                    },
                };

                response = client2.SendRequestAndWaitForResponse(request);
                this.ConnectAndAuthenticate(client2, (string)response[ParameterCode.Address]);
                client2.SendRequestAndWaitForResponse(request);

                CheckEventsCount(client2, 3);

                SendRaiseEvent(client2, AddEventFromThisActor);

                client2.CheckThereIsNoErrorInfoEvent(1000);

                // client3 joins
                client3 = this.CreateMasterClientAndAuthenticate("User3");

                request = new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.PlayerProperties, new Hashtable {{(byte)255, "CacheOpPlugin" } }},
                    },
                };

                response = client3.SendRequestAndWaitForResponse(request);
                this.ConnectAndAuthenticate(client3, (string)response[ParameterCode.Address]);
                client3.SendRequestAndWaitForResponse(request);

                CheckEventsCount(client3, 4);

                client2.Disconnect();
                client3.Disconnect();

                SendRaiseEvent(client, RemoveEventsForActorsLeft);

                this.ConnectAndAuthenticate(client2, (string)response[ParameterCode.Address]);
                client2.SendRequestAndWaitForResponse(request);

                CheckEventsCount(client2, 3);

                client2.Disconnect();

                SendRaiseEvent(client, RemoveEventsForActor1);

                this.ConnectAndAuthenticate(client2, (string)response[ParameterCode.Address]);
                client2.SendRequestAndWaitForResponse(request);

                CheckEventsCount(client2, 2);

                client2.Disconnect();

                SendRaiseEvent(client, RemoveAllEvents);

                this.ConnectAndAuthenticate(client2, (string)response[ParameterCode.Address]);
                client2.SendRequestAndWaitForResponse(request);

                CheckEventsCount(client2, 1);


            }
            finally
            {
                DisposeClient(client);
                DisposeClient(client2);
                DisposeClient(client3);
            }
        }

        private static void SendRaiseEvent(UnifiedTestClient client, byte code, Hashtable data = null)
        {
            client.SendRequest(new OperationRequest
            {
                OperationCode = OperationCode.RaiseEvent,
                Parameters = new Dictionary<byte, object>
                {
                    {ParameterCode.Code, code},
                    {ParameterCode.Data, data }
                }
            });
        }

        private static void CheckEventsCount(UnifiedTestClient client, int expected)
        {
            var count = 0;
            try
            {
                while (true)
                {
                    client.WaitForEvent(500);
                    ++count;
                }
            }
            catch (TimeoutException)
            {
            }

            Assert.That(count, Is.EqualTo(expected));
        }

        #endregion

        #region Misc tests

        [Test]
        public void CodemastersRemoveInOnLeaveTest()
        {
            UnifiedTestClient client = null;
            const string GameName = "CodemastersRemoveInOnLeaveTest";

            const int TTL = 15000;
            try
            {
                client = this.CreateMasterClientAndAuthenticate("User1");

                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.EmptyRoomTTL, 30000},
                        {ParameterCode.PlayerTTL, TTL},
                        {ParameterCode.Plugins, new string[] {"RemovingActorPlugin"}},
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);
                this.ConnectAndAuthenticate(client, (string) response[ParameterCode.Address], client.UserId);
                client.SendRequestAndWaitForResponse(request);
                client.CheckThereIsEvent(EventCode.Join);


                request = new OperationRequest
                {
                    OperationCode = OperationCode.Leave,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.IsInactive, true}
                    }
                };
                client.SendRequest(request);

                Thread.Sleep(TTL + 5000);
            }
            finally
            {
                DisposeClient(client);
            }
        }

        [Test]
        public void BroadcastEventToNonExistingUser()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;
            UnifiedTestClient client3 = null;
            var GameName = MethodBase.GetCurrentMethod().Name;

            try
            {
                client1 = this.CreateMasterClientAndAuthenticate("Player1");
                client2 = this.CreateMasterClientAndAuthenticate("Player2");
                client3 = this.CreateMasterClientAndAuthenticate("Player3");


                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.PlayerTTL, 10000},
                        {ParameterCode.Plugins, new string[] {"BroadcastEventPlugin"}},
                    }
                };

                var response = client1.SendRequestAndWaitForResponse(request);
                this.ConnectAndAuthenticate(client1, (string)response[ParameterCode.Address], client1.UserId);
                client1.SendRequestAndWaitForResponse(request);
                client1.CheckThereIsEvent(EventCode.Join);

                Thread.Sleep(100);
                request = new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                    }
                };

                this.JoinGame(client2, request);
                this.JoinGame(client3, request);


                request = new OperationRequest
                {
                    OperationCode = OperationCode.Leave,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.IsInactive, true}
                    }
                };
                client3.SendRequest(request);

                request = new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Code, (byte)1},
                    }
                };
                client1.SendRequest(request);

                client2.WaitForEvent((byte) 1);

                request = new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Code, (byte)2},
                    }
                };
                client1.SendRequest(request);

                client2.WaitForEvent((byte)1);
            }
            finally
            {
                DisposeClients(client1, client2, client3);
            }
        }

        [Test]
        public void SameInstaceOfPluginTest()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;
            var GameName = MethodBase.GetCurrentMethod().Name;
            var GameName2 = MethodBase.GetCurrentMethod().Name + 2;

            try
            {
                client1 = this.CreateMasterClientAndAuthenticate("Player1");
                client2 = this.CreateMasterClientAndAuthenticate("Player2");


                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.PlayerTTL, 10000},
                        {ParameterCode.Plugins, new string[] {"SameInstancePlugin"}},
                    }
                };

                var response = client1.SendRequestAndWaitForResponse(request);
                this.ConnectAndAuthenticate(client1, (string)response[ParameterCode.Address], client1.UserId);
                client1.SendRequestAndWaitForResponse(request);
                client1.CheckThereIsEvent(EventCode.Join);

                Thread.Sleep(100);
                request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName2},
                        {ParameterCode.PlayerTTL, 10000},
                        {ParameterCode.Plugins, new string[] {"SameInstancePlugin"}},
                    }
                };
//                response = client2.SendRequestAndWaitForResponse(request);
                this.ConnectAndAuthenticate(client2, (string)response[ParameterCode.Address]);
                client2.SendRequestAndWaitForResponse(request, (short)ErrorCode.PluginMismatch);

            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [TestCase("CallTimerBeforeContinue")]
        [TestCase("CallTimerAfterContinue")]
        [TestCase("CallTimerFromHttpCallbackSyncHttp")]
        public void OneTimeTimerTests(string config)
        {
            OnTimerTimerTestsBody(config);
        }

        protected virtual void OnTimerTimerTestsBody(string config)
        {
            UnifiedTestClient client = null;
            UnifiedTestClient client2 = null;
            var GameName = MethodBase.GetCurrentMethod().Name;

            try
            {

                client = this.CreateMasterClientAndAuthenticate("User1");

                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.EmptyRoomTTL, 0},
                        {ParameterCode.PlayerTTL, 0},
                        {ParameterCode.CheckUserOnJoin, false},
                        {ParameterCode.CleanupCacheOnLeave, false},
                        {ParameterCode.SuppressRoomEvents, false},
                        {ParameterCode.LobbyName, "Default"},
                        {ParameterCode.LobbyType, (byte)0},
                        {
                            ParameterCode.GameProperties, new Hashtable{{"config", config}}
                        },
                        {ParameterCode.Plugins, new string[]{"OneTimeTimerTestPlugin"}},
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);

                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);

                client.SendRequestAndWaitForResponse(request);

                client.CheckThereIsEvent(123, this.WaitTimeout);

                request = new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.GameProperties, new Hashtable()},
                        {ParameterCode.UserId, "User2"},
                        {ParameterCode.LobbyName, "Default"},
                    },
                };

                client2 = this.CreateMasterClientAndAuthenticate("User2");
                response = client2.SendRequestAndWaitForResponse(request);

                this.ConnectAndAuthenticate(client2, (string)response[ParameterCode.Address], client2.UserId);
                client2.SendRequestAndWaitForResponse(request);

                client.CheckThereIsEvent(123, this.WaitTimeout);

                request = new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Code, (byte)1},
                    }
                };

                client.SendRequest(request);

                client.CheckThereIsEvent(123, this.WaitTimeout);

                //client2.SendRequestAndWaitForResponse(new OperationRequest
                //{
                //    OperationCode = OperationCode.Leave,
                //    Parameters = new Dictionary<byte, object> { { ParameterCode.IsInactive, true } }
                //});

                client2.Disconnect();

                client.CheckThereIsNoErrorInfoEvent();

                client.Disconnect();
            }
            finally
            {
                if (client != null && client.Connected)
                {
                    client.Disconnect();
                    client.Dispose();
                }

                if (client2 != null && client2.Connected)
                {
                    client2.Disconnect();
                    client2.Dispose();
                }
            }
        }

        [Test]
        public void OneTimeTimerNullCallInfoTest()
        {
            this.OneTimeTimerNullCallInfoTestBody();
        }

        protected virtual void OneTimeTimerNullCallInfoTestBody()
        {
            UnifiedTestClient client = null;
            string GameName = MethodBase.GetCurrentMethod().Name;

            try
            {
                client = this.CreateMasterClientAndAuthenticate("User1");

                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.EmptyRoomTTL, 0},
                        {ParameterCode.Plugins, new string[]{"OneTimeTimerNullCallInfoPlugin" }},
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);

                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);

                client.SendRequestAndWaitForResponse(request);

                client.CheckThereIsEvent(EventCode.Join);

                // Raise 0 event - all good
                var reRequest = new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Code, (byte) 0},
                        {ParameterCode.Data, new Hashtable{{0, 1}}},// expected in cache
                        {ParameterCode.ReceiverGroup, (byte) 1 }
                    }
                };

                client.SendRequest(reRequest);

                var ev = client.WaitForEvent();
                Assert.AreEqual(0, ev.Code);

                // raise 1 event - forgot to process callInfo
                reRequest.Parameters[ParameterCode.Code] = (byte)1;
                client.SendRequest(reRequest);

                client.CheckThereIsErrorInfoEvent();
            }
            finally
            {
                DisposeClient(client);
            }
        }

        [TestCase("SyncHttp")]
        [TestCase("AsyncHttp")]
        [TestCase("SyncHttpNoCallback")]
        [TestCase("AsyncHttpNoCallback")]
        public void OneTimeTimerOnRaiseEventHttpTests(string config)
        {
            this.OnTimeTimerOnRaiseEventHttpTestsBody(config);
        }

        protected virtual void OnTimeTimerOnRaiseEventHttpTestsBody(string config)
        {
            UnifiedTestClient client = null;
            var GameName = MethodBase.GetCurrentMethod().Name;
            try
            {

                client = this.CreateMasterClientAndAuthenticate("User1");

                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.EmptyRoomTTL, 0},
                        {ParameterCode.PlayerTTL, 0},
                        {ParameterCode.CheckUserOnJoin, false},
                        {ParameterCode.CleanupCacheOnLeave, false},
                        {ParameterCode.SuppressRoomEvents, false},
                        {ParameterCode.LobbyName, "Default"},
                        {ParameterCode.LobbyType, (byte)0},
                        {
                            ParameterCode.GameProperties, new Hashtable{{"config", config}}
                        },
                        {ParameterCode.Plugins, new string[]{"OnRaiseEventTimerTestPlugin"}},
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);

                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);

                client.SendRequestAndWaitForResponse(request);

                request = new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Code, (byte)1},
                    }
                };

                client.SendRequest(request);

                if (config.Contains("NoCallback"))
                {
                    client.CheckThereIsErrorInfoEvent(this.WaitTimeout);
                }
                else
                {
                    client.CheckThereIsEvent(123, this.WaitTimeout);
                    client.CheckThereIsNoErrorInfoEvent();
                }
                client.Disconnect();
            }
            finally
            {
                if (client != null && client.Connected)
                {
                    client.Disconnect();
                    client.Dispose();
                }
            }
        }

        [TestCase("AsyncOldHttp")]
        [TestCase("OldHttp")]
        [TestCase("AsyncHttp")]
        [TestCase("Http")]
        public void ApiConsistenceTest(string gameNameSuffix)
        {
            this.ApiConsistenceTestBody(gameNameSuffix);
        }

        protected virtual void ApiConsistenceTestBody(string gameNameSuffix)
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;
            UnifiedTestClient client3 = null;
            var GameName = RandomizeString(MethodBase.GetCurrentMethod().Name + gameNameSuffix);

            try
            {
                client1 = this.CreateMasterClientAndAuthenticate("Player1");
                client2 = this.CreateMasterClientAndAuthenticate("Player2");
                client3 = this.CreateMasterClientAndAuthenticate("Player3");


                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.Plugins, new string[] {"ApiConsistenceTestPlugin"}},
                    }
                };

                var response = client1.SendRequestAndWaitForResponse(request);
                this.ConnectAndAuthenticate(client1, (string)response[ParameterCode.Address], client1.UserId);
                client1.SendRequestAndWaitForResponse(request);
                client1.CheckThereIsEvent(EventCode.Join);

                Thread.Sleep(100);

                request = new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Code, (byte)1},
                    }
                };
                client1.SendRequest(request);

                var ev = client1.WaitForEvent((byte)1);
                Assert.That(ev[0], Is.Null.Or.Empty);

                request = new OperationRequest
                {
                    OperationCode = OperationCode.RaiseEvent,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Code, (byte)2},
                    }
                };
                client1.SendRequest(request);

                ev = client1.WaitForEvent((byte)1);
                Assert.That(ev[0], Is.Null.Or.Empty);
            }
            finally
            {
                DisposeClients(client1, client2, client3);
            }
        }
        #endregion

        #region Turn Based

        [TestCase("TBWebhooks")]
        [TestCase("TBWebhooksOldHttp")]
        public void tb_CreateSaveLoadCloseGameTest(string plugin)
        {
            this.tb_CreateSaveLoadCloseGameTestBody(plugin);
        }

        protected virtual void tb_CreateSaveLoadCloseGameTestBody(string plugin)
        {
            UnifiedTestClient client = null;
            UnifiedTestClient client2 = null;
            var GameName = RandomizeString(MethodBase.GetCurrentMethod().Name);
            try
            {
                // create game and join players

                client = this.CreateMasterClientAndAuthenticate("User1");

                var request = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.EmptyRoomTTL, 0},
                        {ParameterCode.PlayerTTL, int.MaxValue},
                        {ParameterCode.CheckUserOnJoin, true},
                        {ParameterCode.LobbyName, "Default"},
                        {ParameterCode.LobbyType, (byte)0},
                        {ParameterCode.JoinMode, (byte)JoinMode.CreateIfNotExists},
                        {
                            ParameterCode.GameProperties, new Hashtable
                            {
                                {"config", ""},
                                {GamePropertyKey.PropsListedInLobby, new string[]{"config"}}
                            }
                        },
                        {ParameterCode.Plugins, new string[]{plugin}},
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);

                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);

                client.SendRequestAndWaitForResponse(request);


                request = new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.LobbyName, "Default"},
                    },
                };

                client2 = this.CreateMasterClientAndAuthenticate("User2");
                response = client2.SendRequestAndWaitForResponse(request);

                this.ConnectAndAuthenticate(client2, (string)response[ParameterCode.Address], client2.UserId);
                client2.SendRequestAndWaitForResponse(request);


                //request = new OperationRequest
                //{
                //    OperationCode = OperationCode.RaiseEvent,
                //    Parameters = new Dictionary<byte, object>
                //    {
                //        {ParameterCode.Code, (byte)1},
                //    }
                //};

                //client.SendRequest(request);

                // leave the game
                client2.SendRequestAndWaitForResponse(new OperationRequest
                {
                    OperationCode = OperationCode.Leave,
                    Parameters = new Dictionary<byte, object> { { ParameterCode.IsInactive, true } }
                });

                client.SendRequestAndWaitForResponse(new OperationRequest
                {
                    OperationCode = OperationCode.Leave,
                    Parameters = new Dictionary<byte, object> { { ParameterCode.IsInactive, true } }
                });


                client2.Disconnect();

                client.CheckThereIsNoErrorInfoEvent();

                client.Disconnect();

                Thread.Sleep(1000);

                //TBD: add check that game is in http server storage

                //Load game and Close it

                request = new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, GameName},
                        {ParameterCode.JoinMode, JoinMode.RejoinOnly},
                        {ParameterCode.Plugins, new string[]{plugin}},
                    },
                };

                client = this.CreateMasterClientAndAuthenticate("User1");

                response = client.SendRequestAndWaitForResponse(request);

                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address]);

                client.SendRequestAndWaitForResponse(request);

                client2 = this.CreateMasterClientAndAuthenticate("User2");

                response = client2.SendRequestAndWaitForResponse(request);

                this.ConnectAndAuthenticate(client2, (string)response[ParameterCode.Address]);

                client2.SendRequestAndWaitForResponse(request);

                // leave the game
                client2.SendRequestAndWaitForResponse(new OperationRequest
                {
                    OperationCode = OperationCode.Leave,
                    Parameters = new Dictionary<byte, object> { { ParameterCode.IsInactive, false } }
                });

                client.SendRequestAndWaitForResponse(new OperationRequest
                {
                    OperationCode = OperationCode.Leave,
                    Parameters = new Dictionary<byte, object> { { ParameterCode.IsInactive, false } }
                });


                client2.Disconnect();

                client.CheckThereIsNoErrorInfoEvent();

                client.Disconnect();

                // check that game is removed from http server index
            }
            finally
            {
                if (client != null && client.Connected)
                {
                    client.Disconnect();
                    client.Dispose();
                }

                if (client2 != null && client2.Connected)
                {
                    client2.Disconnect();
                    client2.Dispose();
                }
            }
        }
        #endregion

        #region Helpers
        private static void CheckErrorEvent(UnifiedTestClient client, string gameName)
        {
            if (gameName.EndsWith("Fail") || gameName.EndsWith("ForgotCall"))
            {
                client.CheckThereIsErrorInfoEvent();
                return;
            }
            client.CheckThereIsNoErrorInfoEvent();
        }

        protected static void DisposeClient(UnifiedTestClient client)
        {
            if (client == null)
            {
                return;
            }

            if (client.Connected)
            {
                client.Disconnect();
            }
            client.Dispose();
        }

        private void JoinGame(UnifiedTestClient client, OperationRequest request)
        {
            var response = client.SendRequestAndWaitForResponse(request);
            this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address]);
            client.SendRequestAndWaitForResponse(request);
            client.CheckThereIsEvent(EventCode.Join);
        }

        protected static string RandomizeString(string str)
        {
            return str + "_" + Guid.NewGuid().ToString().Substring(8);
        }

        #endregion
    }
}
