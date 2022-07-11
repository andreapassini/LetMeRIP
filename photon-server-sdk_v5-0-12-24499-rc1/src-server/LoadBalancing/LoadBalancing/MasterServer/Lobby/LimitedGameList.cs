// --------------------------------------------------------------------------------------------------------------------
// <copyright file="LimitedGameList.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the GameList type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System.Collections.Generic;

namespace Photon.LoadBalancing.MasterServer.Lobby
{
    #region using directives

    using System;
    using System.Collections;

    using ExitGames.Logging;
    using Photon.Hive.Operations;

    #endregion

    public class LimitedGameList : GameList
    {
        #region Constants and Fields

        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private readonly Random rnd = new Random();

        private List<string> updateQueue = new List<string>();

        private bool _useLegacyLobbies;
        private int _limitGameList;
        private int _limitGameListUpdate;

        #endregion

        public LimitedGameList(AppLobby lobby, bool useLegacyLobbies, int? limitGameList, int? limitGameListUpdate)
            : base(lobby)
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Creating new LimitedGameList");
            }

            _useLegacyLobbies = useLegacyLobbies;
            _limitGameList = limitGameList ?? MasterServerSettings.Default.Limits.Lobby.MaxGamesOnJoin;
            _limitGameListUpdate = limitGameListUpdate ?? MasterServerSettings.Default.Limits.Lobby.MaxGamesInUpdates;
        }

        //sort if more games than x available: closed games < full games < all other visible games
        protected override Hashtable GetAllGames(int maxCount)
        {
            if (_useLegacyLobbies)
            {
                return base.GetAllGames(maxCount);
            }

            //game lists deactivated (redundant, already checked in AddSubscription > if subscription is null no gamelist is send)
            if (_limitGameList == 0)
            {
                return new Hashtable();
            }

            if (maxCount <= 0 || maxCount > _limitGameList)
            {
                maxCount = _limitGameList;
            }
            if (maxCount > this.gameDict.Count)
            {
                maxCount = this.gameDict.Count;
            }

            var hashTable = new Hashtable(maxCount);

            //fewer or equal entries than limit, add all (visible)
            if (gameDict.Count <= maxCount)
            {
                foreach (GameState game in this.gameDict)
                {
                    if (game.IsVisbleInLobby)
                    {
                        Hashtable gameProperties = game.ToHashTable();
                        hashTable.Add(game.Id, gameProperties);
                    }
                }
            }
            //more entries, sort by priority
            else
            {
                var hashTableOpen = new Hashtable();
                var hashTableFull = new Hashtable();
                var hashTableClosed = new Hashtable();

                //start at random index to diversify initial game lists
                var startNodeIndex = this.rnd.Next(this.gameDict.Count);
                var startNode = this.gameDict.GetAtIndex(startNodeIndex);

                var node = startNode;
                do
                {
                    var game = node.Value;
                    node = node.Next ?? this.gameDict.First;

                    if (game.IsVisbleInLobby)
                    {
                        Hashtable gameProperties = game.ToHashTable();
                        if (!game.IsOpen)
                        {
                            hashTableClosed.Add(game.Id, gameProperties);
                        }
                        //cannot be greater
                        else if (game.PlayerCount >= game.MaxPlayer)
                        {
                            hashTableFull.Add(game.Id, gameProperties);
                        }
                        else
                        {
                            hashTableOpen.Add(game.Id, gameProperties);
                        }
                    }

                    if (hashTableOpen.Count >= maxCount)
                    {
                        break;
                    }
                }
                while (node != startNode);

                hashTable = hashTableOpen;

                //add full games until limit is reached
                if (hashTable.Count < maxCount && hashTableFull.Count > 0)
                {
                    foreach (DictionaryEntry pair in hashTableFull)
                    {
                        if (hashTable.Count >= maxCount)
                        {
                            break;
                        }
                        hashTable.Add(pair.Key, pair.Value);
                    }
                }
                //add closed games until limit is reached
                if (hashTable.Count < maxCount && hashTableClosed.Count > 0)
                {
                    foreach (DictionaryEntry pair in hashTableClosed)
                    {
                        if (hashTable.Count >= maxCount)
                        {
                            break;
                        }
                        hashTable.Add(pair.Key, pair.Value);
                    }
                }
            }

            return hashTable;
        }

        protected override Hashtable GetChangedGames()
        {
            if (_useLegacyLobbies)
            {
                return base.GetChangedGames();
            }

            //updates deactivated (redundant, already checked in PublishGameChanges)
            if (_limitGameListUpdate == 0)
            {
                return new Hashtable();
            }

            if (this.changedGames.Count == 0 && this.removedGames.Count == 0)
            {
                return new Hashtable();
            }

            //nothing to limit
            if (this.changedGames.Count <= _limitGameListUpdate)
            {
                updateQueue.Clear();
                return base.GetChangedGames();
            }

            var limit = Math.Min(_limitGameListUpdate, this.changedGames.Count);

            var hashTable = new Hashtable(limit + removedGames.Count);

            var count = 0;
            foreach (var gameId in updateQueue)
            {
                GameState gameInfo;
                if (this.changedGames.TryGetValue(gameId, out gameInfo))
                {
                    if (gameInfo.IsVisible)
                    {
                        Hashtable gameProperties = gameInfo.ToHashTable();
                        hashTable.Add(gameInfo.Id, gameProperties);
                    }
                    changedGames.Remove(gameId);
                }
                else
                {
                    //something to do? game could have been moved to removed 
                }

                count++;

                if (hashTable.Count >= limit)
                {
                    break;
                }
            }

            if (count > 0)
            {
                //just to be sure, this should not be possible
                if (count > updateQueue.Count)
                {
                    updateQueue.Clear();
                }
                else
                {
                    updateQueue.RemoveRange(0, count);
                }
            }

            //always add all removed games
            foreach (string gameId in this.removedGames)
            {
                hashTable.Add(gameId, new Hashtable { { (byte)GameParameter.Removed, true } });
                updateQueue.Remove(gameId);
            }

//            this.changedGames.Clear();
            this.removedGames.Clear();

            return hashTable;
        }

        protected override void HandleVisibility(GameState gameState, bool oldVisible)
        {
            //worth to unify? rather not
            if (_useLegacyLobbies)
            {
                base.HandleVisibility(gameState, oldVisible);
                return;
            }

            if (_limitGameListUpdate == 0)
            {
                return;
            }

            if (gameState.IsVisbleInLobby && !updateQueue.Contains(gameState.Id))
            {
                updateQueue.Add(gameState.Id);
            }
            
            base.HandleVisibility(gameState, oldVisible);
        }

        public override IGameListSubscription AddSubscription(MasterClientPeer peer, Hashtable gamePropertyFilter, int maxGameCount)
        {
            //could be unified: if(_useLegacyLobbies || _limitGameList > 0 || _limitGameListUpdate > 0)
            if (_useLegacyLobbies)
            {
                return base.AddSubscription(peer, gamePropertyFilter, maxGameCount);
            }

            //no gamelist and no updates
            if (_limitGameList == 0 && _limitGameListUpdate == 0)
            {
                return null;
            }

            return base.AddSubscription(peer, gamePropertyFilter, maxGameCount);
        }

        //don't publish game list updates
        public override void PublishGameChanges()
        {
            //could be unified: if(_useLegacyLobbies || _limitGameListUpdate > 0)
            if (_useLegacyLobbies)
            {
                base.PublishGameChanges();
                return;
            }

            if (_limitGameListUpdate == 0)
            {
                //games are added to removedGames in GameListBase.HandleVisibility (overwritten, not triggered) and RemoveGameState. If we don't clear removedGames here it will grow forever
                this.removedGames.Clear();
                return;
            }

             base.PublishGameChanges();
        }

        public override void UpdateLobbyLimits(bool gameListUseLegacyLobbies, int? gameListLimit, int? gameListLimitUpdates, int? gameListLimitSqlFilterResults)
        {
            var newUseLegacyLobbies = gameListUseLegacyLobbies;
            
            var newLimit = gameListLimit ?? MasterServerSettings.Default.Limits.Lobby.MaxGamesOnJoin;
            if (newLimit != _limitGameList)
            {
                if (log.IsInfoEnabled)
                {
                    log.InfoFormat("Changed GameListLimit from {0} to {1} ({2}/{3})", _limitGameList, newLimit, Lobby.LobbyName, Lobby.LobbyType);
                }
                _limitGameList = newLimit;

                //what happens if only the limits where changed (and its a limited lobby)?
                //if limit was set to 0 (and update limit is 0) we could remove the subscription peers
            }

            var newLimitUpdate = gameListLimitUpdates ?? MasterServerSettings.Default.Limits.Lobby.MaxGamesInUpdates;
            if (newLimitUpdate != _limitGameListUpdate)
            {
                if (log.IsInfoEnabled)
                {
                    log.InfoFormat("Changed GameListLimitUpdate from {0} to {1} ({2}/{3})", _limitGameListUpdate, newLimitUpdate, Lobby.LobbyName, Lobby.LobbyType);
                }
                _limitGameListUpdate = newLimitUpdate;

                //what happens if only the limits where changed (and its a limited lobby)?
                //if update limit is set to 0 we could clear changed/removed games and update queue (not happening now)
                if (!newUseLegacyLobbies && newLimitUpdate == 0)
                {
                    this.changedGames.Clear();
                    this.removedGames.Clear();
                    this.updateQueue.Clear();
                    if (log.IsInfoEnabled)
                    {
                        log.InfoFormat("Set update limit to 0, cleared lists ({0}/{1})", Lobby.LobbyName, Lobby.LobbyType);
                    }
                }
                //if it was 0 before we can't do anything, there are no changed/removed games at the moment
            }

            if (newUseLegacyLobbies == _useLegacyLobbies)
            {
                return;
            }

            if (log.IsInfoEnabled)
            {
                log.InfoFormat("Changed GameListUseLegacyLobbies from {0} to {1} ({2}/{3})", _useLegacyLobbies, newUseLegacyLobbies, Lobby.LobbyName, Lobby.LobbyType);
            }
            _useLegacyLobbies = newUseLegacyLobbies;
            //switch to legacy lobbies - we care less for this option
            if (_useLegacyLobbies)
            {
                updateQueue.Clear();
                if (log.IsInfoEnabled)
                {
                    log.InfoFormat("Switched to legacy lobby, cleared updateQueue ({0}/{1})", Lobby.LobbyName, Lobby.LobbyType);
                }
            }
            //switch to limited lobbies
            else
            {
                //subscriptions? -> keep them, we still send updates (unless _limitGameList == 0 && _limitGameListUpdate == 0)
                if (_limitGameList == 0 && _limitGameListUpdate == 0)
                {
                    this.peers.Clear();
                    if (log.IsInfoEnabled)
                    {
                        log.InfoFormat("Switched to limited lobby, no updates or initial gamelist, cleared subscription peers ({0}/{1})", Lobby.LobbyName, Lobby.LobbyType);
                    }
                }
                //changed games list? -> don't clear, add them to update queue if necessary (=actual switch, not each time UpdateLobbyLimits is called)
                if (_limitGameListUpdate > 0)
                {
                    if (this.changedGames.Count > 0 && this.updateQueue.Count == 0)
                    {
                        updateQueue.AddRange(changedGames.Keys);
                        if (log.IsInfoEnabled)
                        {
                            log.InfoFormat("Switched to limited lobby, added {0} games to the update queue ({1}) ({2}/{3})", changedGames.Count, updateQueue.Count, Lobby.LobbyName, Lobby.LobbyType);
                        }
                    }
                }
                //no more updates, already done once if update limit was also changed (no harm doing it twice) 
                else
                {
                    this.changedGames.Clear();
                    this.removedGames.Clear();
//                    this.updateQueue.Clear(); //was not used with legacy lobby
                    if (log.IsInfoEnabled)
                    {
                        log.InfoFormat("Switched to limited lobby, no more updates, cleared lists ({0}/{1})", Lobby.LobbyName, Lobby.LobbyType);
                    }
                }
            }
        }
    }
}
