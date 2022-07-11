// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PluginElement.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Photon.Common.Plugins.Configuration
{
    public class PluginElement
    {
        #region Properties

        public Dictionary<string, string> CustomAttributes { get; } = new Dictionary<string, string>();

        [Required]
        public string Name { get; set; }

        public string Version { get; set; } = "";

        [Required]
        public string Type { get; set; }

        [Required]
        public string AssemblyName { get; set; }

        public string Path { get; set; }
        #endregion

    }
}