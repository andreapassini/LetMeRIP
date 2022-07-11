using Photon.Hive.Plugin;

namespace TestPlugins
{
    internal class NewPropertyPlugin : TestPluginBase
    {
        public override void OnCreateGame(ICreateGameCallInfo info)
        {
            info.Request.GameProperties.Add("NewProperty", "xxx");
            base.OnCreateGame(info);
        }
    }
}
