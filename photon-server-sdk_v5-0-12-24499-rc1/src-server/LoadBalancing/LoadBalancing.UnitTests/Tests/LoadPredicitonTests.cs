using System;
using System.Collections.Generic;
using NUnit.Framework;
using Photon.Common.LoadBalancer.LoadShedding;
using Photon.Common.LoadBalancer.Prediction;

namespace Photon.LoadBalancing.UnitTests.Tests
{
    [TestFixture]
    public class LoadPredicitonTests
    {
        [Test]
        public void PredictionTest()
        {
            var loadStatsCollector = new LoadStatsCollector(Environment.CurrentDirectory, "Prediction.config");

            var values = loadStatsCollector.GetControllersThresholds();
            TestBody(loadStatsCollector, new LoadPrediction((int)FeedbackLevel.LEVELS_COUNT, values));
        }

        [Test]
        public void PredictionConstructedFromDictionaryTest()
        {
            var initDic = new Dictionary<byte, int[]>
            {
                {
                    (byte) FeedbackName.PeerCount, new[]
                    {
                        0, 104, 0,
                        1, 209, 104,
                        2, 314, 209,
                        3, 419, 314,
                        4, 524, 419,
                        5, 629, 524,
                        6, 734, 629,
                        7, 838, 734,
                        8, 943, 838,
                        9, int.MaxValue, 943
                    }
                }
            };
            var prediction = new LoadPrediction(10, initDic);
            var loadStatsCollector = new LoadStatsCollector();
            TestBody(loadStatsCollector, prediction);
        }


        [Test]
        public void FactorLessThanZeroTest()
        {
            var initDic = new Dictionary<byte, int[]>
            {
                {
                    (byte) FeedbackName.PeerCount, new[]
                    {
                        0, 104, 0,
                        1, 209, 104,
                        2, 314, 209,
                        3, 419, 314,
                        4, 524, 419,
                        5, 629, 524,
                        6, 734, 629,
                        7, 838, 734,
                        8, 943, 838,
                        9, int.MaxValue, 943
                    }
                }
            };
            var prediction = new LoadPrediction(10, initDic);
            var loadStatsCollector = new LoadStatsCollector(0.8f);

            Dictionary<byte, int[]> updatedLevels;
            loadStatsCollector.UpdatePrediction(204, FeedbackLevel.Lowest, out updatedLevels);
            if (updatedLevels != null && updatedLevels.Count > 0)
            {
                prediction.UpdatePredictionLevels(updatedLevels);
                updatedLevels.Clear();
            }
            Assert.That(prediction.Output == FeedbackLevel.Lowest);

            prediction.SetPeerCount(185);
            Assert.That(prediction.Output == FeedbackLevel.Level1);

            prediction.SetPeerCount(183);
            Assert.That(prediction.Output == FeedbackLevel.Lowest);

            prediction.SetPeerCount(300);
            Assert.That(prediction.Output == FeedbackLevel.Level2);


            loadStatsCollector.UpdatePrediction(199, FeedbackLevel.Level2, out updatedLevels);
            if (updatedLevels != null && updatedLevels.Count > 0)
            {
                prediction.UpdatePredictionLevels(updatedLevels);
                updatedLevels.Clear();
            }

            prediction.SetPeerCount(200);
            Assert.That(prediction.Output == FeedbackLevel.Level1);

            prediction.SetPeerCount(202);
            Assert.That(prediction.Output == FeedbackLevel.Level2);

        }


        [Test]
        public void FactorGreaterThanZeroTest()
        {
            var initDic = new Dictionary<byte, int[]>
            {
                {
                    (byte) FeedbackName.PeerCount, new[]
                    {
                        0, 104, 0,
                        1, 209, 104,
                        2, 314, 209,
                        3, 419, 314,
                        4, 524, 419,
                        5, 629, 524,
                        6, 734, 629,
                        7, 838, 734,
                        8, 943, 838,
                        9, int.MaxValue, 943
                    }
                }
            };
            var prediction = new LoadPrediction(10, initDic);
            var loadStatsCollector = new LoadStatsCollector(1.2f);

            Dictionary<byte, int[]> updatedLevels;
            loadStatsCollector.UpdatePrediction(204, FeedbackLevel.Lowest, out updatedLevels);
            if (updatedLevels != null && updatedLevels.Count > 0)
            {
                prediction.UpdatePredictionLevels(updatedLevels);
                updatedLevels.Clear();
            }
            Assert.That(prediction.Output == FeedbackLevel.Lowest);

            prediction.SetPeerCount(225);
            Assert.That(prediction.Output == FeedbackLevel.Level1);

            prediction.SetPeerCount(223);
            Assert.That(prediction.Output == FeedbackLevel.Lowest);

            prediction.SetPeerCount(300);
            Assert.That(prediction.Output == FeedbackLevel.Level2);


            loadStatsCollector.UpdatePrediction(213, FeedbackLevel.Level2, out updatedLevels);
            if (updatedLevels != null && updatedLevels.Count > 0)
            {
                prediction.UpdatePredictionLevels(updatedLevels);
                updatedLevels.Clear();
            }

            prediction.SetPeerCount(207);
            Assert.That(prediction.Output == FeedbackLevel.Level1);

            prediction.SetPeerCount(209);
            Assert.That(prediction.Output == FeedbackLevel.Level2);
        }


        //[Test]
        //public void ReloadingPredictionDataTest()
        //{
        //    var configFile = Path.Combine(Environment.CurrentDirectory, "Prediction.config");
        //    try
        //    {
        //        File.Copy(configFile, configFile + ".bak", true);

        //        var collector = new LoadStatsCollector(Environment.CurrentDirectory, "Prediction.config");
        //        var thresholds = collector.GetControllersThresholds();
        //        TestBody(collector, new LoadPrediction((int)FeedbackLevel.LEVELS_COUNT, thresholds));// some updates to values should happen

        //        collector.SaveToFile();

        //        var prediction = new LoadPrediction(Environment.CurrentDirectory, "Prediction.config");

        //        prediction.SetPeerCount(199);
        //        Assert.That(prediction.Output == FeedbackLevel.Level1);

        //        prediction.SetPeerCount(200);
        //        Assert.That(prediction.Output == FeedbackLevel.Level2);

        //        prediction.SetPeerCount(150);
        //        Assert.That(prediction.Output == FeedbackLevel.Level1);

        //        prediction.SetPeerCount(50);
        //        Assert.That(prediction.Output, Is.EqualTo(FeedbackLevel.Lowest));

        //    }
        //    finally
        //    {
        //        File.Copy(configFile + ".bak", configFile, true);
        //        File.Delete(configFile + ".bak");
        //    }
        //}

        private static void TestBody(LoadStatsCollector loadStatsCollector, LoadPrediction predictor)
        {
            Assert.That(predictor.Output == FeedbackLevel.Lowest);

            predictor.SetPeerCount(100);
            Assert.That(predictor.Output == FeedbackLevel.Lowest);

            Dictionary<byte, int[]> updatedLevels;
            loadStatsCollector.UpdatePrediction(100, FeedbackLevel.Lowest, out updatedLevels);
            if (updatedLevels != null && updatedLevels.Count > 0)
            {
                predictor.UpdatePredictionLevels(updatedLevels);
                updatedLevels.Clear();
            }
            Assert.That(predictor.Output == FeedbackLevel.Lowest);

            predictor.SetPeerCount(200);
            Assert.That(predictor.Output == FeedbackLevel.Level1);

            loadStatsCollector.UpdatePrediction(200, FeedbackLevel.Lowest, out updatedLevels);
            if (updatedLevels != null && updatedLevels.Count > 0)
            {
                predictor.UpdatePredictionLevels(updatedLevels);
                updatedLevels.Clear();
            }

            Assert.That(predictor.Output == FeedbackLevel.Lowest);

            loadStatsCollector.UpdatePrediction(200, FeedbackLevel.Level2, out updatedLevels);
            if (updatedLevels != null && updatedLevels.Count > 0)
            {
                predictor.UpdatePredictionLevels(updatedLevels);
                updatedLevels.Clear();
            }

            Assert.That(predictor.Output, Is.EqualTo(FeedbackLevel.Level2));

            predictor.SetPeerCount(198);
            Assert.That(predictor.Output, Is.EqualTo(FeedbackLevel.Level1));
        }

        [Test]
        public void BigValuesTest()
        {
            var prediction = new LoadStatsCollector(Environment.CurrentDirectory, "Prediction.config");

            //prediction.UpdatePrediction(1251, FeedbackLevel.Level2);

            //prediction.UpdatePrediction(1737, FeedbackLevel.Level3);
            //prediction.UpdatePrediction(2193, FeedbackLevel.Level4);
            //prediction.UpdatePrediction(2619, FeedbackLevel.Level5);
            //prediction.UpdatePrediction(2979, FeedbackLevel.Level6);
            //prediction.UpdatePrediction(3000, FeedbackLevel.Level7);

            //prediction.UpdatePrediction(932, FeedbackLevel.Level1); 
            //prediction.UpdatePrediction(1416,FeedbackLevel.Level2); 
            //prediction.UpdatePrediction(1811,FeedbackLevel.Level3); 
            //prediction.UpdatePrediction(2195,FeedbackLevel.Level4); 
            //prediction.UpdatePrediction(2530,FeedbackLevel.Level5); 
            //prediction.UpdatePrediction(2906,FeedbackLevel.Level6);
            //prediction.UpdatePrediction(2999, FeedbackLevel.Level7);
            //prediction.UpdatePrediction(3735, FeedbackLevel.Level8);
            //prediction.UpdatePrediction(4000,FeedbackLevel.Highest);
            //prediction.UpdatePrediction(2499, FeedbackLevel.Level6);
            //prediction.UpdatePrediction(2169, FeedbackLevel.Level5);
            //prediction.UpdatePrediction(1999, FeedbackLevel.Level4);
            //prediction.UpdatePrediction(1499, FeedbackLevel.Level3);
            //prediction.UpdatePrediction(1009, FeedbackLevel.Level2);
            //prediction.UpdatePrediction(584, FeedbackLevel.Level1); 
            Dictionary<byte, int[]> output;
            prediction.UpdatePrediction(874, FeedbackLevel.Level1, out output);
            prediction.UpdatePrediction(1378,FeedbackLevel.Level2, out output);
            prediction.UpdatePrediction(1802,FeedbackLevel.Level3, out output);
            prediction.UpdatePrediction(2156,FeedbackLevel.Level4, out output);
            prediction.UpdatePrediction(2543,FeedbackLevel.Level5, out output);
            prediction.UpdatePrediction(2975,FeedbackLevel.Level6, out output);
            prediction.UpdatePrediction(3000,FeedbackLevel.Level7, out output);
            prediction.UpdatePrediction(3735, FeedbackLevel.Level8, out output);
            prediction.UpdatePrediction(4000, FeedbackLevel.Highest, out output);

            prediction.SaveToFile(Environment.CurrentDirectory, "test.result.config");
        }

    }
}
