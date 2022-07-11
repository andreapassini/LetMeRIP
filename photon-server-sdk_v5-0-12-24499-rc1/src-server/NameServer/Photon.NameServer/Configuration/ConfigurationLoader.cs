// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ConfigurationLoader.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the ConfigurationLoader type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Photon.NameServer.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    using Newtonsoft.Json;

    public class ConfigurationLoader 
    {
        public static bool TryLoadFromFile(string fileName, out List<Node> config, out string message)
        {
            config = null;
            message = string.Empty;

            try
            {
                config = LoadFromFile(fileName);
                return true;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }
        }

        public static List<Node> LoadFromFile(string fileName)
        {
            NodeList result; 
            using (var reader = new StreamReader(fileName))
            {
                string json = reader.ReadToEnd();
                result = JsonConvert.DeserializeObject<NodeList>(json); 
            }

            return result.Nodes; 
        }
    }
}
