using System;

using Microsoft.Extensions.Configuration;

using Photon.Common.LoadBalancer.LoadShedding.Configuration;

namespace Photon.Common.LoadBalancer.Prediction.Configuration
{
    internal static class ConfigurationLoader 
    {
        public static bool TryLoadFromFile(string fileName, out LoadPredictionSystemSection section, out string message)
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

        private static LoadPredictionSystemSection LoadFromFile(string fileName)
        {
            var cb = new ConfigurationBuilder();
            cb.AddXmlFile(fileName, true);

            var configuration = cb.Build();

            var section = configuration.GetSection("LoadPredictionSystem").Get<LoadPredictionSystemSection>();
            if (section == null)
            {
                return null;
            }

            section.FeedbackControllers.Clear();

            var levels = configuration.GetSection("LoadPredictionSystem:FeedbackControllers:Controller");
            foreach (var controllerSection in levels.GetChildren())
            {
                var controller = controllerSection.Get<FeedbackControllerElement>();

                if (controller != null)
                {
                    controller.Deserialize(controllerSection);
                    section.FeedbackControllers.Add(controller);
                }
            }

            return section;
        }
    }
}
