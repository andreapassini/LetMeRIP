using ExitGames.Concurrency.Fibers;
using Photon.Plugins.Common;

namespace Photon.Hive.Plugin
{
    public class FactoryHost : IFactoryHost
    {
        private readonly IPluginLogMessagesCounter logMessagesCounter;

        public FactoryHost(IPluginLogMessagesCounter logMessagesCounter)
        {
            this.logMessagesCounter = logMessagesCounter;
        }

        public IPluginFiber CreateFiber()
        {
            var fiber = new PoolFiber();
            fiber.Start();
            return new PluginFiber(fiber);
        }

        public IPluginLogger CreateLogger(string loggerName)
        {
            return new PluginLogger(loggerName, this.logMessagesCounter);
        }
    }
}
