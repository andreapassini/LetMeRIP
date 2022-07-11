namespace Photon.Plugins.Common
{
    public interface IPluginLogMessagesCounter
    {
        void IncrementWarnsCount();
        void IncrementErrorsCount();
        void IncrementFatalsCount();
    }
}