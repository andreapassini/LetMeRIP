using System.Collections.Generic;
using NUnit.Framework;
using Photon.Common.LoadBalancer.LoadShedding;

namespace Photon.LoadBalancing.UnitTests.Tests
{
    [TestFixture]
    public class FeedbackControllerTests
    {
        [Test]
        public void InitialLevelTest()
        {

            var cpuController = new FeedbackController(FeedbackName.CpuUsage,
                new SortedDictionary<FeedbackLevel, FeedbackLevelData>(), 0, FeedbackLevel.Level3);

            Assert.That(cpuController.Output == FeedbackLevel.Level3);
        }

        [Test]
        public void LevelChangeTest()
        {

            var cpuController = new FeedbackController(FeedbackName.CpuUsage,
                new SortedDictionary<FeedbackLevel, FeedbackLevelData>
                        {
                            { FeedbackLevel.Level0, new FeedbackLevelData(10, 0) },
                            { FeedbackLevel.Level1, new FeedbackLevelData(20, 9) },
                            { FeedbackLevel.Level2, new FeedbackLevelData(30, 19) },
                            { FeedbackLevel.Level3, new FeedbackLevelData(40, 29) },
                            { FeedbackLevel.Level4, new FeedbackLevelData(50, 38) },
                            { FeedbackLevel.Level5, new FeedbackLevelData(60, 48) },
                            { FeedbackLevel.Level6, new FeedbackLevelData(70, 57) },
                            { FeedbackLevel.Level7, new FeedbackLevelData(80, 67) },
                            { FeedbackLevel.Level8, new FeedbackLevelData(90, 77) },
                            { FeedbackLevel.Level9, new FeedbackLevelData(int.MaxValue, 77) }
                        },
                0,
                FeedbackLevel.Level0);

            Assert.That(cpuController.Output == FeedbackLevel.Level0);

            cpuController.SetInput(5);
            Assert.That(cpuController.Output == FeedbackLevel.Level0);

            cpuController.SetInput(15);
            Assert.That(cpuController.Output == FeedbackLevel.Level1);

            cpuController.SetInput(10);
            Assert.That(cpuController.Output == FeedbackLevel.Level1);

            cpuController.SetInput(8);
            Assert.That(cpuController.Output == FeedbackLevel.Level0);

            cpuController.SetInput(95);
            Assert.That(cpuController.Output == FeedbackLevel.Highest);

            cpuController.SetInput(86);
            Assert.That(cpuController.Output == FeedbackLevel.Highest);

            cpuController.SetInput(76);
            Assert.That(cpuController.Output == FeedbackLevel.Level7);

            cpuController.SetInput(86);
            Assert.That(cpuController.Output == FeedbackLevel.Level8);

            cpuController.SetInput(76);
            Assert.That(cpuController.Output == FeedbackLevel.Level7);

            cpuController.SetInput(10);
            Assert.That(cpuController.Output == FeedbackLevel.Level1);
        }

        [Test]
        public void LevelChangeSparseConfigTest()
        {

            var cpuController = new FeedbackController(FeedbackName.CpuUsage,
                new SortedDictionary<FeedbackLevel, FeedbackLevelData>
                        {
                            { FeedbackLevel.Level0, new FeedbackLevelData(10, 0) },
                            { FeedbackLevel.Level4, new FeedbackLevelData(50, 9) },
                            { FeedbackLevel.Level7, new FeedbackLevelData(80, 48) },
                            { FeedbackLevel.Level9, new FeedbackLevelData(int.MaxValue, 77) }
                        },
                0,
                FeedbackLevel.Level0);

            Assert.That(cpuController.Output == FeedbackLevel.Level0);

            cpuController.SetInput(5);
            Assert.That(cpuController.Output == FeedbackLevel.Level0);

            cpuController.SetInput(15);
            Assert.That(cpuController.Output == FeedbackLevel.Level4);

            cpuController.SetInput(10);
            Assert.That(cpuController.Output == FeedbackLevel.Level4);

            cpuController.SetInput(8);
            Assert.That(cpuController.Output == FeedbackLevel.Level0);

            cpuController.SetInput(95);
            Assert.That(cpuController.Output == FeedbackLevel.Highest);

            cpuController.SetInput(86);
            Assert.That(cpuController.Output == FeedbackLevel.Highest);

            cpuController.SetInput(46);
            Assert.That(cpuController.Output == FeedbackLevel.Level4);

            cpuController.SetInput(10);
            Assert.That(cpuController.Output == FeedbackLevel.Level4);
        }

        [Test]
        public void UnexpectedInputTest()
        {

            var cpuController = new FeedbackController(FeedbackName.CpuUsage,
                new SortedDictionary<FeedbackLevel, FeedbackLevelData>
                        {
                            { FeedbackLevel.Level0, new FeedbackLevelData(10, 0) },
                            { FeedbackLevel.Level4, new FeedbackLevelData(50, 9) },
                            { FeedbackLevel.Level7, new FeedbackLevelData(80, 48) },
                            { FeedbackLevel.Level9, new FeedbackLevelData(100, 77) }
                        },
                0,
                FeedbackLevel.Level0);

            Assert.That(cpuController.Output == FeedbackLevel.Lowest);

            cpuController.SetInput(115);
            Assert.That(cpuController.Output == FeedbackLevel.Highest);

            cpuController.SetInput(-1);
            Assert.That(cpuController.Output == FeedbackLevel.Lowest);
        }

        [Test]
        public void LevelChangeNoIntersectConfigTest()
        {

            var cpuController = new FeedbackController(FeedbackName.CpuUsage,
                new SortedDictionary<FeedbackLevel, FeedbackLevelData>
                        {
                            { FeedbackLevel.Level0, new FeedbackLevelData(10, 0) },
                            { FeedbackLevel.Level4, new FeedbackLevelData(50, 20) },
                            { FeedbackLevel.Level7, new FeedbackLevelData(80, 48) },
                            { FeedbackLevel.Level9, new FeedbackLevelData(int.MaxValue, 90) }
                        },
                0,
                FeedbackLevel.Level0);

            Assert.That(cpuController.Output == FeedbackLevel.Level0);

            cpuController.SetInput(5);
            Assert.That(cpuController.Output == FeedbackLevel.Level0);

            cpuController.SetInput(15);
            Assert.That(cpuController.Output == FeedbackLevel.Level4);

            cpuController.SetInput(10);
            Assert.That(cpuController.Output == FeedbackLevel.Level0);

            cpuController.SetInput(95);
            Assert.That(cpuController.Output == FeedbackLevel.Highest);

            cpuController.SetInput(86);
            Assert.That(cpuController.Output == FeedbackLevel.Level7);

            cpuController.SetInput(87);
            Assert.That(cpuController.Output == FeedbackLevel.Highest);

            cpuController.SetInput(46);
            Assert.That(cpuController.Output == FeedbackLevel.Level4);

            cpuController.SetInput(10);
            Assert.That(cpuController.Output == FeedbackLevel.Lowest);
        }


    }
}
