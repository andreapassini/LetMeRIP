// --------------------------------------------------------------------------------------------------------------------
// <copyright file="GameState.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the GameState type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

using ExitGames.Logging;

using Photon.Hive.Common;
using Photon.Hive.Common.Lobby;
using Photon.Hive.Operations;
using Photon.Hive.Plugin;
using Photon.LoadBalancing.MasterServer.GameServer;
using Photon.LoadBalancing.ServerToServer.Events;
using Photon.SocketServer;
using Photon.SocketServer.Rpc;

namespace Photon.LoadBalancing.MasterServer.Lobby
{
    public struct DeferredUser
    {
        public MasterClientPeer Peer;
        public JoinGameRequest JoinRequest;
    }

    public class GameState
    {
        #region Constants and Fields

        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        public readonly AppLobby Lobby;

        private protected readonly DateTime createDateUtc = DateTime.UtcNow;

        /// <summary>
        ///   Used to track peers which currently are joining the game.
        /// </summary>
        private readonly LinkedList<PeerState> joiningPeers = new LinkedList<PeerState>();

        private readonly List<string> activeUserIdList;
        private readonly List<string> inactiveUserIdList;
        private readonly List<string> expectedUsersList;
        private bool isJoinable;
        private string lastUpdateEvent;

        #endregion

        #region Constants

        public const byte GameId = 0;
        public const byte InactiveCountId = 1;
        public const byte CreateDateId = 2;
        public const byte UserListId = 3;
        public const byte PropertiesId = 4;
        public const byte IsVisibleId = 5;
        public const byte IsOpenId = 6;
        public const byte MaxPlayerId = 7;
        public const byte LobbyNameId = 8;
        public const byte LobbyTypeId = 9;
        public const byte InactiveUsersId = 10;
        public const byte ExcludedUsersId = 11;
        public const byte ExpectedUsersId = 12;

        #endregion

        #region Constructors and Destructors

        /// <summary>
        ///   Initializes a new instance of the <see cref = "GameState" /> class.
        /// </summary>
        /// <param name="lobby">
        /// The lobby to which the game belongs.
        /// </param>
        /// <param name = "id">
        ///   The game id.
        /// </param>
        /// <param name = "maxPlayer">
        ///   The maximum number of player who can join the game.
        /// </param>
        /// <param name = "gsContext">
        ///   The game server peer.
        /// </param>
        public GameState(AppLobby lobby, string id, byte maxPlayer, GameServerContext gsContext)
        {
            this.Lobby = lobby;
            this.Id = id;
            this.MaxPlayer = maxPlayer;
            this.IsOpen = true;
            this.IsVisible = true;
            this.HasBeenCreatedOnGameServer = false;
            this.GameServerPlayerCount = 0;
            this.GameServer = gsContext;
            this.IsJoinable = this.CheckIsGameJoinable();
            this.activeUserIdList = new List<string>(maxPlayer > 0 ? maxPlayer : 5);
            this.inactiveUserIdList = new List<string>(maxPlayer > 0 ? maxPlayer : 5);
            this.expectedUsersList = new List<string>(maxPlayer > 0 ? maxPlayer : 5);
            this.ExcludedActors = new List<ExcludedActorInfo>();
        }

        public GameState(AppLobby lobby, Hashtable data)
        {
            this.Lobby = lobby;
            this.Id = (string)data[GameId];
            this.MaxPlayer = (byte)data[MaxPlayerId];
            this.IsOpen = (bool)data[IsOpenId];
            this.IsVisible = (bool)data[IsVisibleId];

            this.InactivePlayerCount = (int)data[InactiveCountId];
            this.createDateUtc = DateTime.FromBinary((long)data[CreateDateId]);
            this.activeUserIdList = new List<string>((string[])data[UserListId]);
            this.inactiveUserIdList = new List<string>((string[])data[InactiveUsersId]);
            this.expectedUsersList = new List<string>((string[])data[ExpectedUsersId]);
            this.ExcludedActors = new List<ExcludedActorInfo>((ExcludedActorInfo[])data[ExcludedUsersId]);
            this.Properties = (Dictionary<object, object>)data[PropertiesId];

            this.HasBeenCreatedOnGameServer = true;
            this.GameServerPlayerCount = 0;
            this.GameServer = null;
            this.IsJoinable = this.CheckIsGameJoinable();
        }

        #endregion

        #region Properties

        public DateTime CreateDateUtc
        {
            get
            {
                return this.createDateUtc;
            }
        }

        /// <summary>
        ///   Gets the context of the game server on which the game is or should be created.
        /// </summary>
        public GameServerContext GameServer { get; private set; }

        internal class ExpiryInfo
        {
            public GameState Game { get; private set; }
            public DateTime ExpiryStart { get; set; }

            internal ExpiryInfo(GameState game, DateTime time)
            {
                this.Game = game;
                this.ExpiryStart = time;
            }
        };

        internal LinkedListNode<ExpiryInfo> ExpiryListNode { get; set; } 
        /// <summary>
        ///   Gets the number of players who joined the game on the game server.
        /// </summary>
        public int GameServerPlayerCount { get; private set; }

        /// <summary>
        ///   Gets the game id.
        /// </summary>
        public string Id { get; private set; }

        public List<DeferredUser> WaitList { get; } = new List<DeferredUser>();
        public int InactivePlayerCount { get; private set; }

        /// <summary>
        ///   Gets or sets a value indicating whether the game is created on a game server instance.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is created on game server; otherwise, <c>false</c>.
        /// </value>
        public bool HasBeenCreatedOnGameServer { get; set; }

        /// <summary>
        ///   Gets or sets a value indicating whether the game is open for players to join the game.
        /// </summary>
        /// <value><c>true</c> if the game is open; otherwise, <c>false</c>.</value>
        public bool IsOpen { get; set; }

        public bool IsPersistent { get; set; }

        /// <summary>
        ///   Gets a value indicating whether this instance is visble in the lobby.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is visble in lobby; otherwise, <c>false</c>.
        /// </value>
        public bool IsVisbleInLobby
        {
            get
            {
                return this.IsVisible && this.HasBeenCreatedOnGameServer;
            }
        }

        /// <summary>
        ///   Gets or sets a value indicating whether the game should be visible to other players.
        /// </summary>
        /// <value>
        ///   <c>true</c> if the game is visible; otherwise, <c>false</c>.
        /// </value>
        public bool IsVisible { get; set; }

        /// <summary>
        ///   Gets the number of players currently joining the game.
        /// </summary>
        public int JoiningPlayerCount
        {
            get
            {
                return this.joiningPeers.Count;
            }
        }

        /// <summary>
        ///   Gets or sets the maximum number of player for the game.
        /// </summary>
        public byte MaxPlayer { get; set; }
        
        /// <summary>
        ///   Gets the number of players joined the game.
        /// </summary>
        public int PlayerCount
        {
            get
            {
                return this.GameServerPlayerCount + this.InactivePlayerCount + this.JoiningPlayerCount + this.YetExpectedUsersCount;
            }
        }

        public int YetExpectedUsersCount
        {
            get { return this.expectedUsersList.Count(userId => !this.ContainsUser(userId)); }
        }

        public Dictionary<object, object> Properties { get; } = new Dictionary<object, object>();

        public bool IsJoinable
        {
            get => this.isJoinable;

            private set
            {
                if (value != this.isJoinable)
                {
                    this.isJoinable = value;
                    this.Lobby.GameList.OnGameJoinableChanged(this);
                }
            }
        }

        public bool ShouldBePreservedInList => this.IsPersistent && this.InactivePlayerCount > 0 && this.Lobby.LobbyType == AppLobbyType.AsyncRandomLobby;

        public bool CheckUserIdOnJoin { get; private set; }

        public List<ExcludedActorInfo> ExcludedActors { get; }
        public DataContract CreateRequest { get; set; }

        public bool ExpectsReplication { get; set; }

        #endregion

        #region Public Methods

        public void AddPeer(ILobbyPeer peer)
        {
            this.ExpectsReplication = false;

            if (this.ContainsUser(peer.UserId))
            {
                return;
            }

            var peerState = new PeerState(peer);

            this.AddPeerState(peerState, peer);
        }

        public void CheckJoinTimeOuts(DateTime minDateTime)
        {
            if (this.joiningPeers.Count == 0)
            {
                return;
            }

            var oldPlayerCount = this.PlayerCount;

            var node = this.joiningPeers.First;
            while (node != null)
            {
                var peerState = node.Value;
                var nextNode = node.Next;

                if (peerState.UtcCreated < minDateTime)
                {
                    if (log.IsDebugEnabled)
                    {
                        log.DebugFormat("Player removed from joining list by timeout. UserId={0}", peerState.UserId);
                    }
                    this.joiningPeers.Remove(node);

                    if (string.IsNullOrEmpty(peerState.UserId) == false)
                    {
                        if (this.Lobby.Application.PlayerOnlineCache != null)
                        {
                            this.Lobby.Application.PlayerOnlineCache.OnDisconnectFromGameServer(peerState.UserId, this);
                        }
                    }
                }

                node = nextNode;
            }

            if (oldPlayerCount != this.PlayerCount)
            {
                this.Lobby.GameList.OnPlayerCountChanged(this, oldPlayerCount);
                this.UpdateGameServerPlayerCount(oldPlayerCount);
            }

            this.IsJoinable = this.CheckIsGameJoinable();
        }

        public string GetUserListsAsString()
        {
            var sb = new StringBuilder(512);

            sb.Append("ActiveUsers:");
            foreach (var ap in this.activeUserIdList)
            {
                sb.Append(ap).Append(',');
            }
            sb.Append(';');

            sb.Append("ExpectedUsers:");
            foreach (var ep in this.expectedUsersList)
            {
                sb.Append(ep).Append(','); 
            }
            sb.Append(';');

            sb.Append("InactiveUsers:");
            foreach (var p in this.inactiveUserIdList)
            {
                sb.Append(p).Append(',');
            }
            sb.Append(';');

            sb.Append("ExcluedUsers:");
            foreach (var p in this.ExcludedActors)
            {
                sb.Append(p).Append(',');
            }
            sb.Append(';');

            sb.Append("WatingUsers:");
            foreach (var p in this.WaitList)
            {
                sb.Append(p.Peer.UserId).Append(',');
            }
            sb.Append(';');


            return sb.ToString();
        }

        // savedgames-poc:
        public void ResetGameServer()
        {
            this.GameServer = null;
        }

        public string GetServerAddress(ILobbyPeer peer)
        {
            string address;

            Func<GameServerContext, bool> filter = ctx =>
            {
                if (ctx.SupportedProtocols == null)
                {
                    return true;
                }
                return ctx.SupportedProtocols.Contains((byte)peer.NetworkProtocol);
            };


            // savedgames-poc:
            if (this.GameServer == null)
            {
                // try to get a game server instance from the load balancer            

                if (!this.Lobby.Application.LoadBalancer.TryGetServer(out var newGameServerContext, filter))
                {
                    throw new Exception("Failed to get server instance.");
                }
                this.GameServer = newGameServerContext;
                log.DebugFormat("GetServerAddress: game={0} got new host GS={1}", this.Id, this.GameServer.Key);
            }
            else
            {
                if (!filter(this.GameServer))
                {
                    log.WarnFormat("Client tries to join game with protocol {0}, but gs does not support it", peer.NetworkProtocol);
                    throw new NotSupportedException(
                        string.Format("Failed to get GS for game. It is on GS which does not support protcol {0} ",
                            peer.NetworkProtocol));
                }
            }

            var useHostnames = peer.UseHostnames; // || config setting ForceHostnames

            var useIPv4 = peer.LocalIPAddress.AddressFamily == AddressFamily.InterNetwork;
            var addrInfo = this.GameServer.AddressInfo;
            switch (peer.NetworkProtocol)
            {
                case NetworkProtocolType.Udp:
                    address = useHostnames ? addrInfo.UdpHostname : (useIPv4 ? addrInfo.UdpAddress : addrInfo.UdpAddressIPv6);
                    break;
                case NetworkProtocolType.Tcp:
                    address = useHostnames ? addrInfo.TcpHostname : (useIPv4 ? addrInfo.TcpAddress : addrInfo.TcpAddressIPv6);
                    break;
                case NetworkProtocolType.WebSocket:
                    address = useHostnames ? addrInfo.WebSocketHostname : (useIPv4 ? addrInfo.WebSocketAddress : addrInfo.WebSocketAddressIPv6);
                    break;
                case NetworkProtocolType.SecureWebSocket:
                    address = addrInfo.SecureWebSocketHostname;
                    break;
                case NetworkProtocolType.WebRTC:
                    address = addrInfo.WebRTCAddress;
                    break;
                default:
                    throw new NotSupportedException(string.Format("No GS address configured for Protocol {0} (Peer Type: {1})", peer.NetworkProtocol, ((PeerBase)peer).NetworkProtocol));
            }
            if (string.IsNullOrEmpty(address))
            {
                throw new NotSupportedException(
                    string.Format("No GS address configured for Protocol {0} (Peer Type: {1}, AddressFamily: {2})", peer.NetworkProtocol, peer.NetworkProtocol, peer.LocalIPAddress.AddressFamily));
            }
            return address;
        }

        public bool MatchGameProperties(Hashtable matchProperties)
        {
            if (matchProperties == null || matchProperties.Count == 0)
            {
                return true;
            }

            foreach (object key in matchProperties.Keys)
            {
                if (!this.Properties.TryGetValue(key, out var gameProperty))
                {
                    return false;
                }

                if (gameProperty.Equals(matchProperties[key]) == false)
                {
                    return false;
                }
            }

            return true;
        }

        public Hashtable ToHashTable()
        {
            var h = new Hashtable();
            foreach (KeyValuePair<object, object> keyValue in this.Properties)
            {
                h.Add(keyValue.Key, keyValue.Value);
            }

            h[(byte)GameParameter.PlayerCount] = (byte)this.PlayerCount;
            h[(byte)GameParameter.MaxPlayers] = this.MaxPlayer;
            h[(byte)GameParameter.IsOpen] = this.IsOpen;
            h.Remove((byte)GameParameter.IsVisible);

            return h;
        }

        public bool TrySetProperties(Hashtable gameProperties, out bool changed, out string debugMessage)
        {
            changed = false;

            if (!WellKnownProperties.TryGetProperties(gameProperties, out var maxPlayer, out var isOpen, out var isVisible, out debugMessage, null))
            {
                return false;
            }

            if (maxPlayer.HasValue && maxPlayer.Value != this.MaxPlayer)
            {
                this.MaxPlayer = maxPlayer.Value;
                this.Properties[(byte)GameParameter.MaxPlayers] = this.MaxPlayer;
                changed = true;
            }

            if (isOpen.HasValue && isOpen.Value != this.IsOpen)
            {
                this.IsOpen = isOpen.Value;
                this.Properties[(byte)GameParameter.IsOpen] = isOpen.Value;
                changed = true;
            }

            if (isVisible.HasValue && isVisible.Value != this.IsVisible)
            {
                this.IsVisible = isVisible.Value;
                changed = true;
            }

            this.Properties.Clear();
            foreach (DictionaryEntry entry in gameProperties)
            {
                if (entry.Value != null)
                {
                    this.Properties[entry.Key] = entry.Value;
                }
            }

            debugMessage = string.Empty;
            this.IsJoinable = this.CheckIsGameJoinable();
            return true;
        }

        public bool SupportsProtocol(NetworkProtocolType networkProtocol)
        {
            if (this.GameServer == null)
            {
                return false;
            }

            if (this.GameServer.SupportedProtocols == null)
            {
                return true;
            }

            return this.GameServer.SupportedProtocols.Contains((byte)networkProtocol);
        }

        public bool Update(UpdateGameEvent updateEvent)
        {
            this.lastUpdateEvent = Newtonsoft.Json.JsonConvert.SerializeObject(updateEvent);
            var peerCount = this.PlayerCount;
            if (updateEvent.Reinitialize)
            {
                if (log.IsDebugEnabled)
                {
                    if (updateEvent.Replication == 1)
                    {
                        log.DebugFormat("Game is reinitialized. game:{0}", this.Id);
                    }
                    else
                    {
                        log.DebugFormat("Game is replicated. game:{0}", this.Id);
                    }
                }

                this.StateCleanUp();
            }

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Got update game event.content={0}", Newtonsoft.Json.JsonConvert.SerializeObject(updateEvent));
            }

            this.ExpectsReplication = false;

            var changed = false;
            var notifyWaiters = false;

            if (this.HasBeenCreatedOnGameServer == false)
            {
                this.HasBeenCreatedOnGameServer = true;
                changed = true;
                notifyWaiters = true;
                this.GameServer.IncrementGameCreations();
            }

            if (updateEvent.CheckUserIdOnJoin != null)
            {
                this.CheckUserIdOnJoin = updateEvent.CheckUserIdOnJoin.Value;
            }

            if (updateEvent.InactiveCount != this.InactivePlayerCount)
            {
                this.InactivePlayerCount = updateEvent.InactiveCount;
                changed = true;
            }

            if (this.GameServerPlayerCount != updateEvent.ActorCount)
            {
                this.GameServerPlayerCount = updateEvent.ActorCount;
                changed = true;
            }

            if (updateEvent.InactiveUsers != null)
            {
                foreach (var userId in updateEvent.InactiveUsers)
                {
                    this.OnPeerLeftGameOnGameServer(userId, deactivate: true);
                }
            }

            if (updateEvent.NewUsers != null)
            {
                foreach (var userId in updateEvent.NewUsers)
                {
                    this.OnPeerJoinedGameOnGameServer(userId);
                }
            }

            if (updateEvent.RemovedUsers != null)
            {
                foreach (var userId in updateEvent.RemovedUsers)
                {
                    this.OnPeerLeftGameOnGameServer(userId);
                }
            }

            if (updateEvent.FailedToAdd != null)
            {
                foreach (var userId in updateEvent.FailedToAdd)
                {
                    this.OnPeerFailedToJoinOnGameServer(userId);
                }
            }

            if (updateEvent.ExcludedUsers != null)
            {
                this.OnUsersExcluded(updateEvent.ExcludedUsers);
                changed = true;
            }

            if (updateEvent.ExpectedUsers != null)
            {
                changed |= this.OnExpectedListUpdated(updateEvent.ExpectedUsers);
            }

            if (updateEvent.MaxPlayers.HasValue && updateEvent.MaxPlayers.Value != this.MaxPlayer)
            {
                this.MaxPlayer = updateEvent.MaxPlayers.Value;
                this.Properties[(byte)GameParameter.MaxPlayers] = this.MaxPlayer;
                changed = true;
            }

            if (updateEvent.IsOpen.HasValue && updateEvent.IsOpen.Value != this.IsOpen)
            {
                this.IsOpen = updateEvent.IsOpen.Value;
                this.Properties[(byte)GameParameter.IsOpen] = updateEvent.IsOpen.Value;
                changed = true;
            }

            if (updateEvent.IsVisible.HasValue && updateEvent.IsVisible.Value != this.IsVisible)
            {
                this.IsVisible = updateEvent.IsVisible.Value;
                changed = true;
            }

            if (updateEvent.PropertyFilter != null)
            {
                var lobbyProperties = new HashSet<object>(updateEvent.PropertyFilter);

                var keys = new object[this.Properties.Keys.Count];
                this.Properties.Keys.CopyTo(keys, 0);

                foreach (var key in keys)
                {
                    if (lobbyProperties.Contains(key) == false)
                    {
                        this.Properties.Remove(key);
                        changed = true;
                    }
                }

                // add max players even if it's not in the property filter
                // MaxPlayer is always reported to the client and available 
                // for JoinRandom matchmaking
                this.Properties[(byte)GameParameter.MaxPlayers] = this.MaxPlayer;
            }

            if (updateEvent.GameProperties != null)
            {
                changed |= this.UpdateProperties(updateEvent.GameProperties);
            }

            this.IsJoinable = this.CheckIsGameJoinable();

            if (updateEvent.IsPersistent.HasValue && updateEvent.IsPersistent.Value != this.IsPersistent)
            {
                this.IsPersistent = updateEvent.IsPersistent.Value;
                changed = true;
            }

            if (updateEvent.Reinitialize)
            {
                if (log.IsDebugEnabled)
                {
                    if (updateEvent.Replication == 1)
                    {
                        log.DebugFormat("Game reinitialization done. game:{0}", this.Id);
                    }
                    else
                    {
                        log.DebugFormat("Game replication done. game:{0}", this.Id);
                    }
                }
            }

            if (peerCount != this.PlayerCount)
            {
                this.Lobby.GameList.OnPlayerCountChanged(this, peerCount);
                this.UpdateGameServerPlayerCount(peerCount);
            }

            // we do notification after updating to correctly calculate players count
            if (notifyWaiters)
            {
                this.Lobby.NotifyWaitListOnGameCreated(this);
                this.WaitList.Clear();
            }
            return changed;
        }

        public void AddPlayerToWaitList(MasterClientPeer peer, JoinGameRequest operation)
        {
            this.WaitList.Add(new DeferredUser { Peer = peer, JoinRequest = operation });
        }

        public void OnRemoved()
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Game removed on GS. GameId={0}, ServerId={1}", this.Id, this.GameServer);
            }

            var oldCount = this.PlayerCount;

            var usersToCleanup = new List<string>(this.activeUserIdList);
            usersToCleanup.AddRange(this.inactiveUserIdList);
            usersToCleanup.AddRange(this.joiningPeers.Select(ps => ps.UserId));

            foreach (var usrId in usersToCleanup)
            {
                this.OnPeerLeftGameOnGameServer(usrId);
            }

            if (this.GameServer != null)
            {
                this.GameServer.OnPlayerCountChanged(0, oldCount);
            }

            this.Lobby.NotifyWaitListOnGameRemoved(this);

            this.WaitList.Clear();
        }

        public override string ToString()
        {
            var peer = this.GameServer != null ? this.GameServer.Peer : null;
            return
                string.Format(
                    "GameState '{0}': Lobby: '{9}', PlayerCount: {1}, Created on GS:'{2}' at {3}, GSPlayerCount: {4}, IsOpen: {5}, IsVisibleInLobby: {6}, IsVisible: {7}, UtcCreated: {8}",
                    this.Id,
                    this.PlayerCount,
                    this.HasBeenCreatedOnGameServer,
                    peer != null ? peer.ToString() : string.Empty,
                    this.GameServerPlayerCount,
                    this.IsOpen,
                    this.IsVisbleInLobby,
                    this.IsVisible,
                    this.CreateDateUtc,
                    this.Lobby.LobbyName);
        }

        public string GetDebugData()
        {
            return string.Format("CheckUserOnJoin:{0}, PeerCount:{1}, GameServerPlayerCount:{9}, YetExpectedCount:{2}, InactiveCount:{3},JoiningCount:{4}," +
                                 "ActivePlayers:{5},Inactive:{6},Expected:{7}, Joining:{8}, lastUpdateEvent:'{10}'",
                this.CheckUserIdOnJoin, this.PlayerCount, this.YetExpectedUsersCount, this.InactivePlayerCount,
                this.JoiningPlayerCount, Newtonsoft.Json.JsonConvert.SerializeObject(this.activeUserIdList),
                Newtonsoft.Json.JsonConvert.SerializeObject(this.inactiveUserIdList),
                Newtonsoft.Json.JsonConvert.SerializeObject(this.expectedUsersList),
                Newtonsoft.Json.JsonConvert.SerializeObject(this.joiningPeers), this.GameServerPlayerCount, this.lastUpdateEvent);
        }

        public bool ContainsUser(string userId)
        {
            return this.inactiveUserIdList.Contains(userId) 
                || this.activeUserIdList.Contains(userId)
                || this.IsUserJoining(userId);
        }

        public bool IsUserInExcludeList(string userId)
        {
            return (-1 != this.ExcludedActors.FindIndex(x => x.UserId == userId));
        }

        public bool IsUserExpected(string userId)
        {
            return (-1 != this.expectedUsersList.IndexOf(userId));
        }

        public bool CheckSlots(string userId, string[] expectedUsers)
        {
            return this.CheckSlots(userId, expectedUsers, out _);
        }

        public bool CheckSlots(string userId, string[] expectedUsers, out string errMsg)
        {
            errMsg = string.Empty;
            if (expectedUsers == null || this.MaxPlayer == 0)
            {
                return true;
            }
            var playerCount = this.PlayerCount + 
                expectedUsers.Count(expectedUser => !this.ContainsUser(expectedUser) 
                    && !this.IsUserExpected(expectedUser) && userId != expectedUser);
            playerCount += this.ContainsUser(userId) || this.IsUserExpected(userId) ? 0 : 1;

            if (this.MaxPlayer < playerCount)
            {
                errMsg = "MaxPlayer value is not big enough to reserve players slots";
                return false;
            }
            return true;
        }

        public void AddSlots(JoinGameRequest request)
        {
            if (request.AddUsers == null)
            {
                return;
            }

            foreach (var userId in request.AddUsers)
            {
                if (!this.IsUserExpected(userId))
                {
                    var oldValue = this.PlayerCount;
                    this.expectedUsersList.Add(userId);
                    if (!this.ContainsUser(userId))
                    {
                        this.Lobby.GameList.OnPlayerCountChanged(this, oldValue);
                        this.UpdateGameServerPlayerCount(oldValue);
                    }
                }
            }
        }
        #endregion

        #region Methods

        private void AddPeerState(PeerState peerState, ILobbyPeer peer)
        {
            var oldValue = this.PlayerCount;
            this.joiningPeers.AddLast(peerState);
            this.Lobby.GameList.OnPlayerCountChanged(this, oldValue);
            this.UpdateGameServerPlayerCount(oldValue);

            this.IsJoinable = this.CheckIsGameJoinable();

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Added peer: gameId={0}, userId={1}, joiningPeers={2}", this.Id, peerState.UserId, this.joiningPeers.Count);
            }

            // update player state in the online players cache
            if (this.Lobby.Application.PlayerOnlineCache != null && string.IsNullOrEmpty(peerState.UserId) == false)
            {
                this.Lobby.Application.PlayerOnlineCache.OnJoiningGame(peer, this);
            }
        }

        private bool IsUserJoining(string userId)
        {
            return this.joiningPeers.Any(joiningPeer => joiningPeer.UserId == userId);
        }

        private void StateCleanUp()
        {
            //var oldPeerCount = this.PlayerCount;

            //this.RemoveActiveUsers();
            //this.inactiveUserIdList.Clear();
            //this.ExcludedActors.Clear();
            ////this.expectedUsersList.Clear();
            //this.InactivePlayerCount = 0;
            //this.GameServerPlayerCount = 0;

            //this.Lobby.GameList.OnPlayerCountChanged(this, oldPeerCount);
            //this.UpdateGameServerPlayerCount(oldPeerCount);
        }

        private void RemoveActiveUsers()
        {
            if (this.Lobby.Application.PlayerOnlineCache != null && this.activeUserIdList.Count > 0)
            {
                foreach (var playerId in this.activeUserIdList)
                {
                    this.Lobby.Application.PlayerOnlineCache.OnDisconnectFromGameServer(playerId, this);
                }
            }
            this.activeUserIdList.Clear();
        }

        /// <summary>
        ///   Invoked for peers which has joined the game on the game server instance.
        /// </summary>
        /// <param name = "userId">The user id of the peer joined.</param>
        private void OnPeerJoinedGameOnGameServer(string userId)
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("User joined on game server: gameId={0}, userId={1}", this.Id, userId);
            }

            // remove the peer from the joining list
            var removed = this.RemoveFromJoiningList(userId);
            if (removed == false && log.IsDebugEnabled)
            {
                log.DebugFormat("User not found in joining list: gameId={0}, userId={1}", this.Id, userId);
            }

            if (string.IsNullOrEmpty(userId))
            {
                return;
            }

            this.inactiveUserIdList.Remove(userId);
            this.activeUserIdList.Add(userId);

            // update player state in the online players cache
            if (this.Lobby.Application.PlayerOnlineCache != null)
            {
                this.Lobby.Application.PlayerOnlineCache.OnJoinedGame(userId, this);
            }
        }

        /// <summary>
        ///   Invoked for peers which has left the game on the game server instance.
        /// </summary>
        /// <param name = "userId">The user id of the peer left.</param>
        /// <param name="deactivate">whether player was deactivated or removed</param>
        private void OnPeerLeftGameOnGameServer(string userId, bool deactivate = false)
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("User left on game server: gameId={0}, userId={1}", this.Id, userId);
            }

            this.RemoveFromJoiningList(userId);

            if (string.IsNullOrEmpty(userId))
            {
                return;
            }

            this.activeUserIdList.Remove(userId);
            if (deactivate)
            {
                if (!this.inactiveUserIdList.Contains(userId))
                {
                    this.inactiveUserIdList.Add(userId);
                }
            }
            else
            {
                this.inactiveUserIdList.Remove(userId);
                // user may be rejected during join process
            }

            // update player state in the online players cache
            if (this.Lobby.Application.PlayerOnlineCache != null)
            {
                this.Lobby.Application.PlayerOnlineCache.OnDisconnectFromGameServer(userId, this);
            }
        }

        private void OnPeerFailedToJoinOnGameServer(string userId)
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("User failed to join on game server: gameId={0}, userId={1}", this.Id, userId);
            }
            this.RemoveFromJoiningList(userId);

            if (string.IsNullOrEmpty(userId) == false)
            {
                if (this.Lobby.Application.PlayerOnlineCache != null)
                {
                    this.Lobby.Application.PlayerOnlineCache.OnDisconnectFromGameServer(userId, this);
                }
            }

            this.IsJoinable = this.CheckIsGameJoinable();
        }

        /// <summary>
        ///   Removes a peer with the specified user id from the list of joining peers.
        /// </summary>
        /// <param name = "userId">The user id of the peer to remove</param>
        /// <returns>True if the peer has been removed; otherwise false.</returns>
        private bool RemoveFromJoiningList(string userId)
        {
            if (userId == null)
            {
                userId = string.Empty;
            }

            var node = this.joiningPeers.First;

            while (node != null)
            {
                if (node.Value.UserId == userId)
                {
                    if (log.IsDebugEnabled)
                    {
                        log.DebugFormat("User removed from joining list.userId={0}", node.Value.UserId);
                    }

                    this.joiningPeers.Remove(node);
                    return true;
                }

                node = node.Next;
            }

            return false;
        }

        private bool UpdateProperties(Hashtable props)
        {
            bool changed = false;

            foreach (DictionaryEntry entry in props)
            {
                if (this.Properties.TryGetValue(entry.Key, out var oldValue))
                {
                    if (entry.Value == null)
                    {
                        changed = true;
                        this.Properties.Remove(entry.Key);
                    }
                    else
                    {
                        if (oldValue == null || !PropertyValueComparer.Compare(oldValue, entry.Value))
                        {
                            changed = true;
                            this.Properties[entry.Key] = entry.Value;
                        }
                    }
                }
                else
                {
                    if (entry.Value != null)
                    {
                        changed = true;
                        this.Properties[entry.Key] = entry.Value;
                    }
                }
            }

            return changed;
        }

        private bool CheckIsGameJoinable()
        {
            if (!this.IsOpen || !this.IsVisible || !(this.HasBeenCreatedOnGameServer || this.IsPersistent) 
                || (this.MaxPlayer > 0 && (this.PlayerCount) >= this.MaxPlayer))
            {
                return false;
            }

            return true;
        }

        private void UpdateGameServerPlayerCount(int oldPlayerCount)
        {
            if (this.GameServer != null)
            {
                this.GameServer.OnPlayerCountChanged(this.PlayerCount, oldPlayerCount);
            }
        }

        private bool OnExpectedListUpdated(string[] expectedUsers)
        {
            if (!this.expectedUsersList.SequenceEqual(expectedUsers))
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("adding expected users. count={0}", expectedUsers.Length);
                }
                this.expectedUsersList.Clear();
                this.expectedUsersList.AddRange(expectedUsers);

                return true;
            }
            return false;
        }

        private void OnUsersExcluded(IEnumerable<ExcludedActorInfo> usersToExclude)
        {
            if (log.IsDebugEnabled)
            {
                log.Debug("Exclude list operations will be executed");
            }
            try
            {
                foreach (var excludedActorInfo in usersToExclude)
                {
                    switch (excludedActorInfo.Reason)
                    {
                        case RemoveActorReason.Banned:
                            this.ExcludedActors.Add(excludedActorInfo);
                            if (log.IsDebugEnabled)
                            {
                                log.DebugFormat("User {0} will be added to excluded list with flag {1}", excludedActorInfo.UserId, excludedActorInfo.Reason);
                            }
                            break;
                        case RemoveActorReason.GlobalBanned:
                            this.ExcludedActors.Add(excludedActorInfo);
                            ((MasterApplication)ApplicationBase.Instance).DefaultApplication.AddToExcludedActors(excludedActorInfo.UserId);
                            if (log.IsDebugEnabled)
                            {
                                log.DebugFormat("User {0} will be added to gameState and gameApplication excluded list, global banned", excludedActorInfo.UserId);
                            }
                            break;
                        default:
                            log.WarnFormat("Unknown RemoveUser reason: {0}", excludedActorInfo.Reason);
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                log.Error(e);
            }
        }

        #endregion

    }
}