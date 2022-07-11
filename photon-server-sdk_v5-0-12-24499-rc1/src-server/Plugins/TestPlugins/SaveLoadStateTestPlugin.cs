using System.IO;
using Newtonsoft.Json;
using Photon.Hive.Plugin;

namespace TestPlugins
{
    class SaveLoadStateTestPlugin : TestPluginBase
    {

        public override bool IsPersistent
        {
            get
            {
                return true;
            }
        }

        public override void OnCloseGame(ICloseGameCallInfo info)
        {
            base.OnCloseGame(info);
            var state = this.PluginHost.GetSerializableGameState();
            WriteGameStateToFile(this.PluginHost.GameId, state);
        }

        public override void OnCreateGame(ICreateGameCallInfo info)
        {
            var state = ReadGameStateFromFile(this.PluginHost.GameId);
            if (state != null)
            {
                this.PluginHost.SetGameState(state);
            }
            else
            {
                this.PluginHost.LogDebug(string.Format("Failed to find file with state for game {0}", this.PluginHost.GameId));
            }
            base.OnCreateGame(info);
        }

        private void WriteGameStateToFile(string name, SerializableGameState state)
        {
            var tempPath = System.IO.Path.GetTempPath();
            var fileName = System.IO.Path.Combine(tempPath, name + ".txt");
            var strState = JsonConvert.SerializeObject(state);

            // Append text to an existing file named "WriteLines.txt".
            using (var outputFile = new StreamWriter(fileName, false))
            {
                outputFile.WriteLine(strState);
                outputFile.Flush();
            }
        }

        protected SerializableGameState ReadGameStateFromFile(string name)
        {
            var tempPath = System.IO.Path.GetTempPath();
            var fileName = System.IO.Path.Combine(tempPath, name + ".txt");
            if (!File.Exists(fileName))
            {
                return null;
            }

            using (var inputFile = new StreamReader(fileName))
            {
                var strState = inputFile.ReadToEnd();
                return JsonConvert.DeserializeObject<SerializableGameState>(strState);
            }
        }


    }

    class SetStateAfterContinueTestPlugin : SaveLoadStateTestPlugin
    {
        public override string Name
        {
            get { return this.GetType().Name; }
        }

        public override void OnCreateGame(ICreateGameCallInfo info)
        {
            base.OnCreateGame(info);

            var state = ReadGameStateFromFile(this.PluginHost.GameId);
            if (state != null)
            {
                if (this.PluginHost.SetGameState(state))
                {
                    this.PluginHost.BroadcastErrorInfoEvent("SetGameState after call to info.Continue succeeded");
                }
                else
                {
                    this.BroadcastEvent(123, null);
                }
            }
            else
            {
                this.PluginHost.LogDebug(string.Format("Failed to find file with state for game {0}", this.PluginHost.GameId));
            }
        }
    }
}
