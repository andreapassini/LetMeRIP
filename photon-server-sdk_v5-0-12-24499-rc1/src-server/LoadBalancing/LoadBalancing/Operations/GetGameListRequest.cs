// --------------------------------------------------------------------------------------------------------------------
// <copyright file="GetGameListRequest.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the GetGameListRequest type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------
namespace Photon.LoadBalancing.Operations
{
    #region using directives

    using System;
    using ExitGames.Logging;
    using Photon.LoadBalancing.Common;
    using Photon.LoadBalancing.MasterServer;
    using Photon.SocketServer.Diagnostics;
    using Photon.Hive.Operations;
    using Photon.SocketServer;
    using Photon.SocketServer.Rpc;

    #endregion

    public class GetGameListRequest : Operation
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();
        private static readonly LogCountGuard LogGuard = new LogCountGuard(new TimeSpan(0, 0, 0, 10));

        public GetGameListRequest(IRpcProtocol protocol, OperationRequest operationRequest)
            : base(protocol, operationRequest)
        {
            if (this.IsValid)
            {
                this.CheckQueryData();
            }
        }

        public GetGameListRequest()
        {
        }

        [DataMember(Code = (byte)ParameterKey.Data, IsOptional = true)]
        public string QueryData { get; set; }

        [DataMember(Code = (byte)ParameterCode.LobbyName, IsOptional = true)]
        public string LobbyName { get; set; }

        [DataMember(Code = (byte)ParameterCode.LobbyType, IsOptional = true)]
        public byte LobbyType { get; set; }

        private void CheckQueryData()
        {
            string errorMsg;
            if (!this.QueryHasSQLInjection(out errorMsg))
            {
                return;
            }

            if (!MasterServerSettings.Default.OnlyLogQueryDataErrors)
            {
                this.isValid = false;
                this.errorMessage = errorMsg;
            }

            log.WarnFormat(LogGuard, "QueryData contains SQL injection. query:{0}. ErrorMsg:{1}", this.QueryData, errorMsg);
        }

        private bool QueryHasSQLInjection(out string errorMsg)
        {
            if (string.IsNullOrEmpty(this.QueryData))
            {
                errorMsg = string.Empty;
                return false;
            }

            if (this.QueryData.Contains(";"))
            {
                errorMsg = LBErrorMessages.NotAllowedSemicolonInQueryData;
                return true;
            }

            var wrongWords = MasterServerSettings.Default.SqlQueryBlockList.Split(';');
            foreach (var word in wrongWords)
            {
                if (this.QueryData.Contains(word))
                {
                    errorMsg = string.Format(LBErrorMessages.NotAllowedWordInQueryData, word);
                    return true;
                }
            }
            errorMsg = string.Empty;
            return false;
        }
    }
}
