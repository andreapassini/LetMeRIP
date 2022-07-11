using System;

using Microsoft.Extensions.Configuration;

namespace Photon.Common.LoadBalancer.LoadShedding.Configuration
{
    internal class ConfigurationLoader
    {
        public static bool TryLoadFromFile(string fileName, out FeedbackControlSystemSection section, out string message)
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

        public static FeedbackControlSystemSection LoadFromFile(string fileName)
        {
            var cb = new ConfigurationBuilder();
            cb.AddXmlFile(fileName, true);

            var configuration = cb.Build();

            var section = configuration.GetSection("FeedbackControlSystem").Get<FeedbackControlSystemSection>();
            if (section == null)
            {
                return null;
            }

            section.FeedbackControllers.Clear();

            var levels = configuration.GetSection("FeedbackControlSystem:FeedbackControllers:Controller");
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
