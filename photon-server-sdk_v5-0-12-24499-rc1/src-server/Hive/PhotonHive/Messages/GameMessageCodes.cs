// --------------------------------------------------------------------------------------------------------------------
// <copyright file="GameMessageCodes.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   GameMessageCodes define the type of a "LiteGame" Message, the meaning and its content.
//   Messages are used to communicate async with rooms and games.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Photon.Hive.Messages
{
    /// <summary>
    /// GameMessageCodes define the type of a "LiteGame" Message, the meaning and its content.
    /// Messages are used to communicate async with rooms and games.
    /// </summary>
    public enum GameMessageCodes : byte
    {
        /// <summary>
        /// Message is an operation.
        /// </summary>
        Operation = 0,

        /// <summary>
        /// Message to remove peer from game.
        /// </summary>
        RemovePeerFromGame = 1,

        MaxValue = 10,
    }
}