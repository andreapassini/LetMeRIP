// --------------------------------------------------------------------------------------------------------------------
// <copyright file="GameList.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the GameList type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Photon.LoadBalancing.MasterServer.Lobby
{
    #region using directives

    using System;
    using System.Collections;
    using System.Data.Common;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Linq;
    using ExitGames.Logging;
    using Photon.Common;
    using Photon.SocketServer;
    using Photon.LoadBalancing.MasterServer.GameServer;
    using Photon.LoadBalancing.Operations;
    using Photon.LoadBalancing.ServerToServer.Events;
    using Photon.SocketServer.Diagnostics;

    #endregion

    public class SqlGameList : GameListBase
    {
        #region Constants and Fields

        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        protected readonly GameTable gameDatabase = new GameTable(10, "C");
        
        int _limitSqlFilterResults;

        private bool useStoredProcedures;
        private List<string> protectedGameProperties = new List<string>();
        private Dictionary<string, string> storedProcedures = new Dictionary<string, string>();

        private string matchmakingStoredProcedure;

        #endregion

        #region Constructors and Destructors

        public SqlGameList(AppLobby lobby, int? limitSqlFilterResults, string matchmakingStoredProcedure = null)
            : base(lobby)
        {
            this._limitSqlFilterResults = limitSqlFilterResults ?? MasterServerSettings.Default.Limits.Lobby.MaxGamesInGetGamesListResponse;

            if (log.IsDebugEnabled)
            {
                log.Debug($"Creating new SqlGameList _limitSqlFilterResults:{this._limitSqlFilterResults}");
            }


            this.matchmakingStoredProcedure = matchmakingStoredProcedure;

            //do this in the AppLobby and pass dictionary to here?
//            TryLoadStoredProceduresFromFile();

            TryLoadStoredProcedures(matchmakingStoredProcedure);
        }

        #endregion

        
        #region Public Methods

        public override void AddGameState(GameState gameState, Dictionary<string, object> authCookie = null)
        {
            //set the protected game properties with values from the auth cookie, remove keys if not in auth cookie
            if (useStoredProcedures)
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("AddGameState id '{0}', setting protected game properties", gameState.Id);
                }

                foreach (var key in protectedGameProperties)
                {
                    if (authCookie != null && authCookie.ContainsKey(key))
                    {
                        gameState.Properties[key] = authCookie[key];
                        if (log.IsDebugEnabled)
                        {
                            log.DebugFormat("AddGameState, game property {0}/{1}", key, authCookie[key]);
                        }
                    }
                    else
                    {
                        var remove = gameState.Properties.Remove(key);
                        if (log.IsDebugEnabled)
                        {
                            log.DebugFormat("AddGameState, removed game property {0}: {1}", key, remove);
                        }
                    }
                }
            }
            
            base.AddGameState(gameState);

            if (gameState.IsJoinable)
            {
                this.gameDatabase.InsertGameState(gameState.Id, gameState.Properties);
            }
        }

        protected override bool RemoveGameState(GameState gameState)
        {
            this.gameDatabase.Delete(gameState.Id);
            return base.RemoveGameState(gameState);
        }

        public override ErrorCode TryGetRandomGame(JoinRandomGameRequest joinRequest, ILobbyPeer peer, out GameState gameState, out string message)
        {
            message = null;
            gameState = null;

            if (this.gameDict.Count == 0)
            {
//                gameState = null;
                return ErrorCode.NoMatchFound;
            }

            if (string.IsNullOrEmpty(joinRequest.QueryData))
            {
                return GetFirstJoinableGame(joinRequest, peer, out gameState);

//                var node = this.gameDict.First;
//                while (node != null)
//                {
//                    gameState = node.Value;
//                    if (IsGameJoinable(joinRequest, peer, gameState))
//                    {
//                        return ErrorCode.Ok;
//                    }
//
//                    node = node.Next;
//                }
//
////                gameState = null;
//                return ErrorCode.NoMatchFound;
            }

            if (!useStoredProcedures)
            {
                //remove last semicolon, otherwise we have another (empty) query after split
                if (joinRequest.QueryData.EndsWith(";"))
                {
                    joinRequest.QueryData = joinRequest.QueryData.Remove(joinRequest.QueryData.Length - 1);
                }

                var splitQueries = joinRequest.QueryData.Split(';');
                //create a setting for max numbers of queries?
                if (splitQueries.Length > 3)
                {
                    //create entry in ErrorMessages?
                    message = "Max queries allowed: 3";
                    return ErrorCode.OperationInvalid;
                }

                foreach (var query in splitQueries)
                {
                    if (string.IsNullOrEmpty(query))
                    {
                        //error or ignore?
                        continue;

//                        message = "Query was empty";
//                        return ErrorCode.OperationInvalid;
                    }

                    var result = TryGetRandomGame(query, joinRequest, peer, out gameState, out message);
                    if (result != ErrorCode.NoMatchFound)
                    {
                        //match or error
                        return result;
                    }
                }

                //no match, check options
//                return joinRequest.JoinRandomType == (byte)JoinRandomType.JoinRandomOnSqlNoMatch
//                    ? GetFirstJoinableGame(joinRequest, peer, out gameState)
//                    : ErrorCode.NoMatchFound;

                return ErrorCode.NoMatchFound;

//                return TryGetRandomGame(joinRequest.QueryData, joinRequest, peer, out gameState, out message);
            }

            //obsolete soon
            //TODO const
            //TODO decide stored procedure prefix
            var spPrefix = "$SP.";

            var spName = joinRequest.QueryData;

            //temp, until the prefix is removed
            if (joinRequest.QueryData.StartsWith(spPrefix, StringComparison.InvariantCultureIgnoreCase))
            {
                spName = joinRequest.QueryData.Substring(spPrefix.Length).ToLower();
            }

            if (!storedProcedures.ContainsKey(spName) || string.IsNullOrEmpty(storedProcedures[spName]))
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("Stored procedure '{0}' not found", spName);
                }

                message = "Stored procedure not found";
                return ErrorCode.OperationInvalid;
            }

            var queryData = storedProcedures[spName];

            //TODO const
            //TODO decide value
//            var authCookiePlaceholderPrefix = "$ac.";
            const char placeholderStart = '[';
            const char placeholderEnd = ']';

            //replace placeholder values from AuthCookie
            //just call string.Replace for all values in the AuthCookie?
            var masterClientPeer = (MasterClientPeer) peer;

            queryData = ReplacePlaceholderIgnoreCase(queryData, masterClientPeer.AuthCookie);

//            if (queryData.IndexOf(authCookiePlaceholderPrefix, StringComparison.InvariantCultureIgnoreCase) >= 0)
            if (queryData.IndexOf(placeholderStart) >= 0 || queryData.IndexOf(placeholderEnd) >= 0)
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("Stored procedure '{0}' contains placeholder entries not found in AuthCookie", spName);
                }

                message = "Stored procedure contains placeholder entries not found in AuthCookie";
                return ErrorCode.OperationInvalid;
            }

            //strip last semicolon in case one was added to end of stored procedure > already done when reading file
//            if (queryData.EndsWith(";"))
//            {
//                queryData = queryData.Substring(0, queryData.Length - 1);
//            }

            //TODO decide delimiter. using semicolon means that funnels can only be used with stored procedures (if client sends semicolon a NotAllowedSemicolonInQuereyData is returned)
            var queries = queryData.Split(';');

            var errorCode = ErrorCode.NoMatchFound;

            //TODO const
            //TODO max number of queries
            var limit = 10;

            for (int i = 0; i < queries.Length; i++)
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("Searching match, index {0}, query '{1}'", i, queries[i]);
                }

                errorCode = TryGetRandomGame(queries[i], joinRequest, peer, out gameState, out message);

                if (errorCode != ErrorCode.NoMatchFound || i >= limit-1)
                {
                    break;
                }
            }

            return errorCode;
        }

        private ErrorCode TryGetRandomGame(string queryData, JoinRandomGameRequest joinRequest, ILobbyPeer peer, out GameState gameState, out string message)
        {
            message = null;

            int skipCount = 0;
            while (true)
            {
                string id;
                try
                {
                    id = this.gameDatabase.FindMatch(queryData, skipCount++);
                }
                catch (DbException sqlException)
                {
                    gameState = null;
                    message = sqlException.Message;
                    return ErrorCode.OperationInvalid;
                }

                if (string.IsNullOrEmpty(id))
                {
                    gameState = null;
                    return ErrorCode.NoMatchFound;
                }

                if (!this.gameDict.TryGet(id, out gameState))
                {
                    return ErrorCode.NoMatchFound;
                }

                if (IsGameJoinable(joinRequest, peer, gameState))
                {
                    if (log.IsDebugEnabled)
                    {
                        log.Debug($"Random Game '{gameState}', CheckUserOnJoin:{gameState.CheckUserIdOnJoin}, " +
                                  $"UserLists: {gameState.GetUserListsAsString()} Properties:{ValueToString.ToString(gameState.ToHashTable())}" +
                                  $"is joinable for request: {ValueToString.OperationToString(joinRequest.OperationRequest)}, Joining User:{peer.UserId}");
                    }
                    return ErrorCode.Ok;
                }
            }
        }

        private ErrorCode GetFirstJoinableGame(JoinRandomGameRequest joinRequest, ILobbyPeer peer, out GameState gameState)
        {
            var node = this.gameDict.First;
            while (node != null)
            {
                gameState = node.Value;
                if (IsGameJoinable(joinRequest, peer, gameState))
                {
                    log.Debug($"First Joinable Game '{gameState}', CheckUserOnJoin:{gameState.CheckUserIdOnJoin}, " +
                              $"UserLists: {gameState.GetUserListsAsString()} Properties:{ValueToString.ToString(gameState.ToHashTable())}" +
                              $"is joinable for request: {ValueToString.OperationToString(joinRequest.OperationRequest)}, Joining User:{peer.UserId}");
                    return ErrorCode.Ok;
                }

                node = node.Next;
            }

            gameState = null;
            return ErrorCode.NoMatchFound;
        }

        public override void OnGameJoinableChanged(GameState gameState)
        {
            var isJoinable = gameState.IsJoinable;

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("OnGameJoinableChanged: gameId={0}, joinable={1}", gameState.Id, isJoinable);
            }

            if (isJoinable)
            {
                if (useStoredProcedures && log.IsDebugEnabled)
                {
                    log.Debug("Protected game properties");
                    foreach (var key in protectedGameProperties)
                    {
                        log.DebugFormat("{0}/{1}", key, gameState.Properties.ContainsKey(key) ? gameState.Properties[key] : "null");
                    }
                }
                this.gameDatabase.InsertGameState(gameState.Id, gameState.Properties);
            }
            else
            {
                this.gameDatabase.Delete(gameState.Id);
            }
        }

        public override bool UpdateGameState(UpdateGameEvent updateOperation, GameServerContext incomingGameServerPeer, out GameState gameState)
        {
            //use the exisiting values for the protected game parameters
            if (useStoredProcedures)
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("UpdateGameState {0}", updateOperation.GameId);
                }

                if (!this.gameDict.TryGetValue(updateOperation.GameId, out gameState))
                {
                    if (log.IsDebugEnabled)
                    {
                        log.Debug("Game not found");
                    }
                    return false;
                }
                if (gameState.Properties != null)
                {
                    if (updateOperation.GameProperties == null)
                    {
                        updateOperation.GameProperties = new Hashtable();
                    }

                    foreach (var key in protectedGameProperties)
                    {
                        if (gameState.Properties.ContainsKey(key))
                        {
                            updateOperation.GameProperties[key] = gameState.Properties[key];
                            if (log.IsDebugEnabled)
                            {
                                log.DebugFormat("UpdateGameState, set key {0}/{1}", key, gameState.Properties[key]);
                            }
                        }
                        else
                        {
                            updateOperation.GameProperties.Remove(key);
                            if (log.IsDebugEnabled)
                            {
                                log.DebugFormat("UpdateGameState, removed key {0}", key);
                            }
                        }
                    }
                }
            }

            if (base.UpdateGameState(updateOperation, incomingGameServerPeer, out gameState))
            {
                if (gameState.IsJoinable)
                {
                    this.gameDatabase.Update(gameState.Id, gameState.Properties);
                }
                return true;
            }
            return false;
        }

        public Hashtable GetGameList(string query, out ErrorCode errorCode, out string message)
        {
            if (log.IsDebugEnabled)
            {
                log.Debug($"SqlGameList.GetGameList is called. query:'{query}'");
            }

            errorCode = ErrorCode.Ok;
            message = null;

            var games = new Hashtable();

            if (this.gameDict.Count == 0 || _limitSqlFilterResults <= 0)
            {
                if (log.IsDebugEnabled)
                {
                    log.Debug($"SqlGameList.GetGameList returned empty game list. gameDictCount:{this.gameDict.Count}, ._limitSqlFilterResults:{this._limitSqlFilterResults}, query:'{query}'");
                }
                return games;
            }

            try
            {
                var gameIds = gameDatabase.FindMatches(query, _limitSqlFilterResults);
                foreach (var gameId in gameIds)
                {
                    GameState gameState;
                    if (this.gameDict.TryGet(gameId, out gameState))
                    {
                        games.Add(gameId, gameState.ToHashTable());
                    }
                }
            }
            catch (DbException sqlException)
            {
                errorCode = ErrorCode.OperationInvalid;
                message = sqlException.Message;
            }

            return games;
        }

        public override void UpdateLobbyLimits(bool gameListUseLegacyLobbies, int? gameListLimit, int? gameListLimitUpdates, int? gameListLimitSqlFilterResults)
        {
            var newLimit = gameListLimitSqlFilterResults ?? MasterServerSettings.Default.Limits.Lobby.MaxGamesInGetGamesListResponse;
            if (newLimit != _limitSqlFilterResults)
            {
                if (log.IsInfoEnabled)
                {
                    log.InfoFormat("Changed GameListLimitSqlFilterResults from {0} to {1} ({2}/{3})", _limitSqlFilterResults, newLimit, Lobby.LobbyName, Lobby.LobbyType);
                }
                _limitSqlFilterResults = newLimit;
            }
        }

        #endregion

        private void TryLoadStoredProceduresFromFile()
        {
            var combined = Path.Combine(ApplicationBase.Instance.ApplicationRootPath, "StoredProcedures.config");
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("TryLoadStoredProceduresFromFile, using path {0}", combined);
            }

            try
            {
                if (!File.Exists(combined))
                {
                    useStoredProcedures = false;
                    return;
                }

                useStoredProcedures = true;
                
                using (var file = new StreamReader(combined))
                {
                    TryLoadStoredProcedures(file.ReadToEnd());
                }
            }
            catch (Exception ex)
            {
                log.ErrorFormat("TryLoadStoredProceduresFromFile: {0}", ex.Message);
            }
        }

        private string ReplacePlaceholderIgnoreCase(string input, Dictionary<string, object> authCookie)
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("ReplacePlaceholderIgnoreCase: '{0}'", input);
            }

//            const string prefix = "$ac.";
            const char placeholderStart = '[';
            const char placeholderEnd = ']';
            var indexStart = 0;
            var indexEnd = 0;

            indexEnd = input.IndexOf(placeholderStart, indexStart);
            if (indexEnd < 0)
            {
                if (log.IsDebugEnabled)
                {
                    log.Debug("ReplacePlaceholderIgnoreCase, nothing to replace");
                }
                return input;
            }

            var authCookieIgnoreCase = new Dictionary<string, object>(authCookie, StringComparer.CurrentCultureIgnoreCase);
            var sb = new StringBuilder();

            while (indexEnd >= 0)
            {
//                indexEnd = input.IndexOf(prefix, indexStart, StringComparison.InvariantCultureIgnoreCase);

                sb.Append(input, indexStart, indexEnd - indexStart);
                indexStart = indexEnd;
                indexEnd = input.IndexOf(placeholderEnd, indexStart + 1);

                //no closing tag
                if (indexEnd < 0)
                {
                    sb.Append(input.Substring(indexStart));
                    if (log.IsDebugEnabled)
                    {
                        log.DebugFormat("ReplacePlaceholderIgnoreCase, didn't find closing tag: '{0}'", sb.ToString());
                    }
                    return sb.ToString();
                }

                var key = input.Substring(indexStart + 1, indexEnd - indexStart - 1);
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("ReplacePlaceholderIgnoreCase, key '{0}'", key);
                }

                if (!authCookieIgnoreCase.ContainsKey(key))
                {
                    sb.Append(input.Substring(indexStart));

                    if (log.IsDebugEnabled)
                    {
                        log.DebugFormat("ReplacePlaceholderIgnoreCase, did not find key in auth cookie: '{0}'", sb.ToString());
                    }

                    return sb.ToString();
                }

                sb.Append(authCookieIgnoreCase[key]);
                indexStart = indexEnd + 1;
                indexEnd = input.IndexOf(placeholderStart, indexStart);
            }

            sb.Append(input.Substring(indexStart));

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("ReplacePlaceholderIgnoreCase: '{0}'", sb.ToString());
            }

            return sb.ToString();
        }

        private void TryLoadStoredProcedures(string matchmakingStoredProcedure)
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("TryLoadStoredProcedures, using MatchmakingStoredProcedure config\n###\n{0}\n###\n", matchmakingStoredProcedure);
            }

            useStoredProcedures = false;
            protectedGameProperties = new List<string>();
            storedProcedures = new Dictionary<string, string>();

            try
            {
                if (string.IsNullOrEmpty(matchmakingStoredProcedure))
                {
                    return;
                }

                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("Config uses \\r\\n line break: {0}", matchmakingStoredProcedure.IndexOf("\r\n") >= 0);
                }

                //just in case we get different line breaks
                matchmakingStoredProcedure = matchmakingStoredProcedure.Replace("\r\n", "\n");
                var lines = matchmakingStoredProcedure.Split('\n');

                if (lines.Length < 2)
                {
                    if (log.IsDebugEnabled)
                    {
                        log.DebugFormat("Invalid config, minimum number of lines is 2 (was {0}, line 1 protected game properties, line 2+ stored procedures)", lines.Length);
                    }
                    return;
                }

                if (lines[0] != null && !string.IsNullOrEmpty(lines[0]))
                {
                    protectedGameProperties = lines[0].Split(';').ToList();
                }

                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("{0} protected game properties: '{1}'", protectedGameProperties.Count, lines[0]);
                }

                for (int i = 1; i < lines.Length; i++)
                {
                    var pair = lines[i].Split(':');
                    if (pair.Length != 2)
                    {
                        if (log.IsDebugEnabled)
                        {
                            log.DebugFormat("Stored procedure wrong format, skipping line '{0}'", lines[i]);
                        }
                        continue;
                    }

                    //warn if key is already used?
                    storedProcedures[pair[0].ToLower()] = pair[1];
                }
            }
            catch (Exception ex)
            {
                log.ErrorFormat("TryLoadStoredProcedures: {0}", ex.Message);
            }

            var keys = storedProcedures.Keys.ToArray();
            //minimal validation
            foreach (var key in keys)
            {
                var sp = storedProcedures[key];

                //strip last semicolon if there is one
                if (sp.EndsWith(";"))
                {
                    sp = sp.Remove(sp.Length - 1);   //Substring(0, sp.Length - 1);
                    storedProcedures[key] = sp;
                }

                //check for forbidden words
                var wrongWords = MasterServerSettings.Default.SqlQueryBlockList.Split(';');
                foreach (var word in wrongWords)
                {
                    if (sp.Contains(word))
                    {
                        if (log.IsDebugEnabled)
                        {
                            log.DebugFormat("Removing stored procedure '{0}', contains not allowed word '{1}'", key, word);
                        }

                        storedProcedures[key] = null;
                        break;
                    }
                }

                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("Stored procedure '{0}': '{1}'", key, storedProcedures[key]);
                }
            }

            //set to true if at least 1 SP is configured. Doesn't matter if any protected game properties are set as long as the first config line was used (empty or actual game properties keys)
            useStoredProcedures = storedProcedures.Count > 0;

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("TryLoadStoredProcedures, loaded {0} stored procedures", storedProcedures.Count);
            }
        }

        public void UpdateMatchmakingStoredProcedure(string matchmakingStoredProcedure)
        {
            if (!string.Equals(this.matchmakingStoredProcedure, matchmakingStoredProcedure))
            {
                if (log.IsInfoEnabled)
                {
                    log.InfoFormat("Changed MatchmakingStoredProcedure for lobby {0}/{1} from\n###\n{2}\n###\nto\n###\n{3}\n###\n", Lobby.LobbyName, Lobby.LobbyType, this.matchmakingStoredProcedure, matchmakingStoredProcedure);
                }
                this.matchmakingStoredProcedure = matchmakingStoredProcedure;
                TryLoadStoredProcedures(matchmakingStoredProcedure);
            }
            else if(log.IsDebugEnabled)
            {
                log.DebugFormat("UpdateMatchmakingStoredProcedure, no change, update for testing");
                TryLoadStoredProcedures(matchmakingStoredProcedure);
            }
        }
    }
}