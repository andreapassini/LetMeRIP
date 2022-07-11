// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PluginSettings.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Collections.Generic;

namespace Photon.Common.Plugins.Configuration
{
    public class PluginSettings
    {
        #region Properties

        public bool Enabled { get; set; } = false;

        public List<PluginElement> Plugins { get; } = new List<PluginElement>();

        #endregion
    }

}