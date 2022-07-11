using System.Collections.Generic;
using System.Reflection;
using ExitGames.Client.Photon;
using Photon.LoadBalancing.UnifiedClient;
using Photon.LoadBalancing.UnifiedClient.AuthenticationSchemes;
using Photon.LoadBalancing.UnitTests.UnifiedServer.Policy;
using Photon.Realtime;
using Hashtable = System.Collections.Hashtable;

namespace Photon.LoadBalancing.UnitTests.Offline
{
    using global::LoadBalancing.TestInterfaces;
    using NUnit.Framework;
    using Photon.LoadBalancing.UnitTests.UnifiedServer.OfflineExtra;

    [TestFixture]
    public class PluginOfflineTests : PluginTestsImpl
    {

        public PluginOfflineTests()
            : base(new OfflineConnectPolicy(new TokenAuthenticationScheme()))
        {
            Photon.SocketServer.Protocol.TryRegisterCustomType(typeof(CustomPluginType), 1, 
                CustomPluginType.Serialize, CustomPluginType.Deserialize);
        }

        [TestCase("HttpCallAsyncBeforeContinue")]
        [TestCase("HttpCallAsyncAfterContinue")]
        [TestCase("HttpCallSyncBeforeContinueContinueInCallback")]
        [TestCase("HttpCallSyncAfterContinueContinueInCallback")]
        public void A_Group_PluginHttpTests(string config)
        {
            UnifiedTestClient client = null;
            UnifiedTestClient client2 = null;
            var GameName = RandomizeString(MethodBase.GetCurrentMethod().Name);

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
                        {ParameterCode.Plugins, new string[]{"AllMethosCallHttpTestPlugin"}},
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);

                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);

                client.SendRequestAndWaitForResponse(request);

                client.CheckThereIsEvent(123, this.WaitTimeout);

                client.SendRequestAndWaitForResponse(new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Properties, new Hashtable {{GamePropertyKey.IsOpen, true}}}
                    }
                });

                client.CheckThereIsEvent(123, this.WaitTimeout);
                client.CheckThereIsEvent(123, this.WaitTimeout);

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

                client.CheckThereIsEvent(123, this.WaitTimeout);
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

                client2.SendRequestAndWaitForResponse(new OperationRequest
                {
                    OperationCode = OperationCode.Leave,
                    Parameters = new Dictionary<byte, object> { { ParameterCode.IsInactive, true } }
                });

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

                this.CheckGameIsClosed(GameName, this.WaitTimeout);
            }
        }

        [Test]
        public void A_Group_PluginHttpCallSyncBeforeContinueTests()
        {
            const string config = "HttpCallSyncBeforeContinue";
            UnifiedTestClient client = null;
            UnifiedTestClient client2 = null;
            var GameName = RandomizeString(MethodBase.GetCurrentMethod().Name);

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
                        {ParameterCode.Plugins, new string[]{"AllMethosCallHttpTestPlugin"}},
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);

                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);

                client.SendRequestAndWaitForResponse(request);

                client.CheckThereIsEvent(123, this.WaitTimeout);

                client.SendRequestAndWaitForResponse(new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Properties, new Hashtable {{GamePropertyKey.IsOpen, true}}}
                    }
                }, ErrorCode.PluginReportedError);

                client.CheckThereIsErrorInfoEvent(this.WaitTimeout);
                client.CheckThereIsEvent(123, this.WaitTimeout);

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
                client2.SendRequestAndWaitForResponse(request, ErrorCode.PluginReportedError);

                client.CheckThereIsErrorInfoEvent(this.WaitTimeout);
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

                client.EventQueueClear();

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

                this.CheckGameIsClosed(GameName, this.WaitTimeout);
            }
        }

        [Test]
        public void A_Group_PluginHttpCallSyncAfterContinueTests()
        {
            const string config = "HttpCallSyncAfterContinue";

            UnifiedTestClient client = null;
            UnifiedTestClient client2 = null;
            var GameName = RandomizeString(MethodBase.GetCurrentMethod().Name);

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
                        {ParameterCode.Plugins, new string[]{"AllMethosCallHttpTestPlugin"}},
                    }
                };

                var response = client.SendRequestAndWaitForResponse(request);

                this.ConnectAndAuthenticate(client, (string)response[ParameterCode.Address], client.UserId);

                client.SendRequestAndWaitForResponse(request);

                client.CheckThereIsEvent(123, this.WaitTimeout);

                client.SendRequestAndWaitForResponse(new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Properties, new Hashtable {{GamePropertyKey.IsOpen, true}}}
                    }
                });

                client.CheckThereIsErrorInfoEvent(this.WaitTimeout);
                client.CheckThereIsEvent(123, this.WaitTimeout);

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

                client.CheckThereIsErrorInfoEvent(this.WaitTimeout);
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

                client2.SendRequestAndWaitForResponse(new OperationRequest
                {
                    OperationCode = OperationCode.Leave,
                    Parameters = new Dictionary<byte, object> { { ParameterCode.IsInactive, true } }
                });

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

                this.CheckGameIsClosed(GameName, this.WaitTimeout);
            }
        }

        private void CheckGameIsClosed(string gameName, int timeout)
        {
            var policy = (OfflineConnectPolicy) this.connectPolicy;

            var gameServerApp = policy.GSApplication;

            Assert.IsTrue(((ITestGameServerApplication)gameServerApp).WaitGameDisposed(gameName, timeout));
        }
    }
}
