using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using ExitGames.Logging;
using Photon.Common.LoadBalancer.LoadShedding;
using Photon.Common.LoadBalancer.LoadShedding.Configuration;
using Photon.Common.LoadBalancer.Prediction.Configuration;
using Photon.SocketServer.Annotations;
using ConfigurationLoader = Photon.Common.LoadBalancer.Prediction.Configuration.ConfigurationLoader;
using DefaultConfiguration = Photon.Common.LoadBalancer.Prediction.Configuration.DefaultConfiguration;

namespace Photon.Common.LoadBalancer.Prediction
{
    public class LoadStatsCollector
    {
        #region Constants and Fields

        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private readonly string rootPath;
        private readonly string configFileName;

        private SortedDictionary<FeedbackLevel, FeedbackLevelData> thresholdValues;

        private const int MinimalLevelSize = 10;

        private readonly float initialFactor;

        #endregion

        #region .ctor

        public LoadStatsCollector(string applicationRootPath, string predictionConfig, float factor = 1.0f)
            :this(factor)
        {
            this.rootPath = applicationRootPath;
            this.configFileName = predictionConfig;

            this.Initialize(predictionConfig);
        }

        public LoadStatsCollector(float factor = 1.0f)
        {
            this.initialFactor = factor;
            this.thresholdValues = DefaultConfiguration.GetDefaultControllers();
        }

        #endregion

        #region Publics

        [PublicAPI]
        public Dictionary<byte, int[]> GetPredictionData()
        {
            return this.GetControllersThresholds();
        }

        [PublicAPI]
        public bool SaveToFile(string dir, string fileName)
        {
            var fullName = Path.Combine(dir, fileName);
            if (!this.SaveToFile(fullName))
            {
                log.WarnFormat("Error during saving of config. {0}", fullName);
                return false;
            }
            return true;
        }

        [PublicAPI]
        public void SaveToFile()
        {
            if (this.SaveToFile(this.rootPath, this.configFileName + ".last"))
            {
                var sourceFile = Path.Combine(this.rootPath, this.configFileName);
                var lastFile = Path.Combine(this.rootPath, this.configFileName + ".last");
                if (File.Exists(sourceFile))
                {
                    File.Replace(lastFile, sourceFile, null);
                }
                else
                {
                    File.Copy(lastFile, sourceFile);
                    File.Delete(lastFile);
                }
            }
        }

        public void UpdatePrediction(int reportedPeerCount, FeedbackLevel reportedFeedbackLevel, out Dictionary<byte, int[]> updatedLevels)
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Prediction update for level:{0}, input:{1}, factor:{2}",
                    reportedPeerCount, reportedFeedbackLevel, this.initialFactor);
            }

            Dictionary<FeedbackLevel, FeedbackLevelData> updatedLevelData = null;
            if (!this.UpdateThresholdsInt(reportedPeerCount, reportedFeedbackLevel, this.initialFactor, ref updatedLevelData))
            {
                updatedLevels = null;
                return;
            }

            if (updatedLevelData != null && updatedLevelData.Count != 0)
            {
                var levelDataArray = new int[updatedLevelData.Count * 3];

                updatedLevels = new Dictionary<byte, int[]>() { { (byte)FeedbackName.PeerCount, levelDataArray } };

                var index = 0;
                foreach (var feedbackLevelData in updatedLevelData)
                {
                    levelDataArray[index * 3 + 0] = (int)feedbackLevelData.Key;
                    levelDataArray[index * 3 + 1] = feedbackLevelData.Value.UpperBound;
                    levelDataArray[index * 3 + 2] = feedbackLevelData.Value.LowerBound;
                    ++index;
                }
            }
            else
            {
                updatedLevels = null;
            }
        }

        #endregion

        #region Privates
        private bool SaveToFile(string fileName)
        {
            try
            {
                using (var fileStream = File.Open(fileName, FileMode.OpenOrCreate))
                {
                    fileStream.Seek(0, SeekOrigin.Begin);

                    var settings = new XmlWriterSettings
                    {
                        Indent = true,
                        IndentChars = "\t",
                        CheckCharacters = false
                    };
                    var xmlWriter = XmlWriter.Create(fileStream, settings);


                    xmlWriter.WriteStartDocument();

                    this.Serialize(xmlWriter);

                    xmlWriter.WriteEndDocument();

                    xmlWriter.Close();
                    fileStream.Close();

                    return true;
                }
            }
            catch (Exception e)
            {
                log.Error(e);
            }
            return false;
        }

        private bool UpdateThresholdsInt(int input, FeedbackLevel level, float factor, 
            ref Dictionary<FeedbackLevel, FeedbackLevelData> updatedLevels)
        {
            var expectedLevel = FeedbackLevel.Lowest;

            foreach (var thresholdValue in this.thresholdValues)
            {
                if (thresholdValue.Value.UpperBound >= input)
                {
                    expectedLevel = thresholdValue.Key;
                    break;
                }

                if (thresholdValue.Key == FeedbackLevel.Highest)
                {
                    expectedLevel = FeedbackLevel.Highest;
                    break;
                }
            }

            if (expectedLevel == level)
            {
                return false;
            }

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Level updating. input:{0}, level:{1}, factor:{2}", input, level, factor);
            }

            if (updatedLevels == null)
            {
                updatedLevels = new Dictionary<FeedbackLevel, FeedbackLevelData>();
            }

            if (expectedLevel < level)
            {
                var next = this.GetNextExistingLowerLevel(level);

                this.SafeUpdateThresholdsDownward(level, next, input, factor, ref updatedLevels);
            }
            else
            {
                var next = this.GetNextExistingHigherLevel(level);

                this.SafeUpdateThresholdsUpward(level, next, input, updatedLevels, factor);
            }
            return true;
        }

        private void SafeUpdateThresholdsUpward(FeedbackLevel level, FeedbackLevel next, int input, Dictionary<FeedbackLevel, FeedbackLevelData> updatedLevels, float factor)
        {
            FeedbackLevel realLevel;
            this.GetExistingLevel(level, out realLevel);

            FeedbackLevelData values, hierLevelValues;
            if (!this.UpdateLevelValues(realLevel, input, factor, out values, out hierLevelValues, updatedLevels))
            {
                return;
            }

            if (level == next)
            {
                return;
            }


            var upDownDiff = hierLevelValues.UpperBound - values.UpperBound;
            if (MinimalLevelSize > upDownDiff)
            {
                this.UpdateThresholdsInt(values.UpperBound + MinimalLevelSize, next, 1.0f, ref updatedLevels);
            }
        }

        private bool UpdateLevelValues(FeedbackLevel level, int input, float factor, out FeedbackLevelData upDownValues, out FeedbackLevelData higherLevelValues, Dictionary<FeedbackLevel, FeedbackLevelData> updatedLevels)
        {
            higherLevelValues = new FeedbackLevelData(-1, -1);
            if (!this.thresholdValues.TryGetValue(level, out upDownValues))
            {
                log.WarnFormat("Can not find values for level {0}", level);
                return false;
            }

            if (input < 0)
            {
                input = 0;
            }

            if (level == FeedbackLevel.Highest)
            {
                if (log.IsDebugEnabled)
                {
                    log.Debug("We do not update upper bound for 'Highest' level");
                }
                //upDownValues.UpperBound = (int)(factor * (input - upDownValues.UpperBound)) + upDownValues.UpperBound;
                //this.thresholdValues[level] = upDownValues;
                //updatedLevels[level] = upDownValues;
                return true;
            }

            var nextLevel = this.GetNextExistingHigherLevel(level);

            if (!this.thresholdValues.TryGetValue(nextLevel, out higherLevelValues))
            {
                log.WarnFormat("Can not find values for level {0}", nextLevel);
                return false;
            }

            var upDownDiff = upDownValues.UpperBound - higherLevelValues.LowerBound;
            upDownValues.UpperBound = (int)(factor * (input - upDownValues.UpperBound)) + upDownValues.UpperBound;
            higherLevelValues.LowerBound = upDownValues.UpperBound - upDownDiff;
            if (higherLevelValues.LowerBound > upDownValues.UpperBound)
            {
                higherLevelValues.LowerBound = upDownValues.UpperBound;
            }

            this.thresholdValues[level] = upDownValues;
            this.thresholdValues[nextLevel] = higherLevelValues;
            updatedLevels[level] = upDownValues;
            updatedLevels[nextLevel] = higherLevelValues;

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Level updated. Input:{0}, level:{1}, next:{2}, {1}'s data:{3}, {2}'s data:{4}",
                    input, level, nextLevel, upDownValues, higherLevelValues);
            }

            return true;
        }

        private void SafeUpdateThresholdsDownward(FeedbackLevel level, FeedbackLevel next, int input, 
            float factor, ref Dictionary<FeedbackLevel, FeedbackLevelData> updatedLevels)
        {
            FeedbackLevelData values, higherLevelValues;
            if (!this.UpdateLevelValues(next, input - 1, factor, out values, out higherLevelValues, updatedLevels))
            {
                return;
            }

            var upDownDiff = values.UpperBound - values.LowerBound;

            if (upDownDiff < MinimalLevelSize)
            {
                this.UpdateThresholdsInt(values.UpperBound - MinimalLevelSize, next, 1.0f, ref updatedLevels);
            }
        }

        /// <summary>
        /// Gets either 'level' if it is exist or first existing level below 'level'
        /// </summary>
        /// <param name="level"></param>
        /// <param name="realLevel"></param>
        private void GetExistingLevel(FeedbackLevel level, out FeedbackLevel realLevel)
        {
            while (!this.DoesLevelExists(level))
            {
                level = this.GetNextExistingLowerLevel(level);
            }
            realLevel = level;
        }

        private bool DoesLevelExists(FeedbackLevel level)
        {
            FeedbackLevelData values;
            if (this.thresholdValues.TryGetValue(level, out values))
            {
                return true;
            }

            if (level == FeedbackLevel.Highest)
            {
                return true;
            }

            return false;
        }

        private FeedbackLevel GetNextExistingHigherLevel(FeedbackLevel level)
        {
            var next = level;

            while (next != FeedbackLevel.Highest)
            {
                next = GetNextHigher(next);
                if (this.DoesLevelExists(next))
                {
                    return next;
                }
            }

            this.DoesLevelExists(level);

            return level;
        }

        private FeedbackLevel GetNextExistingLowerLevel(FeedbackLevel level)
        {
            var next = level;

            while (next != FeedbackLevel.Lowest)
            {
                next = GetNextLower(next);
                if (this.DoesLevelExists(next))
                {
                    return next;
                }
            }

            return FeedbackLevel.Lowest;
        }

        private static FeedbackLevel GetNextHigher(FeedbackLevel next)
        {
            return next == FeedbackLevel.Highest ? next : next + 1;
        }

        private static FeedbackLevel GetNextLower(FeedbackLevel current)
        {
            return current == FeedbackLevel.Lowest ? current : current - 1;
        }

        private void Initialize(string workLoadConfigFile)
        {
            // try to load feedback controllers from file: 

            string message;
            LoadPredictionSystemSection section;
            string filename = Path.Combine(this.rootPath, workLoadConfigFile);

            if (!ConfigurationLoader.TryLoadFromFile(filename, out section, out message))
            {
                log.WarnFormat(
                    "Could not initialize Load Prediction System from configuration: Invalid configuration file {0}. Using default settings... ({1})",
                    filename,
                    message);
            }

            if (section != null)
            {
                // load controllers from config file.);
                foreach (FeedbackControllerElement controllerElement in section.FeedbackControllers)
                {
                    if (controllerElement.Name != FeedbackName.PeerCount)
                    {
                        continue;
                    }

                    var dict = new SortedDictionary<FeedbackLevel, FeedbackLevelData>();
                    foreach (FeedbackLevelElement level in controllerElement.Levels)
                    {
                        var values = new FeedbackLevelData
                        {
                            UpperBound = level.Value,
                            LowerBound = level.ValueDown == -1 ? level.Value : level.ValueDown,
                        };
                        if (level.Level == FeedbackLevel.Highest)
                        {
                            values.UpperBound = int.MaxValue;
                        }

                        dict.Add(level.Level, values);
                    }
                    this.thresholdValues = dict;
                }


                log.InfoFormat("Initialized Load Prediction with {0} controllers from config file.", section.FeedbackControllers.Count);
            }
            else
            {
                // default settings, in case no config file was found.
                this.thresholdValues = DefaultConfiguration.GetDefaultControllers();
            }
        }

        public Dictionary<byte, int[]> GetControllersThresholds()
        {
            var result = new Dictionary<byte, int[]>
            {
                {(byte) FeedbackName.PeerCount, this.GetThresholdsData()}
            };

            return result;
        }

        private int[] GetThresholdsData()
        {
            var result = new int[this.thresholdValues.Count * 3];

            var i = 0;
            foreach (var thresholdValue in this.thresholdValues)
            {
                result[i] = (int)thresholdValue.Key;
                result[i + 1] = thresholdValue.Value.UpperBound;
                result[i + 2] = thresholdValue.Value.LowerBound;
                i += 3;
            }
            return result;
        }

        private void Serialize(XmlWriter writer)
        {
            writer.WriteStartElement("LoadPredictionSystem");
            this.SerializeControllers(writer);
            writer.WriteEndElement();
        }

        private void SerializeControllers(XmlWriter writer)
        {
            writer.WriteStartElement("FeedbackControllers");
            this.SerializeController(writer);
            writer.WriteEndElement();
        }

        private void SerializeController(XmlWriter writer)
        {
            writer.WriteStartElement("add");
            writer.WriteAttributeString("Name", FeedbackName.PeerCount.ToString());
            writer.WriteAttributeString("InitialInput", "0");
            writer.WriteAttributeString("InitialLevel", FeedbackLevel.Lowest.ToString());

            this.SerializeLevels(writer);
            writer.WriteEndElement();
        }

        private void SerializeLevels(XmlWriter writer)
        {
            writer.WriteStartElement("FeedbackLevels");

            foreach (var value in this.thresholdValues)
            {
                writer.WriteStartElement("add");
                writer.WriteAttributeString("Level", value.Key.ToString());
                writer.WriteAttributeString("Value", value.Value.UpperBound.ToString());
                writer.WriteAttributeString("ValueDown", value.Value.LowerBound.ToString());
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        #endregion
    }
}
