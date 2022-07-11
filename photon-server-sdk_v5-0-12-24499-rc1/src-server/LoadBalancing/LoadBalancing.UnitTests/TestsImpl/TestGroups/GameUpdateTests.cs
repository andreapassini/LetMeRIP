using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using ExitGames.Client.Photon;
using Photon.Realtime;
using NUnit.Framework;
using Photon.Hive.Operations;
using Photon.Hive.Plugin;
using Photon.LoadBalancing.Operations;
using Photon.LoadBalancing.UnifiedClient;
using Photon.LoadBalancing.UnitTests.UnifiedServer;
using OperationCode = Photon.Realtime.OperationCode;
using ParameterCode = Photon.Realtime.ParameterCode;

namespace Photon.LoadBalancing.UnitTests.UnifiedTests
{
    public abstract partial class LBApiTestsImpl
    {
        #region Games Update Tests

        public enum LobbyFilterContent
        {
            LobbyFilterEmpty,
            LobbyFilterNonEmpty,
            LobbyFilterIsNull,
        }

        /// <summary>
        /// test when we update property not included in lobby properties list.
        ///  we should not get this update in all cases EXCEPT LobbyFilter is NULL
        /// </summary>
        [Test]
        public void GetGameUpdate_NonLobbyPropertyChange(
            [Values(
                LobbyFilterContent.LobbyFilterEmpty, 
                LobbyFilterContent.LobbyFilterNonEmpty,
                LobbyFilterContent.LobbyFilterIsNull)] LobbyFilterContent lobbyFilterCase, 
            [Values(LobbyType.Default)]LobbyType lobbyType)
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;

            var lobbyName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_Lobby");
            var lobbyFilter = SetupLobbyFilter(lobbyFilterCase);
            string propertyName = "NotInLobbyFilterList";
            try
            {
                // authenticate client and check if the Lobbystats event will be received
                // Remarks: The event cannot be checked for a specific lobby count because
                // previous tests may have created lobbies. 
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                if (string.IsNullOrEmpty(this.Player1) || client1.Token == null)
                {
                    Assert.Ignore("This test does not work correctly for old clients without userId and token");
                }

                client1.JoinLobby(lobbyName, (byte)lobbyType);

                // create a new game for the lobby
                var gameName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);
                var createGameResponse = client1.CreateGame(gameName, true, true, 0, new Hashtable() { { "z", "w" } }, lobbyFilter, null);

                this.ConnectAndAuthenticate(client1, createGameResponse.Address, client1.UserId);
                client1.CreateGame(gameName, true, true, 0, new Hashtable() { { "z", "w" } }, lobbyFilter, null);

                // give the game server some time to report the game to the master server
                Thread.Sleep(100);

                var authParameter = new Dictionary<byte, object> { { (byte)Operations.ParameterCode.LobbyStats, true } };
                // check if new game is listed in lobby statistics
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2, authParameter);
                if (this.AuthPolicy == AuthPolicy.UseAuthOnce) // in this case we should send OpSettings to master
                {
                    client2.SendRequest(new OperationRequest
                    {
                        OperationCode = (byte)OperationCode.ServerSettings,
                        Parameters = new Dictionary<byte, object> { { SettingsRequestParameters.LobbyStats, true } },
                    });
                }

                client2.JoinLobby(lobbyName, (byte)lobbyType);

                Thread.Sleep(3000);
                client2.EventQueueClear();

                client1.SendRequest(new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Properties, new Hashtable {{propertyName, "yyy"}}},
                    }
                });

                Thread.Sleep(100);

                if (lobbyFilter == null)
                {
                    client2.CheckThereIsEvent((byte)Events.EventCode.GameListUpdate, this.WaitTimeout);
                }
                else
                {
                    client2.CheckThereIsNoEvent((byte)Events.EventCode.GameListUpdate, this.WaitTimeout);
                }
            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        /// <summary>
        /// test when we update property included in lobby properties list.
        ///  we should get  update
        /// </summary>
        [Test]
        public void GetGameUpdate_LobbyPropertyChange([Values(LobbyType.Default)]LobbyType lobbyType)
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;

            var lobbyFilterCase = LobbyFilterContent.LobbyFilterNonEmpty;
            var lobbyName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_Lobby");
            var lobbyFilter = SetupLobbyFilter(lobbyFilterCase);
            string propertyName = "InLobbyFilterList";
            try
            {
                // authenticate client and check if the Lobbystats event will be received
                // Remarks: The event cannot be checked for a specific lobby count because
                // previous tests may have created lobbies. 
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                if (string.IsNullOrEmpty(this.Player1) || client1.Token == null)
                {
                    Assert.Ignore("This test does not work correctly for old clients without userId and token");
                }

                client1.JoinLobby(lobbyName, (byte)lobbyType);

                // create a new game for the lobby
                var gameName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);
                var createGameResponse = client1.CreateGame(gameName, true, true, 0, new Hashtable() { { "z", "w" } }, lobbyFilter, null);

                this.ConnectAndAuthenticate(client1, createGameResponse.Address, client1.UserId);
                client1.CreateGame(gameName, true, true, 0, new Hashtable() { { "z", "w" } }, lobbyFilter, null);

                // give the game server some time to report the game to the master server
                Thread.Sleep(100);

                var authParameter = new Dictionary<byte, object> { { (byte)Operations.ParameterCode.LobbyStats, true } };
                // check if new game is listed in lobby statistics
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2, authParameter);
                if (this.AuthPolicy == AuthPolicy.UseAuthOnce) // in this case we should send OpSettings to master
                {
                    client2.SendRequest(new OperationRequest
                    {
                        OperationCode = (byte)OperationCode.ServerSettings,
                        Parameters = new Dictionary<byte, object> { { SettingsRequestParameters.LobbyStats, true } },
                    });
                }

                client2.JoinLobby(lobbyName, (byte)lobbyType);

                client1.SendRequest(new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Properties, new Hashtable {{propertyName, "yyy"}}},
                    }
                });

                Thread.Sleep(100);

                client2.CheckThereIsEvent((byte)Events.EventCode.GameListUpdate, this.WaitTimeout);
            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        /// <summary>
        /// test when we remove property included in lobby properties list.
        ///  we should get update
        /// </summary>
        [Test]
        public void GetGameUpdate_LobbyPropertyRemove([Values(LobbyType.Default)]LobbyType lobbyType)
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;

            var lobbyFilterCase = LobbyFilterContent.LobbyFilterNonEmpty;
            var lobbyName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_Lobby");
            var lobbyFilter = SetupLobbyFilter(lobbyFilterCase);
            string propertyName = "InLobbyFilterList";
            try
            {
                // authenticate client and check if the Lobbystats event will be received
                // Remarks: The event cannot be checked for a specific lobby count because
                // previous tests may have created lobbies. 
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                if (string.IsNullOrEmpty(this.Player1) || client1.Token == null)
                {
                    Assert.Ignore("This test does not work correctly for old clients without userId and token");
                }

                client1.JoinLobby(lobbyName, (byte)lobbyType);

                // create a new game for the lobby
                var gameName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);
                var createGameRequest = new CreateGameRequest
                {
                    GameId = gameName,
                    RoomFlags = RoomOptionFlags.DeleteNullProps,
                    GameProperties = new Hashtable()
                    {
                        {"z", "w"},
                        {propertyName, "Value" },
                        {(byte)GameParameter.LobbyProperties, lobbyFilter}
                    }
                };
                var createGameResponse = client1.CreateGame(createGameRequest);

                this.ConnectAndAuthenticate(client1, createGameResponse.Address, client1.UserId);
                client1.CreateGame(createGameRequest);

                // give the game server some time to report the game to the master server
                Thread.Sleep(100);

                var authParameter = new Dictionary<byte, object> { { (byte)Operations.ParameterCode.LobbyStats, true } };
                // check if new game is listed in lobby statistics
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2, authParameter);
                if (this.AuthPolicy == AuthPolicy.UseAuthOnce) // in this case we should send OpSettings to master
                {
                    client2.SendRequest(new OperationRequest
                    {
                        OperationCode = (byte)OperationCode.ServerSettings,
                        Parameters = new Dictionary<byte, object> { { SettingsRequestParameters.LobbyStats, true } },
                    });
                }

                client2.JoinLobby(lobbyName, (byte)lobbyType);

                client1.SendRequest(new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Properties, new Hashtable {{propertyName, "xx"}}},
                    }
                });

                Thread.Sleep(300);

                var gameListUpdate = client2.WaitForEvent((byte)Events.EventCode.GameListUpdate, this.WaitTimeout);

                var gameList = (Hashtable)gameListUpdate.Parameters[(byte)ParameterKey.GameList];
                var propertyList = (Hashtable)gameList[gameName];

                Assert.That(propertyList[propertyName], Is.EqualTo("xx"));

                client1.SendRequest(new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Properties, new Hashtable {{propertyName, null}}},
                    }
                });

                Thread.Sleep(300);

                gameListUpdate = client2.WaitForEvent((byte)Events.EventCode.GameListUpdate, this.WaitTimeout);
                gameList = (Hashtable)gameListUpdate.Parameters[(byte)ParameterKey.GameList];
                propertyList = (Hashtable)gameList[gameName];

                Assert.That(propertyList.Contains(propertyName), Is.False);

            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }


        private static string[] SetupLobbyFilter(LobbyFilterContent lobbyFilterEmpty)
        {
            string[] lobbyFilter = null;
            switch (lobbyFilterEmpty)
            {
                case LobbyFilterContent.LobbyFilterEmpty:
                    lobbyFilter = new string[0];
                    break;
                case LobbyFilterContent.LobbyFilterNonEmpty:
                    lobbyFilter = new[] { "InLobbyFilterList" };
                    break;
            }
            return lobbyFilter;
        }

        /// <summary>
        /// test when we update existing property with same value. In All cases we should NOT get any update
        /// Test check different case when property is in Lobby filter and when not
        /// </summary>
        [Test]
        public void GetGameUpdate_SetSameValue(
            [Values(
                LobbyFilterContent.LobbyFilterEmpty,
                LobbyFilterContent.LobbyFilterNonEmpty,
                LobbyFilterContent.LobbyFilterIsNull)] LobbyFilterContent lobbyFilterCase,
            [Values(
                LobbyType.Default, 
                LobbyType.SqlLobby)]LobbyType lobbyType,
            [Values("NotInLobbyFilterList", "InLobbyFilterList")] string propertyName,
            [Values(
                "yyy",
                new string[] {"x", "y"},
                new byte[] {1, 2, 3})] object propertyValue)
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;

            var lobbyName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_Lobby");
            var lobbyFilter = SetupLobbyFilter(lobbyFilterCase);
            try
            {
                // authenticate client and check if the Lobbystats event will be received
                // Remarks: The event cannot be checked for a specific lobby count because
                // previous tests may have created lobbies. 
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                if (string.IsNullOrEmpty(this.Player1) || client1.Token == null)
                {
                    Assert.Ignore("This test does not work correctly for old clients without userId and token");
                }

                client1.JoinLobby(lobbyName, (byte)lobbyType);

                var roomProperties = new Hashtable()
                {
                    {"z", "w"},
                    {propertyName, propertyValue}
                };
                // create a new game for the lobby
                var gameName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);
                var createGameResponse = client1.CreateGame(gameName, true, true, 0, roomProperties, lobbyFilter, null);

                this.ConnectAndAuthenticate(client1, createGameResponse.Address, client1.UserId);
                client1.CreateGame(gameName, true, true, 0, roomProperties, lobbyFilter, null);

                // give the game server some time to report the game to the master server
                Thread.Sleep(3000);

                var authParameter = new Dictionary<byte, object> { { (byte)Operations.ParameterCode.LobbyStats, true } };
                // check if new game is listed in lobby statistics
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2, authParameter);
                if (this.AuthPolicy == AuthPolicy.UseAuthOnce) // in this case we should send OpSettings to master
                {
                    client2.SendRequest(new OperationRequest
                    {
                        OperationCode = (byte)OperationCode.ServerSettings,
                        Parameters = new Dictionary<byte, object> { { SettingsRequestParameters.LobbyStats, true } },
                    });
                }

                client2.JoinLobby(lobbyName, (byte)lobbyType);

                client1.SendRequest(new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Properties, new Hashtable {{propertyName, propertyValue } }},
                    }
                });

                Thread.Sleep(100);

                client2.CheckThereIsNoEvent((byte)Events.EventCode.GameListUpdate, this.WaitTimeout);
            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [TestCase(LobbyType.Default)]
        public virtual void GetGameUpdate_ExpectedUsersPropertyChange(LobbyType lobbyType)
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;

            var lobbyFilterCase = LobbyFilterContent.LobbyFilterNonEmpty;
            var lobbyName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_Lobby");
            var lobbyFilter = SetupLobbyFilter(lobbyFilterCase);
            try
            {
                // authenticate client and check if the Lobbystats event will be received
                // Remarks: The event cannot be checked for a specific lobby count because
                // previous tests may have created lobbies. 
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                if (string.IsNullOrEmpty(this.Player1) || client1.Token == null)
                {
                    Assert.Ignore("This test does not work correctly for old clients without userId or token");
                }

                client1.JoinLobby(lobbyName, (byte)lobbyType);

                // create a new game for the lobby
                var gameName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);
                var request = new CreateGameRequest
                {
                    AddUsers = new[] {this.Player1},
                    GameId = gameName,
                    LobbyType = (byte)lobbyType,
                    LobbyName = lobbyName,
                };

                var createGameResponse = client1.CreateGame(request);

                this.ConnectAndAuthenticate(client1, createGameResponse.Address, client1.UserId);
                client1.CreateGame(request);

                // give the game server some time to report the game to the master server
                Thread.Sleep(100);

                var authParameter = new Dictionary<byte, object> { { (byte)Operations.ParameterCode.LobbyStats, true } };
                // check if new game is listed in lobby statistics
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2, authParameter);
                if (this.AuthPolicy == AuthPolicy.UseAuthOnce) // in this case we should send OpSettings to master
                {
                    client2.SendRequest(new OperationRequest
                    {
                        OperationCode = (byte)OperationCode.ServerSettings,
                        Parameters = new Dictionary<byte, object> { { SettingsRequestParameters.LobbyStats, true } },
                    });
                }

                client2.JoinLobby(lobbyName, (byte)lobbyType);

                Thread.Sleep(3000);

                client2.EventQueueClear();

                client1.SendRequest(
                        new OperationRequest
                        {
                            OperationCode = OperationCode.SetProperties,

                            Parameters = new Dictionary<byte, object>
                            {
                                {ParameterCode.ExpectedValues, new Hashtable{{(byte) GameParameter.ExpectedUsers, new string[] {this.Player1}}}},
                                {ParameterCode.Properties, new Hashtable{{(byte) GameParameter.ExpectedUsers, new [] {this.Player2}}}}
                            }
                        }
                );

                Thread.Sleep(100);

                client2.CheckThereIsEvent((byte)Events.EventCode.GameListUpdate, this.WaitTimeout);

                client1.SendRequest(
                        new OperationRequest
                        {
                            OperationCode = OperationCode.SetProperties,

                            Parameters = new Dictionary<byte, object>
                            {
                                {ParameterCode.ExpectedValues, new Hashtable{{(byte) GameParameter.ExpectedUsers, new[] {this.Player2}}}},
                                {ParameterCode.Properties, new Hashtable{{(byte) GameParameter.ExpectedUsers, new [] {this.Player2}}}}
                            }
                        }
                );

                Thread.Sleep(100);

                client2.CheckThereIsNoEvent((byte)Events.EventCode.GameListUpdate);

            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [Test]
        public void GetGameUpdate_WellKnownPropertyChange(
            [Values(GameParameter.IsVisible, GameParameter.IsOpen)] GameParameter gameParameter, 
            [Values(LobbyType.Default)]LobbyType lobbyType)
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;

            var lobbyFilterCase = LobbyFilterContent.LobbyFilterNonEmpty;
            var lobbyName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_Lobby");
            var lobbyFilter = SetupLobbyFilter(lobbyFilterCase);
            try
            {
                // authenticate client and check if the Lobbystats event will be received
                // Remarks: The event cannot be checked for a specific lobby count because
                // previous tests may have created lobbies. 
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                if (string.IsNullOrEmpty(this.Player1) || client1.Token == null)
                {
                    Assert.Ignore("This test does not work correctly for old clients without userId and token");
                }

                client1.JoinLobby(lobbyName, (byte)lobbyType);

                // create a new game for the lobby
                var gameName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);
                var createGameResponse = client1.CreateGame(gameName, true, true, 0, new Hashtable() { { "z", "w" } }, lobbyFilter, null);

                this.ConnectAndAuthenticate(client1, createGameResponse.Address, client1.UserId);
                client1.CreateGame(gameName, true, true, 0, new Hashtable() { { "z", "w" } }, lobbyFilter, null);

                // give the game server some time to report the game to the master server
                Thread.Sleep(100);

                var authParameter = new Dictionary<byte, object> { { (byte)Operations.ParameterCode.LobbyStats, true } };
                // check if new game is listed in lobby statistics
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2, authParameter);
                if (this.AuthPolicy == AuthPolicy.UseAuthOnce) // in this case we should send OpSettings to master
                {
                    client2.SendRequest(new OperationRequest
                    {
                        OperationCode = (byte)OperationCode.ServerSettings,
                        Parameters = new Dictionary<byte, object> { { SettingsRequestParameters.LobbyStats, true } },
                    });
                }

                client2.JoinLobby(lobbyName, (byte)lobbyType);
                Thread.Sleep(3000);
                client2.EventQueueClear();

                client1.SendRequest(new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Properties, new Hashtable {{(byte)gameParameter, false}}},
                    }
                });

                client2.CheckThereIsEvent((byte)Events.EventCode.GameListUpdate, this.WaitTimeout);

                // we send same value again
                client1.SendRequest(new OperationRequest
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.Properties, new Hashtable {{ (byte)gameParameter, false}}},
                    }
                });

                client2.CheckThereIsNoEvent((byte)Events.EventCode.GameListUpdate, this.WaitTimeout);

            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }


        [Test]
        public void GetGameUpdate_PluginSetStateOnCreation([Values(LobbyType.Default)]LobbyType lobbyType)
        {
            if (!this.UsePlugins)
            {
                Assert.Ignore("This test needs plugins");
            }

            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;

            var lobbyFilterCase = LobbyFilterContent.LobbyFilterNonEmpty;
            var lobbyName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_Lobby");
            var lobbyFilter = SetupLobbyFilter(lobbyFilterCase);
            try
            {
                // authenticate client and check if the Lobbystats event will be received
                // Remarks: The event cannot be checked for a specific lobby count because
                // previous tests may have created lobbies. 
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                if (string.IsNullOrEmpty(this.Player1) || client1.Token == null)
                {
                    Assert.Ignore("This test does not work correctly for old clients without userId and token");
                }

                client1.JoinLobby(lobbyName, (byte)lobbyType);

                // create a new game for the lobby
                var gameName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);
                var request = new CreateGameRequest
                {
                    GameId = gameName,
                    LobbyType = (byte)lobbyType,
                    LobbyName = lobbyName,
                    Plugins = new [] { "SaveLoadStateTestPlugin" },

                    GameProperties = new Hashtable
                    {
                        { "xxx", 1},
                        { "yyy", 2},
                        {GameParameter.LobbyProperties, new [] {"xxx"} }
                    },
                };

                var createGameResponse = client1.CreateGame(request);

                this.ConnectAndAuthenticate(client1, createGameResponse.Address, client1.UserId);
                client1.CreateGame(request);

                // give the game server some time to report the game to the master server
                Thread.Sleep(100);

                client1.Disconnect();// after disconnect game state is saved

                Thread.Sleep(100);

                var authParameter = new Dictionary<byte, object> { { (byte)Operations.ParameterCode.LobbyStats, true } };
                // check if new game is listed in lobby statistics
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2, authParameter);
                if (this.AuthPolicy == AuthPolicy.UseAuthOnce) // in this case we should send OpSettings to master
                {
                    client2.SendRequest(new OperationRequest
                    {
                        OperationCode = (byte)OperationCode.ServerSettings,
                        Parameters = new Dictionary<byte, object> { { SettingsRequestParameters.LobbyStats, true } },
                    });
                }

                client2.JoinLobby(lobbyName, (byte)lobbyType);
                Thread.Sleep(3000);
                client2.EventQueueClear();


                var joinRequest = new JoinGameRequest
                {
                    GameId = gameName,
                    LobbyType = (byte)lobbyType,
                    LobbyName = lobbyName,
                    Plugins = new string[] { "SaveLoadStateTestPlugin" },
                    JoinMode = (byte)JoinMode.JoinOrRejoin,
                };

                this.ConnectAndAuthenticate(client1, this.MasterAddress);
                var jgResponse = client1.JoinGame(joinRequest);

                this.ConnectAndAuthenticate(client1, jgResponse.Address);
                client1.JoinGame(joinRequest);

                EventData eventData;
                Assert.That(client2.TryWaitForEvent((byte)Events.EventCode.GameListUpdate, this.WaitTimeout, out eventData));

                Assert.That(eventData.Parameters.Count > 0);

                var gameList = (Hashtable) eventData.Parameters[(byte) ParameterKey.GameList];
                var propertyList = (Hashtable) gameList[gameName];

                Assert.That(propertyList["xxx"], Is.EqualTo(1));
            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        /// <summary>
        /// test when we remove property included in lobby properties list.
        ///  we should get update
        /// </summary>
        [Test]
        public void GetGameUpdate_OnGameCreation([Values(LobbyType.Default)]LobbyType lobbyType)
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;

            var lobbyFilterCase = LobbyFilterContent.LobbyFilterNonEmpty;
            var lobbyName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_Lobby");
            var lobbyFilter = SetupLobbyFilter(lobbyFilterCase);
            try
            {
                // authenticate client and check if the Lobbystats event will be received
                // Remarks: The event cannot be checked for a specific lobby count because
                // previous tests may have created lobbies. 
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                if (string.IsNullOrEmpty(this.Player1) || client1.Token == null)
                {
                    Assert.Ignore("This test does not work correctly for old clients without userId and token");
                }

                client1.JoinLobby(lobbyName, (byte)lobbyType);

                var authParameter = new Dictionary<byte, object> { { (byte)Operations.ParameterCode.LobbyStats, true } };
                // check if new game is listed in lobby statistics
                client2 = this.CreateMasterClientAndAuthenticate(this.Player2, authParameter);
                if (this.AuthPolicy == AuthPolicy.UseAuthOnce) // in this case we should send OpSettings to master
                {
                    client2.SendRequest(new OperationRequest
                    {
                        OperationCode = (byte)OperationCode.ServerSettings,
                        Parameters = new Dictionary<byte, object> { { SettingsRequestParameters.LobbyStats, true } },
                    });
                }

                client2.JoinLobby(lobbyName, (byte)lobbyType);

                // create a new game for the lobby
                var gameName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);
                var createGameRequest = new CreateGameRequest
                {
                    GameId = gameName,
                    RoomFlags = RoomOptionFlags.DeleteNullProps,
                };
                var createGameResponse = client1.CreateGame(createGameRequest);

                this.ConnectAndAuthenticate(client1, createGameResponse.Address, client1.UserId);
                client1.CreateGame(createGameRequest);

                // give the game server some time to report the game to the master server
                Thread.Sleep(300);

                var gameListUpdate = client2.WaitForEvent((byte)Events.EventCode.GameListUpdate, this.WaitTimeout);

                var gameList = (Hashtable)gameListUpdate.Parameters[(byte)ParameterKey.GameList];
                Assert.IsTrue(gameList.ContainsKey(gameName));
            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }


        #endregion
    }
}
