namespace Photon.LoadBalancing.MasterServer.Lobby
{
    using System;
    using System.Collections;
    using System.Collections.Generic;

    using Photon.Common;
    using Photon.LoadBalancing.MasterServer.GameServer;
    using Photon.LoadBalancing.Operations;
    using Photon.LoadBalancing.ServerToServer.Events;

    public interface IGameList
    {
        int Count { get; }

        int PlayerCount { get; }

        void AddGameState(GameState gameState, Dictionary<string, object> authCookie = null);

        int CheckJoinTimeOuts(TimeSpan timeOut);

        int CheckJoinTimeOuts(DateTime minDateTime);

        bool ContainsGameId(string gameId);

        IGameListSubscription AddSubscription(MasterClientPeer peer, Hashtable gamePropertyFilter, int maxGameCount);

        void RemoveGameServer(GameServerContext gameServer);

        bool RemoveGameStateByName(string gameId);

        bool TryGetGame(string gameId, out GameState gameState);

        ErrorCode TryGetRandomGame(JoinRandomGameRequest joinRequest, ILobbyPeer peer, out GameState gameState, out string message);

        bool UpdateGameState(UpdateGameEvent updateOperation, GameServerContext gameServer, out GameState gameState);

        void PublishGameChanges();

        void OnPlayerCountChanged(GameState gameState, int oldPlayerCount);

        void OnGameJoinableChanged(GameState gameState);

        void SetExpectReplicationFlag(bool serverContext, GameServerContext gameServerContext);
        void RemoveNotReplicatedGames(GameServerContext gameServerContext);

        void UpdateLobbyLimits(bool gameListUseLegacyLobbies, int? gameListLimit, int? gameListLimitUpdates, int? gameListLimitSqlFilterResults);
    }
}
