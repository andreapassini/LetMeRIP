// --------------------------------------------------------------------------------------------------------------------
// <copyright file="SqlFilterGameList.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the GameList type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Photon.LoadBalancing.MasterServer.Lobby
{
    #region using directives

    using System.Collections;
    using ExitGames.Logging;
    using Photon.Common;

    #endregion

    public class SqlFilterGameList : SqlGameList
    {
        #region Constants and Fields

        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private bool _useLegacyLobbies;
//        private int _limitSqlFilterResults;

        #endregion

        public SqlFilterGameList(AppLobby lobby, bool useLegacyLobbies, int? limitSqlFilterResults, string matchmakingStoredProcedure) : base(lobby, limitSqlFilterResults, matchmakingStoredProcedure)
        {
            if (log.IsDebugEnabled)
            {
                log.Debug($"Creating new SqlFilterGameList. useLegacyLobbies:{useLegacyLobbies}");
            }

            _useLegacyLobbies = useLegacyLobbies;
        }

        protected override void HandleVisibility(GameState gameState, bool oldVisible)
        {
            if (_useLegacyLobbies)
            {
                base.HandleVisibility(gameState, oldVisible);
            }
        }

        //don't send game list
        public override IGameListSubscription AddSubscription(MasterClientPeer peer, Hashtable gamePropertyFilter, int maxGameCount)
        {
            if (_useLegacyLobbies)
            {
                return base.AddSubscription(peer, gamePropertyFilter, maxGameCount);
            }

            if (log.IsDebugEnabled)
            {
                log.Debug($"SqlFilterGameList - no subscription is possible we do not use legacy lobbies");
            }
            return null;
        }

        //don't publish game list updates
        public override void PublishGameChanges()
        {
            if (_useLegacyLobbies)
            {
                base.PublishGameChanges();
            }
            else
            {
                //games are added to removedGames in GameListBase.HandleVisibility (overwritten, not triggered) and RemoveGameState. If we don't clear removedGames here it will grow forever
                this.removedGames.Clear();
            }
        }

        public override void UpdateLobbyLimits(bool gameListUseLegacyLobbies, int? gameListLimit, int? gameListLimitUpdates, int? gameListLimitSqlFilterResults)
        {
            var newUseLegacyLobbies = gameListUseLegacyLobbies;
            if (newUseLegacyLobbies != _useLegacyLobbies)
            {
                if (log.IsInfoEnabled)
                {
                    log.InfoFormat("Changed GameListUseLegacyLobbies from {0} to {1} ({2}/{3})", _useLegacyLobbies, newUseLegacyLobbies, Lobby.LobbyName, Lobby.LobbyType);
                }
                _useLegacyLobbies = newUseLegacyLobbies;
                if (_useLegacyLobbies)
                {
                    //nothing to do
                }
                //possible switch to limited lobbies
                else
                {
                    //for SqlGameList: no more updates. 
                    //we could clear the changed and removed game list (to free memory) 
                    this.changedGames.Clear();
                    this.removedGames.Clear();
                    //we could remove the subscriptions (peers) but this seems not necessary
                    this.peers.Clear();
                    if (log.IsInfoEnabled)
                    {
                        log.InfoFormat("Switched to limited lobby, cleared lists ({0}/{1})", Lobby.LobbyName, Lobby.LobbyType);
                    }
                }
            }

            base.UpdateLobbyLimits(gameListUseLegacyLobbies, gameListLimit, gameListLimitUpdates, gameListLimitSqlFilterResults);
        }
    }
}
