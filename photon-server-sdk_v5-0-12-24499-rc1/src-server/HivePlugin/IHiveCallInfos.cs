// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ICallInfo.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;

namespace Photon.Hive.Plugin
{
    using System.Collections.Generic;


    public interface ITypedCallInfo<out RequestType> : ICallInfo
        where RequestType : IOperationRequest
    {
        #region Properties

        RequestType Request { get; }
        /// <summary>
        /// User ID of the actor triggering the call.
        /// </summary>
        string UserId { get; }
        /// <summary>
        /// NickName of the actor triggering the call.
        /// </summary>
        string Nickname { get; }

#if PLUGINS_0_9
        [Obsolete("Use Nickname instead")]
        string Username { get; }
#endif
        [Obsolete("Use AuthCookie instead")]
        object AuthResultsToken { get; }
        /// <summary>
        /// AuthCookie of the actor triggering the call.
        /// </summary>
        Dictionary<string, object> AuthCookie { get; } 
        #endregion
    }
    /// <summary>
    /// Base interface of argument passed to OnLeave callback.
    /// </summary>
    public interface ILeaveGameCallInfo : ITypedCallInfo<ILeaveGameRequest>
    {
        #region Properties
        /// <summary>Number of the actor leaving the room.</summary>
        int ActorNr { get; }
        /// <summary>
        /// Indicates whether or not the actor is marked inactive and can rejoin the room later.
        /// </summary>
        bool IsInactive { get; }
        /// <summary>
        /// Code for the reason why the actor is leaving the room. 
        /// For possible values, <see cref="Photon.Hive.Plugin.LeaveReason"/>.
        /// </summary>
        int Reason { get; }
        /// <summary>
        /// Logs of last exchange between peer and server before the leave event.
        /// </summary>
        string Details { get; }

        #endregion
    }

#if PLUGINS_0_9
    [Obsolete("Use ILeaveGameCallInfo instead")]
    public interface ILeaveCallInfo : ILeaveGameCallInfo
    {
    }
#endif
    /// <summary>
    /// Base interface of argument passed to OnCreateGame callback.
    /// </summary>
    public interface ICreateGameCallInfo : ITypedCallInfo<IJoinGameRequest>
    {
        #region Properties
        /// <summary>
        /// Indicates if the operation triggering this call is Op Join with JoinMode.CreateIfNotExists 
        /// or JoinMode.JoinOrRejoin or JoinMode.RejoinOnly.
        /// </summary>
        bool IsJoin { get; }
        /// <summary>
        /// Indicates whether to create room if it could not be loaded from external source and joined.
        /// If true, this call is triggered by Op Join with JoinMode.CreateIfNotExists.
        /// </summary>
        bool CreateIfNotExists { get; }
        /// <summary>
        /// Room creation options.
        /// </summary>
        Dictionary<string, object> CreateOptions { get; }
 
        #endregion
    }
    /// <summary>
    /// Base interface of argument passed to OnCloseGame callback.
    /// </summary>
    public interface ICloseGameCallInfo : ITypedCallInfo<ICloseRequest>
    {
        #region Properties
        /// <summary>Not used.</summary>
        int ActorCount { get; }
        /// <summary>
        /// Indicates if the room is closed from the start due to a failure when creating it.
        /// </summary>
        bool FailedOnCreate { get; }
        #endregion

        /// <summary>
        /// Processing of the call is deferred and plugin is nolonger interrupted.
        /// </summary>
        [Obsolete("Method should not be used with new HttpRequest/CreateOneTimeTimer methods")]
        void Defer();
    }
    /// <summary>
    /// Base interface of argument passed to BeforeCloseGame callback.
    /// </summary>
    public interface IBeforeCloseGameCallInfo : ITypedCallInfo<ICloseRequest>
    {
        /// <summary>
        /// Indicates if the room is closed from the start due to a failure when creating it.
        /// </summary>
        bool FailedOnCreate { get; }
    }
    /// <summary>
    /// Base interface of argument passed to BeforeJoin callback.
    /// </summary>
    public interface IBeforeJoinGameCallInfo : ITypedCallInfo<IJoinGameRequest>
    {
    }
    /// <summary>
    /// Base interface of argument passed to OnJoinGame callback.
    /// </summary>
    public interface IJoinGameCallInfo : ITypedCallInfo<IJoinGameRequest>
    {
        #region Properties
        /// <summary>Number of the actor joining the room.</summary>
        int ActorNr { get; }
        /// <summary>
        /// Join parameters added by server to indicate how this join call should be processed.
        /// </summary>
        ProcessJoinParams JoinParams { get; }

        #endregion
    }

#if PLUGINS_0_9
    [Obsolete("Use IJoinGameCallInfo instead")]
    public interface IJoinCallInfo : IJoinGameCallInfo
    {
    }
#endif
    /// <summary>
    /// Base interface of argument passed to OnRaiseEvent plugin callback.
    /// </summary>
    public interface IRaiseEventCallInfo : ITypedCallInfo<IRaiseEventRequest>
    {
        #region Properties
        /// <summary>Number of the actor triggering the callback call.</summary>
        int ActorNr { get; }
        #endregion

        #region Methods
        /// <summary>
        /// Silently skips processing. The Op RaiseEvent is ignored.
        /// </summary>
        void Cancel();
        /// <summary>
        /// Processing of the call is deferred and plugin is nolonger interrupted.
        /// </summary>
        [Obsolete("Method should not be used with new HttpRequest/CreateOneTimeTimer methods")]
        void Defer();

        #endregion
    }
    /// <summary>
    /// Base interface of argument passed to BeforeSetProperties plugin callback.
    /// </summary>
    public interface IBeforeSetPropertiesCallInfo : ITypedCallInfo<ISetPropertiesRequest>
    {
        #region Properties
        /// <summary>Number of the actor triggering the callback call.</summary>
        int ActorNr { get; }

        #endregion

        #region Methods
        /// <summary>
        /// Silently skips processing. The Op SetProperties is ignored.
        /// </summary>
        void Cancel();
        /// <summary>
        /// Processing of the call is deferred and plugin is nolonger interrupted.
        /// </summary>
        [Obsolete("Method should not be used with new HttpRequest/CreateOneTimeTimer methods")]
        void Defer();

        #endregion
    }

    //public static class SetPropertiesResult
    //{
    //    public const byte FAILED = 0;
    //    public const byte SUCCEDED = 1;
    //}

    /// <summary>
    /// Base interface of argument passed to OnSetProperties plugin callback.
    /// </summary>
    public interface ISetPropertiesCallInfo : ITypedCallInfo<ISetPropertiesRequest>
    {
        #region Properties
        /// <summary>Number of the actor triggering the callback call.</summary>
        int ActorNr { get; }

        //byte OperationStatus { get; }
        #endregion
    }

    /// <summary>
    /// Base interface of argument passed to OnSetPropertiesFailed callback.
    /// </summary>
    public interface ISetPropertiesFailedCallInfo : ITypedCallInfo<ISetPropertiesRequest>
    {
        #region Properties
        /// <summary>Number of the actor triggering the callback call.</summary>
        int ActorNr { get; }
        #endregion
    }

//#if PLUGINS_0_9
    /// <summary>
    /// Base interface of argument passed to OnDisconnect callback.
    /// </summary>
    public interface IDisconnectCallInfo : ITypedCallInfo<IOperationRequest>
    {
        #region Properties
        /// <summary>Number of the actor triggering the callback call.</summary>
        int ActorNr { get; }

        #endregion
    }
//#endif
}