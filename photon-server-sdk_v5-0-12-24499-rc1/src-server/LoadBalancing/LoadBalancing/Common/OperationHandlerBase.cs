// --------------------------------------------------------------------------------------------------------------------
// <copyright file="OperationHandlerBase.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Provides basic methods for <see cref="IOperationHandler" /> implementations.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System;
using ExitGames.Logging;
using Photon.Common;
using Photon.SocketServer;
using Photon.SocketServer.Diagnostics;
using Photon.SocketServer.Rpc;

namespace Photon.LoadBalancing.Common
{
    #region using directives

    

    #endregion

    /// <summary>
    ///   Provides basic methods for <see cref = "IOperationHandler" /> implementations.
    /// </summary>
    public abstract class OperationHandlerBase : IOperationHandler
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();
        private static readonly LogCountGuard exceptionLogGuard = new LogCountGuard(new TimeSpan(0, 1, 0));

        #region Public Methods

        public static OperationResponse HandleInvalidOperation(Operation operation, ILogger logger)
        {
            string errorMessage = operation.GetErrorMessage();

            if (logger != null && logger.IsDebugEnabled)
            {
                logger.DebugFormat("Invalid operation: OpCode={0}; {1}", operation.OperationRequest.OperationCode, errorMessage);
            }

            return new OperationResponse(operation.OperationRequest.OperationCode)
            {
                ReturnCode = (short) ErrorCode.OperationInvalid,
                DebugMessage = errorMessage
            };
        }

        protected static OperationResponse HandleUnknownOperationCode(OperationRequest operationRequest, ILogger logger)
        {
            if (logger != null && logger.IsDebugEnabled)
            {
                logger.DebugFormat("Unknown operation code: OpCode={0}", operationRequest.OperationCode);
            }

            return new OperationResponse(operationRequest.OperationCode)
            {
                ReturnCode = (short) ErrorCode.OperationInvalid,
                DebugMessage = LBErrorMessages.UnknownOperationCode
            };
        }

        public static bool ValidateOperation(Operation operation, ILogger logger, out OperationResponse response)
        {
            if (operation.IsValid)
            {
                response = null;
                return true;
            }

            response = HandleInvalidOperation(operation, logger);
            return false;
        }

        protected abstract OperationResponse OnOperationRequest(PeerBase peer, OperationRequest operationRequest, SendParameters sendParameters);


        #endregion

        #region Implemented Interfaces

        #region IOperationHandler

        public virtual void OnDisconnect(PeerBase peer)
        {
        }

        public virtual void OnDisconnectByOtherPeer(PeerBase peer)
        {
        }

        OperationResponse IOperationHandler.OnOperationRequest(PeerBase peer, OperationRequest operationRequest, SendParameters sendParameters)
        {
            try
            {
                return this.OnOperationRequest(peer, operationRequest, sendParameters);
            }
            catch (Exception e)
            {
                /// we do not use LogExtensions log methods here to reduce cpu loading if message will be skipped anyway
                if (exceptionLogGuard.IncrementAndCheck())
                {
                    var message = LogExtensions.AddSkippedMessagesInfo(exceptionLogGuard, 
                        $"OnOperationRequest Exception: p:{peer}, Exception Msg:{e.Message}, request:{ValueToString.OperationToString(operationRequest)}");
                    log.Error(message, e);
                }

                return new OperationResponse(operationRequest.OperationCode)
                {
                    ReturnCode = (short) ErrorCode.InternalServerError,
                    DebugMessage = e.ToString()
                };
            }
        }

        #endregion

        #endregion
    }
}