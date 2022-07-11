// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ConfigurationLoader.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System;

using Microsoft.Extensions.Configuration;

namespace Photon.Common.LoadBalancer.Configuration
{
    internal class ConfigurationLoader
    {
        public static bool TryLoadFromFile(string fileName, out LoadBalancerSection section, out string message)
        {
            section = null;
            message = string.Empty;

            try
            {
                section = LoadFromFile(fileName);
                return true;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }
        }

        public static LoadBalancerSection LoadFromFile(string fileName)
        {
            var cb = new ConfigurationBuilder();
            cb.AddXmlFile(fileName, true);

            var configuration = cb.Build();

            var section = configuration.GetSection("LoadBalancer").Get<LoadBalancerSection>();
            if (section == null)
            {
                return null;
            }

            section.LoadBalancerWeights.Clear();

            var levels = configuration.GetSection("LoadBalancer:LoadBalancerWeights:Level");
            foreach (var levelSection in levels.GetChildren())
            {
                var level = levelSection.Get<LoadBalancerWeight>();

                if (level != null)
                {
                    section.LoadBalancerWeights.Add(level);
                }
            }

            return section;
        }
    }
}
