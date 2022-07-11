// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PluginInfo.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the PluginInfo type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using ExitGames.Logging;
using System.Collections.Generic;

namespace Photon.Common.Plugins
{
    public class PluginInfo
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private string customPath = null;

        #region Properties

        public Dictionary<string, string> ConfigParams { get; set; }

        public string Type { get; set; }

        public string Name { get; set; }

        public string AssemblyName { get; set; }

        /// <summary>
        /// Default Path is resolved to:
        /// \verbatim Plugins\{Name}\{Version}\bin\{AssemblyName} \endverbatim
        /// else if <see cref="SetCustomPath"/> was set succesfully:
        /// \verbatim Plugins\Custom\{customPath}\{Version}\bin\{AssemblyName} \endverbatim
        /// </summary>
        public string Path
        {
            get
            {
                if (this.customPath != null)
                {
                    return System.IO.Path.Combine(@"Plugins/Custom", System.IO.Path.Combine(this.customPath, string.Format(@"{0}/bin/{1}", this.Version, this.AssemblyName)));                        
                }
                else
                {
                    return string.Format(@"Plugins/{0}/{1}/bin/{2}", this.Name, this.Version, this.AssemblyName);
                }
            }
        }

        public string Version { get; set; }

        #endregion

        /// <summary>
        /// Sets the customPath used to compute <see cref="Path"/>, it will only be set (returning true) if NOT rooted, e.g. starts with c:\ or \.
        /// </summary>
        /// <param name="relativePath"></param>
        /// <returns></returns>
        public bool SetCustomPath(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath) || System.IO.Path.IsPathRooted(relativePath))
            {
                return false;
            }
            this.customPath = relativePath.Replace('\\', '/');

            log.WarnFormat($"Got path '{relativePath}'. path after '\\'s replacement: '{this.customPath}'");
            return true;
        }
    }
}
