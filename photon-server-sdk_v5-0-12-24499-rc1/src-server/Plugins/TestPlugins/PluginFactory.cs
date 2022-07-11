
using System;
using System.Collections.Generic;
using Photon.Hive.Plugin;
using Photon.Hive.Plugin.WebHooks;
using TestPlugins.OldHttp;
using TestPlugins.OldHttp.WebHooks;

namespace TestPlugins
{
    public class PluginFactory : IPluginFactory2
    {
        private static PluginBase pluginInstance;

        private HttpPluginsTestListener listener;

        public void SetFactoryHost(IFactoryHost factoryHost, FactoryParams factoryParams)
        {
            this.listener = new HttpPluginsTestListener(factoryHost.CreateLogger("TestPlugins:HttpTestListener"));
        }
        public IGamePlugin Create(IPluginHost sink, string pluginName, Dictionary<string, string> config, out string errorMsg)
        {
            var prefix = sink.GameId.Contains("_") ? sink.GameId.Substring(0, sink.GameId.IndexOf('_')) : string.Empty; 

            IGamePlugin plugin;
            switch (pluginName)
            {
                case "TypesTestPlugin":
                    plugin = new TypesTestPlugin();
                    break;
                case "SetPropertiesCheckPlugin":
                    plugin = new SetPropertiesCheckPlugin();
                    break;
                case "JoinFailuresCheckPlugin":
                    plugin = new JoinFailuresCheckPlugin();
                    break;
                case "RaiseEventChecksPlugin":
                    plugin = new RaiseEventChecksPlugin();
                    break;
                case "BasicTestsPlugin":
                    plugin = new BasicTestsPlugin();
                    break;
                case "SameInstancePlugin":
                    if (pluginInstance == null)
                    {
                        pluginInstance = new SameInstancePlugin();
                    }
                    plugin = pluginInstance;
                    break;
                case "SaveLoadStateTestPlugin":
                    plugin = new SaveLoadStateTestPlugin();
                    break;
                case "ScheduleBroadcastTestPlugin":
                    plugin = new ScheduleBroadcastTestPlugin();
                    break;
                case "ScheduleSetPropertiesTestPlugin":
                    plugin = new ScheduleSetPropertiesTestPlugin();
                    break;
                case "Webhooks":
                    plugin = new WebHooksPlugin();
                    plugin.SetupInstance(sink, config, out errorMsg);
                    return plugin;
                case "MasterClientIdPlugin":
                    plugin = new MasterClientIdPlugin();
                    break;
                case "CustomTypeCheckPlugin":
                    plugin = new CustomTypeCheckPlugin();
                    break;
                case "SyncAsyncHttpTestPlugin":
                    plugin = new SyncAsyncHttpTestPlugin();
                    break;
                case "SyncAsyncHttpTestPluginOldHttp":
                    plugin = new SyncAsyncHttpTestPluginOldHttp();
                    break;
                case "CustomTypeMapperPlugin":
                    plugin = new CustomTypeMapperPlugin();
                    break;
                case "JoinExceptionsPlugin":
                    plugin = new JoinExceptionsPlugin();
                    break;
                case "CheckSecurePlugin":
                {
                    plugin = new SecureCheckPlugin("CheckSecurePlugin");
                    config = new Dictionary<string, string>
                    {
                        {"BaseUrl", "https://wt-e4c18d407aa73a40e4182aaf00a2a2eb-0.run.webtask.io/realtime-webhooks-1.2"},
                        {"PathJoin", "JoinGameSecure"},
                        {"PathEvent", "RaiseEventSecure"},
                        {"PathCreate", "CreateGameSecure"},
                        {"PathGameProperties", "SetPropertiesSecure"},
                    };
                    break;
                }
                case "StrictModeFailurePlugin":
                    plugin = new StrictModeFailurePlugin();
                    break;
                case "StrictModeFailurePluginOldHttp":
                    plugin = new StrictModeFailurePluginOldHttp();
                    break;
                case "SetStateAfterContinueTestPlugin":
                    plugin = new SetStateAfterContinueTestPlugin();
                    break;
                case "ErrorPlugin":
                    plugin = new ErrorPlugin("Error plugin is used");
                    break;
                case "StripedGameStatePlugin":
                    plugin = new StripedGameStatePlugin();
                    break;
                case "NullRefPlugin":
                    errorMsg = "NullRefPlugin is called";
                    return null;

                case "ExceptionPlugin":
                    errorMsg = "Exception plugin is called";
                    throw new Exception("From exception:" + errorMsg);
                case "BanTestPlugin":
                    plugin = new BanTestPlugin();
                    break;
                case "ChangeGamePropertiesOnJoinPlugin":
                    plugin = new ChangeGamePropertiesOnJoinPlugin();
                    break;
                case "RemovingActorPlugin":
                    plugin = new RemovingActorPlugin();
                    break;
                case "BroadcastEventPlugin":
                    plugin = new BroadcastEventPlugin();
                    break;
                case "LongOnClosePlugin":
                    plugin = new LongOnClosePlugin();
                    break;
                case "LongOnClosePluginWithPersistence":
                    plugin = new LongOnClosePluginWithPersistence();
                    break;
                case "WrongUrlTestPlugin":
                    plugin = new WrongUrlTestPlugin();
                    break;
                    case "OnLeaveExceptionsPlugin":
                    plugin = new OnLeaveExceptionsPlugin();
                    break;
                case "CacheOpPlugin":
                    plugin = new CacheOpPlugin();
                    break;
                case "AllMethosCallHttpTestPlugin":
                    plugin = new AllMethosCallHttpTestPlugin();
                    break;
                case "OneTimeTimerTestPlugin":
                    plugin = new OneTimeTimerTestPlugin();
                    break;
                case "OnRaiseEventTimerTestPlugin":
                    plugin = new OnRaiseEventTimerTestPlugin();
                    break;
                case "ActorPropertiesBroadcastDuringJoin":
                    plugin = new ActorPropertiesBroadcastDuringJoin();
                    break;
                case "ApiConsistenceTestPlugin":
                    plugin = new ApiConsistenceTestPlugin();
                    break;
                case "CorrectOnLeaveTestPlugin":
                    plugin = new CorrectOnLeaveTestPlugin();
                    break;
                case "SetPropertiesToInActiveActorTestPlugin":
                    plugin = new SetPropertiesToInActiveActorTestPlugin();
                    break;
                case "SetPlayerTTLAndEmptyRoomTTLPlugin":
                    plugin = new SetPlayerTTLAndEmptyRoomTTLPlugin();
                    break;
                case "TBWebhooks":
                    plugin = new TBWebHooksPlugin();
                    config = new Dictionary<string, string>
                    {
                        {"BaseUrl", "http://localhost:55557/realtime-webhooks-1.2"},
                        {"PathJoin", "GameJoin"},
                        {"PathEvent", "RaiseEvent"},
                        {"PathCreate", "GameCreate"},
                        {"PathClose", "GameClose"},
                        {"PathLoad", "GameLoad"},
                        {"IsPersistent", "True"},
                    };
                    break;
                case "TBWebhooksOldHttp":
                    plugin = new TBWebHooksPluginOldHttp();
                    config = new Dictionary<string, string>
                    {
                        {"BaseUrl", "http://localhost:55557/realtime-webhooks-1.2"},
                        {"PathJoin", "GameJoin"},
                        {"PathEvent", "RaiseEvent"},
                        {"PathCreate", "GameCreate"},
                        {"PathClose", "GameClose"},
                        {"PathLoad", "GameLoad"},
                        {"IsPersistent", "True"},
                    };
                    break;

                case "HttpResponseHeadersPlugin":
                    plugin = new HttpResponseHeadersPlugin();
                    break;
                case "HttpMethodTestPlugin":
                    plugin = new HttpMethodTestPlugin();
                    break;
                case "BrokenPropsDuringGameCreationPlugin":
                    plugin = new BrokenPropsDuringGameCreationPlugin();
                    break;
                case "HttpRequestNullCallInfoPlugin":
                    plugin = new HttpRequestNullCallInfoPlugin();
                    break;
                case "OneTimeTimerNullCallInfoPlugin":
                    plugin = new OneTimeTimerNullCallInfoPlugin();
                    break;
                case "NewPropertyPlugin":
                    plugin = new NewPropertyPlugin();
                    break;
                case "HttpLimitTestPlugin":
                    plugin = new HttpLimitTestPlugin(this.listener);
                    break;
                default:
                    switch (prefix)
                    {
                        case "ForwardPlugin1":
                            plugin = new WebHooksPlugin();
                            config = new Dictionary<string, string> {{"BaseUrl", "X"}};
                            break;
                        case "ForwardPlugin2":
                            if (string.IsNullOrEmpty(pluginName))
                            {
                                plugin = new PluginBase();
                            }
                            else
                            {
                                plugin = new WebHooksPlugin();
                                config = new Dictionary<string, string>
                                {
                                    {"BaseUrl", "https://wt-e4c18d407aa73a40e4182aaf00a2a2eb-0.run.webtask.io/"},
                                    {"PathClose", "GameClose"},
                                    {"PathCreate", "GameCreate"},
                                };
                            }
                            break;
                        default:
                            if (string.IsNullOrEmpty(pluginName))
                            {
                                plugin = new PluginBase();
                            }
                            else
                            {
                                plugin = new ErrorPlugin(string.Format("PluginFactory: Can not find plugin with name:'{0}'", pluginName));
                            }
                            break;
                    }
                    break;
            }

            if (plugin.SetupInstance(sink, config, out errorMsg))
            {
                return  plugin;
            }

            return null;
        }
    }

    class SameInstancePlugin : TestPluginBase
    {
    }
}
