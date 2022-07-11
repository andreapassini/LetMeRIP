// -------------------------------------------------public -------------------------------------------------------------------
// <copyright file="OperationCode.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the OperationCode type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Photon.NameServer.Operations
{
    public enum OperationCode : byte
    {
        AuthOnce = 231,
        // from Loadbalancing / Hive
        Authenticate = 230,

        GetRegionList = 220,
    }
}
