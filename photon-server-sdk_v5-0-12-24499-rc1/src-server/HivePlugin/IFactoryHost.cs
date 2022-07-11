namespace Photon.Hive.Plugin
{
    public interface IFactoryHost
    {
        IPluginFiber CreateFiber();

        IPluginLogger CreateLogger(string loggerName);
    }
}