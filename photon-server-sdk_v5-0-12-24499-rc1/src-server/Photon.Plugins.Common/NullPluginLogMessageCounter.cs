namespace Photon.Plugins.Common
{
    public class NullPluginLogMessageCounter : IPluginLogMessagesCounter
    {
        public static readonly NullPluginLogMessageCounter Instance = new NullPluginLogMessageCounter();

        public void IncrementWarnsCount()
        {
        }

        public void IncrementErrorsCount()
        {
        }

        public void IncrementFatalsCount()
        {
        }
    }
}