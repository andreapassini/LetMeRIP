// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ServerConfig.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the ServerConfig type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Photon.NameServer.Configuration
{
    using System;

    /// <remarks/>
    [Serializable()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    [System.Xml.Serialization.XmlRootAttribute(Namespace = "", IsNullable = false)]
    public partial class ServerConfig
    {
        public string ServerType;
        public int ServerState;
        public string Cloud;
        public string Region;
        public string Cluster;
    }
}
