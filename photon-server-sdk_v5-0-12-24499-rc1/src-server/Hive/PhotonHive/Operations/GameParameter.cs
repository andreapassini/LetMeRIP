// --------------------------------------------------------------------------------------------------------------------
// <copyright file="GameParameter.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the GameParameter type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using Photon.Hive.Plugin;

namespace Photon.Hive.Operations
{
    /// <summary>
    /// Well known game properties (used as byte keys in game-property hashtables).
    /// </summary>
    public enum GameParameter : byte
    {
        MaxPlayers = GameParameters.MaxPlayers, 
        IsVisible = GameParameters.IsVisible, 
        IsOpen = GameParameters.IsOpen,
        PlayerCount = 252,  // used in gamestate reproted to master
        Removed = 251, // used in gamestate reproted to master
        LobbyProperties = GameParameters.LobbyProperties,
        MasterClientId = GameParameters.MasterClientId,
        ExpectedUsers = GameParameters.ExpectedUsers,
        PlayerTTL = GameParameters.PlayerTTL,
        EmptyRoomTTL = GameParameters.EmptyRoomTTL,

        MinValue = 235,
    }
}