// --------------------------------------------------------------------------------------------------------------------
// <copyright file="LoadBalancerTests.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the LoadBalancerTests type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using Photon.Common.LoadBalancer;
using Photon.Common.LoadBalancer.LoadShedding;
using Photon.Common.LoadBalancer.Priorities;

namespace Photon.LoadBalancing.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;

    using ExitGames.Logging;
    using ExitGames.Logging.Log4Net;

    using log4net.Config;
    using NUnit.Framework;
    using Photon.Common.LoadBalancer.Common;
    using Photon.SocketServer;

    [TestFixture]
    public class LoadBalancerTests
    {
        private class Server : IComparable<Server>
        {
            public string Name { get; set; }

            public int Count { get; set; }

            public int CompareTo(Server other)
            {
                if (other == null)
                {
                    return 1;
                }

                return string.Compare(this.Name, other.Name, StringComparison.Ordinal);
            }

            public override string ToString()
            {
                return string.Format("{0}: Count={1}", this.Name, this.Count);
            }

            public List<byte> SupportedProtocols { get; set; }
        }

        private LoadBalancer<Server> balancer;

        private List<Server> servers;

        [OneTimeSetUp]
        public void Setup()
        {
            this.balancer = new LoadBalancer<Server>();

            this.servers = new List<Server>();
            for (int i = 0; i < 10; i++)
            {
                this.servers.Add(new Server { Name = "Server" + i });
            }
        }

        [Test]
        public void Basics()
        {
            Server server;

            this.balancer = new LoadBalancer<Server>();
            this.TryGetServer(out server, false);
            this.TryUpdateServer(this.servers[0], FeedbackLevel.Level2, false);

            Assert.That(balancer.AverageWorkload, Is.EqualTo(FeedbackLevel.Highest));

            this.TryAddServer(this.servers[0], FeedbackLevel.Highest);
            this.TryGetServer(out server, false);

            Assert.That(balancer.AverageWorkload, Is.EqualTo(FeedbackLevel.Highest));

            this.TryAddServer(this.servers[1], FeedbackLevel.Highest);
            this.TryGetServer(out server, false);

            Assert.That(balancer.AverageWorkload, Is.EqualTo(FeedbackLevel.Highest));

            this.TryUpdateServer(this.servers[0], FeedbackLevel.Level3);
            this.TryGetServer(out server);
            Assert.AreSame(this.servers[0], server);

            Assert.That(balancer.AverageWorkload, Is.EqualTo(FeedbackLevel.Level6));

            this.TryRemoveServer(this.servers[0]);
            this.TryRemoveServer(this.servers[0], false);

            Assert.That(balancer.AverageWorkload, Is.EqualTo(FeedbackLevel.Highest));
        }

        [Test]
        public void BasicFiltering()
        {
            try
            {
                this.balancer = new LoadBalancer<Server>();

                this.TryAddServer(this.servers[1], FeedbackLevel.Lowest);
                this.servers[1].SupportedProtocols = new List<byte> { (byte)NetworkProtocolType.Tcp };

                Func<Server, bool> filterUdp = s =>
                {
                    if (s.SupportedProtocols == null)
                    {
                        return true;
                    }
                    return s.SupportedProtocols.Contains((byte)NetworkProtocolType.Udp);
                };
                Server x;
                Assert.That(this.balancer.TryGetServer(out x, filterUdp), Is.False);

                Func<Server, bool> filterTcp = s =>
                {
                    if (s.SupportedProtocols == null)
                    {
                        return true;
                    }
                    return s.SupportedProtocols.Contains((byte)NetworkProtocolType.Tcp);
                };

                Assert.That(this.balancer.TryGetServer(out x, filterTcp), Is.True);

            }
            finally
            {
                this.servers[1].SupportedProtocols = null;
            }
        }

        [Test]
        public void Properties()
        {
            this.balancer = new LoadBalancer<Server>();

            this.CheckLoadBalancerProperties(0, 0, 225);

            var loadLevelWeights = this.balancer.LoadLevelWeights;
            this.TryAddServer(this.servers[0], FeedbackLevel.Lowest);
            this.CheckLoadBalancerProperties(0, loadLevelWeights[0], 0);

            this.TryAddServer(this.servers[1], FeedbackLevel.Lowest);
            this.CheckLoadBalancerProperties(0, 2 * loadLevelWeights[(int)FeedbackLevel.Lowest], 0);

            this.TryUpdateServer(this.servers[0], FeedbackLevel.Level2);
            var expectedWeight = loadLevelWeights[(int) FeedbackLevel.Lowest] + loadLevelWeights[(int) FeedbackLevel.Level2];
            this.CheckLoadBalancerProperties(2, expectedWeight, 25);

            this.TryUpdateServer(this.servers[1], FeedbackLevel.Level2);
            expectedWeight = loadLevelWeights[(int)FeedbackLevel.Level2] + loadLevelWeights[(int)FeedbackLevel.Level2];
            this.CheckLoadBalancerProperties(4, expectedWeight, 50);

            expectedWeight = loadLevelWeights[(int)FeedbackLevel.Level2] + loadLevelWeights[(int)FeedbackLevel.Level4];
            this.TryUpdateServer(this.servers[0], FeedbackLevel.Level4);
            this.CheckLoadBalancerProperties(6, expectedWeight, 75);

            expectedWeight = loadLevelWeights[(int)FeedbackLevel.Level2] + loadLevelWeights[(int)FeedbackLevel.Level4];
            this.TryUpdateServer(this.servers[1], FeedbackLevel.Level2);
            this.CheckLoadBalancerProperties(6, expectedWeight, 75);

            this.TryRemoveServer(this.servers[1]);
            expectedWeight = loadLevelWeights[(int) FeedbackLevel.Level4];
            this.CheckLoadBalancerProperties(4, expectedWeight, 100);

            this.TryRemoveServer(this.servers[0]);
            this.CheckLoadBalancerProperties(0, 0, 225);
        }

        [Test]
        public void UpdateServerWithOORTest()
        {
            this.balancer = new LoadBalancer<Server>();
            this.TryAddServer(this.servers[0], FeedbackLevel.Lowest);
            this.TryAddServer(this.servers[1], FeedbackLevel.Lowest);

            this.balancer.TryUpdateServer(this.servers[0], FeedbackLevel.Lowest, 0, ServerState.OutOfRotation);

            Server srv;
            this.balancer.TryGetServer(out srv);
            Assert.AreEqual(this.servers[1], srv);

            this.TryRemoveServer(this.servers[1]);
            this.balancer.TryGetServer(out srv);
            Assert.IsNull(srv);

            this.balancer.TryUpdateServer(this.servers[0], FeedbackLevel.Highest, 0, ServerState.OutOfRotation);
            this.balancer.TryGetServer(out srv);
            Assert.IsNull(srv);

            this.balancer.TryUpdateServer(this.servers[0], FeedbackLevel.Highest);
            this.balancer.TryGetServer(out srv);
            Assert.IsNull(srv);

            this.balancer.TryUpdateServer(this.servers[0], FeedbackLevel.Lowest);
            this.balancer.TryGetServer(out srv);
            Assert.AreEqual(this.servers[0], srv);

            this.balancer.TryUpdateServer(this.servers[0], FeedbackLevel.Lowest, 0, ServerState.OutOfRotation);
            this.balancer.TryGetServer(out srv);
            Assert.IsNull(srv);
            this.balancer.TryUpdateServer(this.servers[0], FeedbackLevel.Lowest);
            this.balancer.TryGetServer(out srv);
            Assert.AreEqual(this.servers[0], srv);
            //add existing
            this.balancer.TryUpdateServer(this.servers[0], FeedbackLevel.Lowest, 0, ServerState.OutOfRotation);
            this.balancer.TryGetServer(out srv);
            Assert.IsNull(srv);
            Assert.False(this.balancer.TryAddServer(this.servers[0], FeedbackLevel.Lowest));
            //remove oor
            this.TryRemoveServer(this.servers[0]);
            this.balancer.TryGetServer(out srv);
            Assert.IsNull(srv);
        }

        [Test]
        public void LoadSpreadByDefault()
        {
            const int count = 100000;

            // default, as per DefaultConfiguration.GetDefaultWeights: 
            /*
            var loadLevelWeights = new int[]
            {
                40, // FeedbackLevel.Lowest
                30, // FeedbackLevel.Low
                20, // FeedbackLevel.Normal
                10, // FeedbackLevel.High
                0 // FeedbackLevel.Highest
            };
             */

            this.balancer = new LoadBalancer<Server>();

            const int serversInUse = 5;
            for (int i = 0; i < serversInUse; i++)
            {
                bool result = this.balancer.TryAddServer(this.servers[i], FeedbackLevel.Lowest);
                Assert.IsTrue(result);
            }

            // 5 servers with a load level of lowest
            // every server should get about 20 percent of the assignments
            this.AssignServerLoop(count);
            for (int i = 0; i < serversInUse; i++)
            {
                this.CheckServer(this.servers[i], count, 20, 5);
            }

            this.TryUpdateServer(this.servers[0], FeedbackLevel.Level0);
            this.TryUpdateServer(this.servers[1], FeedbackLevel.Level1);
            this.TryUpdateServer(this.servers[2], FeedbackLevel.Level2);
            this.TryUpdateServer(this.servers[3], FeedbackLevel.Level3);
            this.TryUpdateServer(this.servers[4], FeedbackLevel.Highest);

            Assert.That(this.balancer.AverageWorkload, Is.EqualTo((FeedbackLevel)Math.Round(((double)FeedbackLevel.Level0 
                                         + (double)FeedbackLevel.Level1 
                                         + (double)FeedbackLevel.Level2 
                                         + (double)FeedbackLevel.Level3 
                                         + (double)FeedbackLevel.Highest)/serversInUse)));
            
            this.AssignServerLoop(count);
            var k = 100.0/this.balancer.ServersInUseWeight;
            var weights = this.balancer.LoadLevelWeights;
            this.CheckServer(this.servers[0], count, (int)(k * weights[0]), 5);
            this.CheckServer(this.servers[1], count, (int)(k * weights[1]), 5);
            this.CheckServer(this.servers[2], count, (int)(k * weights[2]), 5);
            this.CheckServer(this.servers[3], count, (int)(k * weights[3]), 5);
            this.CheckServer(this.servers[4], count, 0, 0);

            this.TryUpdateServer(this.servers[0], FeedbackLevel.Level0);
            this.TryUpdateServer(this.servers[1], FeedbackLevel.Level1);
            this.TryUpdateServer(this.servers[2], FeedbackLevel.Level1);
            this.TryUpdateServer(this.servers[3], FeedbackLevel.Level2);
            this.TryUpdateServer(this.servers[4], FeedbackLevel.Level2);

            Assert.That(this.balancer.AverageWorkload, Is.EqualTo((FeedbackLevel)Math.Round(((double)FeedbackLevel.Level0 
                                                                                   + (double)FeedbackLevel.Level1 
                                                                                   + (double)FeedbackLevel.Level1 
                                                                                   + (double)FeedbackLevel.Level2 
                                                                                   + (double)FeedbackLevel.Level2)/serversInUse)));

            this.AssignServerLoop(count);

            k = 100.0 / this.balancer.ServersInUseWeight;
            this.CheckServer(this.servers[0], count, (int)(k * weights[0]), 5);//28
            this.CheckServer(this.servers[1], count, (int)(k * weights[1]), 5);//21
            this.CheckServer(this.servers[2], count, (int)(k * weights[1]), 5);
            this.CheckServer(this.servers[3], count, (int)(k * weights[2]), 5);//14
            this.CheckServer(this.servers[4], count, (int)(k * weights[2]), 5);

            this.TryUpdateServer(this.servers[0], FeedbackLevel.Level1);
            this.TryUpdateServer(this.servers[1], FeedbackLevel.Level2);
            this.TryUpdateServer(this.servers[2], FeedbackLevel.Level2);
            this.TryUpdateServer(this.servers[3], FeedbackLevel.Level2);
            this.TryUpdateServer(this.servers[4], FeedbackLevel.Level2);

            Assert.That(this.balancer.AverageWorkload, Is.EqualTo((FeedbackLevel)Math.Round(((double)FeedbackLevel.Level1 
                                                                                   + (double)FeedbackLevel.Level2 
                                                                                   + (double)FeedbackLevel.Level2 
                                                                                   + (double)FeedbackLevel.Level2 
                                                                                   + (double)FeedbackLevel.Level2)/serversInUse)));

            this.AssignServerLoop(count);
            k = 100.0 / this.balancer.ServersInUseWeight;
            this.CheckServer(this.servers[0], count, (int)(k * weights[1]), 5);
            this.CheckServer(this.servers[1], count, 18, 5);
            this.CheckServer(this.servers[2], count, 18, 5);
            this.CheckServer(this.servers[3], count, 18, 5);
            this.CheckServer(this.servers[4], count, 18, 5);

            this.TryUpdateServer(this.servers[0], FeedbackLevel.Level1);
            this.TryUpdateServer(this.servers[1], FeedbackLevel.Highest);
            this.TryUpdateServer(this.servers[2], FeedbackLevel.Highest);
            this.TryUpdateServer(this.servers[3], FeedbackLevel.Highest);
            this.TryUpdateServer(this.servers[4], FeedbackLevel.Highest);

            Assert.That(this.balancer.AverageWorkload, Is.EqualTo((FeedbackLevel)Math.Round(((double)FeedbackLevel.Level1 
                                                                                   + (double)FeedbackLevel.Highest 
                                                                                   + (double)FeedbackLevel.Highest 
                                                                                   + (double)FeedbackLevel.Highest 
                                                                                   + (double)FeedbackLevel.Highest)/serversInUse)));

            this.AssignServerLoop(count);
            this.CheckServer(this.servers[0], count, 100, 0);
            this.CheckServer(this.servers[1], count, 0, 0);
            this.CheckServer(this.servers[2], count, 0, 0);
            this.CheckServer(this.servers[3], count, 0, 0);
            this.CheckServer(this.servers[4], count, 0, 0);

            this.TryUpdateServer(this.servers[0], FeedbackLevel.Highest);
            Server server;
            Assert.IsFalse(this.balancer.TryGetServer(out server));
        }

        [Test]
        public void LoadSpread()
        {
            const int count = 100000;

            var loadLevelWeights = new int[] 
            { 
                50, // FeedbackLevel.Lowest
                30, // FeedbackLevel.Level1
                15, // FeedbackLevel.Level2
                5, // FeedbackLevel.Level3
                0, 
                0, 
                0, 
                0, 
                0,
                0,// FeedbackLevel.Highest
            };

            this.balancer = new LoadBalancer<Server>(loadLevelWeights, 42);
            const int serversInUse = 5;
            for (int i = 0; i < serversInUse; i++)
            {
                bool result = this.balancer.TryAddServer(this.servers[i], FeedbackLevel.Lowest);
                Assert.IsTrue(result);
            }

            // 5 servers with a load level of lowest
            // every server should get about 20 percent of the assignments
            this.AssignServerLoop(count);
            var expectedPercents = 100/serversInUse;
            for (int i = 0; i < serversInUse; i++)
            {
                this.CheckServer(this.servers[i], count, expectedPercents, 5);
            }

            for (int i = 0; i < serversInUse; i++)
            {
                this.TryUpdateServer(this.servers[i], (FeedbackLevel)i);
            }

            this.AssignServerLoop(count);
            for (var i = 0; i < serversInUse - 1; i++)
            {
                this.CheckServer(this.servers[i], count, loadLevelWeights[i], 5);
            }
            this.CheckServer(this.servers[serversInUse - 1], count, 0, 0);

            this.TryUpdateServer(this.servers[0], FeedbackLevel.Level0);
            this.TryUpdateServer(this.servers[1], FeedbackLevel.Level1);
            this.TryUpdateServer(this.servers[2], FeedbackLevel.Level1);
            this.TryUpdateServer(this.servers[3], FeedbackLevel.Level2);
            this.TryUpdateServer(this.servers[4], FeedbackLevel.Level2);

            this.AssignServerLoop(count);
            this.CheckServer(this.servers[0], count, 36, 5);
            this.CheckServer(this.servers[1], count, 21, 5);
            this.CheckServer(this.servers[2], count, 21, 5);
            this.CheckServer(this.servers[3], count, 11, 5);
            this.CheckServer(this.servers[4], count, 11, 5);

            this.TryUpdateServer(this.servers[0], FeedbackLevel.Level1);
            this.TryUpdateServer(this.servers[1], FeedbackLevel.Level2);
            this.TryUpdateServer(this.servers[2], FeedbackLevel.Level2);
            this.TryUpdateServer(this.servers[3], FeedbackLevel.Level2);
            this.TryUpdateServer(this.servers[4], FeedbackLevel.Level2);
            this.AssignServerLoop(count);
            this.CheckServer(this.servers[0], count, 33, 5);
            this.CheckServer(this.servers[1], count, 17, 5);
            this.CheckServer(this.servers[2], count, 17, 5);
            this.CheckServer(this.servers[3], count, 17, 5);
            this.CheckServer(this.servers[4], count, 17, 5);

            this.TryUpdateServer(this.servers[0], FeedbackLevel.Level1);
            this.TryUpdateServer(this.servers[1], FeedbackLevel.Level4);
            this.TryUpdateServer(this.servers[2], FeedbackLevel.Level4);
            this.TryUpdateServer(this.servers[3], FeedbackLevel.Level4);
            this.TryUpdateServer(this.servers[4], FeedbackLevel.Level4);
            this.AssignServerLoop(count);
            this.CheckServer(this.servers[0], count, 100, 0);
            this.CheckServer(this.servers[1], count, 0, 0);
            this.CheckServer(this.servers[2], count, 0, 0);
            this.CheckServer(this.servers[3], count, 0, 0);
            this.CheckServer(this.servers[4], count, 0, 0);

            this.TryUpdateServer(this.servers[0], FeedbackLevel.Level4);
            Server server;
            Assert.IsFalse(this.balancer.TryGetServer(out server));
        }

        /// <summary>
        /// check load spread when one of the servers does not support UDP
        /// </summary>
        [Test]
        public void LoadSpreadWithFiltering()
        {
            const int count = 5000;

            var loadLevelWeights = new int[] 
            { 
                50, // FeedbackLevel.Lowest
                30, // FeedbackLevel.Level1
                15, // FeedbackLevel.Level2
                5, // FeedbackLevel.Level3
                0, 
                0, 
                0, 
                0, 
                0,
                0,// FeedbackLevel.Highest
            };
            const int ServerWithProtocols = 5;
            try
            {
                this.balancer = new LoadBalancer<Server>(loadLevelWeights, 42);
                const int serversInUse = 6;
                for (int i = 0; i < serversInUse; i++)
                {
                    bool result = this.balancer.TryAddServer(this.servers[i], FeedbackLevel.Lowest);
                    Assert.IsTrue(result);
                }

                this.servers[ServerWithProtocols].SupportedProtocols = new List<byte> { (byte)NetworkProtocolType.Udp };

                Func<Server, bool> filterExpectsTcp = s =>
                {
                    if (s.SupportedProtocols == null)
                    {
                        return true;
                    }
                    return s.SupportedProtocols.Contains((byte)NetworkProtocolType.Tcp);
                };

                // 6 servers with a load level of lowest 
                // every server should get about 100/20 percent of the assignments.
                // but one is filter out. its loading will go to first server
                this.AssignServerLoopWithFilter(count, filterExpectsTcp);

                var expectedPercents = 100 / serversInUse;
                this.CheckServer(this.servers[0], count, 2 * expectedPercents, 5);
                for (int i = 1; i < serversInUse - 1; i++)
                {
                    this.CheckServer(this.servers[i], count, expectedPercents, 5);
                }

                Func<Server, bool> filterExpectsUdp = s =>
                {
                    if (s.SupportedProtocols == null)
                    {
                        return true;
                    }
                    return s.SupportedProtocols.Contains((byte)NetworkProtocolType.Udp);
                };

                // 6 servers with a load level of lowest
                // every server should get about 100/6 percent of the assignments
                this.AssignServerLoopWithFilter(count, filterExpectsUdp);

                expectedPercents = 100 / serversInUse;
                for (int i = 0; i < serversInUse; i++)
                {
                    this.CheckServer(this.servers[i], count, expectedPercents, 5);
                }

            }
            finally
            {
                this.servers[ServerWithProtocols].SupportedProtocols = null;
            }
        }

         [Test]
        public void LoadSpreadFromConfig()
        {
            const int count = 100000;

            const string configPath = "LoadBalancer.config";
            this.balancer = new LoadBalancer<Server>(configPath);

            const int ServersCount = 5;
            for (int i = 0; i < ServersCount; i++)
            {
                bool result = this.balancer.TryAddServer(this.servers[i], FeedbackLevel.Lowest);
                Assert.IsTrue(result);
            }

            // 5 servers with a load level of lowest
            // every server should get about 20 percent of the assignments
            this.AssignServerLoop(count);
            var expectedPercents = 100/ ServersCount;

            for (int i = 0; i < ServersCount; i++)
            {
                this.CheckServer(this.servers[i], count, expectedPercents, 5);
            }

            this.TryUpdateServer(this.servers[0], FeedbackLevel.Level1);
            this.TryUpdateServer(this.servers[1], FeedbackLevel.Level2);
            this.TryUpdateServer(this.servers[2], FeedbackLevel.Level3);
            this.TryUpdateServer(this.servers[3], FeedbackLevel.Level4);
            this.TryUpdateServer(this.servers[4], FeedbackLevel.Highest);
            this.AssignServerLoop(count);
            this.CheckServer(this.servers[0], count, 25, 5);
            this.CheckServer(this.servers[1], count, 25, 5);
            this.CheckServer(this.servers[2], count, 25, 5);
            this.CheckServer(this.servers[3], count, 25, 5);
            this.CheckServer(this.servers[4], count, 0, 0);

            this.TryUpdateServer(this.servers[0], FeedbackLevel.Level1);
            this.TryUpdateServer(this.servers[1], FeedbackLevel.Level2);
            this.TryUpdateServer(this.servers[2], FeedbackLevel.Level2);
            this.TryUpdateServer(this.servers[3], FeedbackLevel.Level3);
            this.TryUpdateServer(this.servers[4], FeedbackLevel.Level3);
            this.AssignServerLoop(count);
            this.CheckServer(this.servers[0], count, 20, 5);
            this.CheckServer(this.servers[1], count, 20, 5);
            this.CheckServer(this.servers[2], count, 20, 5);
            this.CheckServer(this.servers[3], count, 20, 5);
            this.CheckServer(this.servers[4], count, 20, 5);

            this.TryUpdateServer(this.servers[0], FeedbackLevel.Level2);
            this.TryUpdateServer(this.servers[1], FeedbackLevel.Level3);
            this.TryUpdateServer(this.servers[2], FeedbackLevel.Level3);
            this.TryUpdateServer(this.servers[3], FeedbackLevel.Level3);
            this.TryUpdateServer(this.servers[4], FeedbackLevel.Level3);
            this.AssignServerLoop(count);
            this.CheckServer(this.servers[0], count, 20, 5);
            this.CheckServer(this.servers[1], count, 20, 5);
            this.CheckServer(this.servers[2], count, 20, 5);
            this.CheckServer(this.servers[3], count, 20, 5);
            this.CheckServer(this.servers[4], count, 20, 5);

            this.TryUpdateServer(this.servers[0], FeedbackLevel.Level2);
            this.TryUpdateServer(this.servers[1], FeedbackLevel.Highest);
            this.TryUpdateServer(this.servers[2], FeedbackLevel.Highest);
            this.TryUpdateServer(this.servers[3], FeedbackLevel.Highest);
            this.TryUpdateServer(this.servers[4], FeedbackLevel.Highest);
            this.AssignServerLoop(count);
            this.CheckServer(this.servers[0], count, 100, 0);
            this.CheckServer(this.servers[1], count, 0, 0);
            this.CheckServer(this.servers[2], count, 0, 0);
            this.CheckServer(this.servers[3], count, 0, 0);
            this.CheckServer(this.servers[4], count, 0, 0);

            this.TryUpdateServer(this.servers[0], FeedbackLevel.Highest);
            Server server;
            Assert.IsFalse(this.balancer.TryGetServer(out server));
        }

        [Test]
        public void LoadSpreadAfterConfigChange()
        {
            const int count = 100000;

            const string configPath = "LoadBalancer.config";
            this.balancer = new LoadBalancer<Server>(configPath);

            for (int i = 0; i < this.servers.Count; i++)
            {
                bool result = this.balancer.TryAddServer(this.servers[i], FeedbackLevel.Lowest);
                Assert.IsTrue(result);
            }

            // 5 servers with a load level of lowest
            // every server should get about 20 percent of the assignments
            this.AssignServerLoop(count);
            foreach (var server in this.servers)
            {
                this.CheckServer(server, count, 100 / this.servers.Count, 5);
            }

            for (var i = 0; i < this.servers.Count - 1; i++)
            {
                this.TryUpdateServer(this.servers[i], (FeedbackLevel)i);
            }
            this.TryUpdateServer(this.servers[this.servers.Count - 1], FeedbackLevel.Highest);

            this.AssignServerLoop(count);
            var expectedPercents = 100/(this.servers.Count - 1);

            for (var i = 0; i < this.servers.Count - 1; i++)
            {
                this.CheckServer(this.servers[i], count, expectedPercents, 5);
            }
            this.CheckServer(this.servers[this.servers.Count - 1], count, 0, 0);

            File.Copy("LoadBalancer.config", "LoadBalancer.config.bak", true);
            File.Delete("LoadBalancer.config");
            
            // wait a bit until the update is done: 
            Thread.Sleep(500);

            this.AssignServerLoop(count);

            var loadLevelWeights = this.balancer.LoadLevelWeights;

            for (var i = 0; i < this.servers.Count - 1; i++)
            {
                this.CheckServer(this.servers[i], count, loadLevelWeights[i], 5);
            }
            this.CheckServer(this.servers[this.servers.Count - 1], count, 0, 0);

            File.Copy("LoadBalancer.config.bak", "LoadBalancer.config", true);

            // wait a bit until the update is done: 
            Thread.Sleep(1000);

            this.AssignServerLoop(count);
            expectedPercents = 100 / (this.servers.Count - 1);
            for (var i = 0; i < this.servers.Count - 1; i++)
            {
                this.CheckServer(this.servers[i], count, expectedPercents, 5);
            }
            this.CheckServer(this.servers[this.servers.Count - 1], count, 0, 0);
        }

        [Test]
        public void LoadSpreadAfterIncludingNewPriorityChange()
        {
            var loadLevelWeights = new int[]
            {
                50, // FeedbackLevel.Lowest
                30, // FeedbackLevel.Level2
                15, // FeedbackLevel.Level3
                5, // FeedbackLevel.Level4
                0,
                0,
                0,
                0,
                0,
                0, // FeedbackLevel.Highest
            };

            this.balancer = new LoadBalancer<Server>(loadLevelWeights, 42, FeedbackLevel.Level2, FeedbackLevel.Level1);
            const int serversInUse = 5;
            const int SecondPriortiy = 2;
            for (var i = 0; i < serversInUse; i++)
            {
                var result = this.balancer.TryAddServer(this.servers[i], FeedbackLevel.Lowest, (byte) (i > 2 ? SecondPriortiy : 0));
                Assert.IsTrue(result);
            }

            Assert.That(this.balancer.ServersInUseWeight == 150);

            for (var i = 0; i < 3; i++)
            {
                this.balancer.TryUpdateServer(this.servers[i], FeedbackLevel.Level2, 0);
            }
            Assert.That(this.balancer.ServersInUseWeight == 145);
            Assert.That(this.balancer.TotalWorkload, Is.EqualTo((int)FeedbackLevel.Level2 * 3));

            this.balancer.TryUpdateServer(this.servers[0], FeedbackLevel.Lowest, 0);

            Assert.That(this.balancer.ServersInUseWeight == 80);
        }

        [Test]
        public void LoadSpreadAfterIncludingNewPriorityChange2()
        {
            var loadLevelWeights = new int[]
            {
                50, // FeedbackLevel.Lowest
                30, // FeedbackLevel.Level2
                15, // FeedbackLevel.Level3
                5, // FeedbackLevel.Level4
                0,
                0,
                0,
                0,
                0,
                0, // FeedbackLevel.Highest
            };

            this.balancer = new LoadBalancer<Server>(loadLevelWeights, 42, FeedbackLevel.Level2, FeedbackLevel.Lowest);
            const int serversInUse = 5;
            const int SecondPriortiy = 2;
            for (var i = 0; i < serversInUse; i++)
            {
                var result = this.balancer.TryAddServer(this.servers[i], FeedbackLevel.Lowest, (byte)(i > 2 ? SecondPriortiy : 0));
                Assert.IsTrue(result);
            }

            Assert.That(this.balancer.ServersInUseWeight == 150);
            Assert.That(this.balancer.AverageWorkload, Is.EqualTo(FeedbackLevel.Lowest));

            for (var i = 0; i < 3; i++)
            {
                this.balancer.TryUpdateServer(this.servers[i], FeedbackLevel.Level2, 0);
            }
            Assert.That(this.balancer.ServersInUseWeight, Is.EqualTo(145));
            Assert.That(this.balancer.AverageWorkload, Is.EqualTo(FeedbackLevel.Level1));

            this.balancer.TryUpdateServer(this.servers[0], FeedbackLevel.Lowest, 0);
            Assert.That(this.balancer.ServersInUseWeight == 180);

            this.balancer.TryUpdateServer(this.servers[1], FeedbackLevel.Lowest, 0);
            Assert.That(this.balancer.ServersInUseWeight == 215);

            this.balancer.TryUpdateServer(this.servers[2], FeedbackLevel.Lowest, 0);
            Assert.That(this.balancer.ServersInUseWeight == 150);
        }

        [Test]
        public void LoseZeroPriorityServersTest()
        {
            var loadLevelWeights = new int[]
            {
                50, // FeedbackLevel.Lowest
                30, // FeedbackLevel.Level2
                15, // FeedbackLevel.Level3
                5, // FeedbackLevel.Level4
                0,
                0,
                0,
                0,
                0,
                0, // FeedbackLevel.Highest
            };

            this.balancer = new LoadBalancer<Server>(loadLevelWeights, 42, FeedbackLevel.Level3, FeedbackLevel.Lowest);
            const int serversInUse = 5;
            const int SecondPriority = 2;
            for (var i = 0; i < serversInUse; i++)
            {
                var result = this.balancer.TryAddServer(this.servers[i], FeedbackLevel.Lowest, (byte)(i > 2 ? SecondPriority : 0));
                Assert.IsTrue(result);
            }

            Assert.That(this.balancer.ServersInUseWeight == 150);

            this.balancer.TryRemoveServer(this.servers[0],  0);
            Assert.That(this.balancer.ServersInUseWeight == 100);

            this.balancer.TryRemoveServer(this.servers[1], 0);
            Assert.That(this.balancer.ServersInUseWeight == 50);

            this.balancer.TryRemoveServer(this.servers[2], 0);
            Assert.That(this.balancer.ServersInUseWeight == 100);
        }

        [Test]
        public void AverageLoadPriorityChangeTest()
        {
            var loadLevelWeights = new int[]
            {
                50, // FeedbackLevel.Lowest
                30, // FeedbackLevel.Level2
                15, // FeedbackLevel.Level3
                5, // FeedbackLevel.Level4
                0,
                0,
                0,
                0,
                0,
                0, // FeedbackLevel.Highest
            };

            this.balancer = new LoadBalancer<Server>(loadLevelWeights, 42, FeedbackLevel.Level2, FeedbackLevel.Lowest);
            const int serversInUse = 5;
            const byte SecondPriority = 2;
            for (var i = 0; i < serversInUse; i++)
            {
                var result = this.balancer.TryAddServer(this.servers[i], FeedbackLevel.Lowest, (byte)(i > 2 ? SecondPriority : 0));
                Assert.IsTrue(result);
            }

            Assert.That(this.balancer.AverageWorkload, Is.EqualTo(FeedbackLevel.Lowest));
            Assert.That(this.balancer.TryUpdateServer(this.servers[3], FeedbackLevel.Level5, SecondPriority));
            Assert.That(this.balancer.AverageWorkload, Is.EqualTo(FeedbackLevel.Level1));
            Assert.That(this.balancer.TotalWorkload, Is.EqualTo((int)FeedbackLevel.Level5));

            Assert.That(this.balancer.TryUpdateServer(this.servers[3], FeedbackLevel.Lowest, SecondPriority));

            for (var i = 0; i < 3; i++)
            {
                this.balancer.TryUpdateServer(this.servers[i], FeedbackLevel.Level4, 0);
            }

            Assert.That(this.balancer.AverageWorkload, Is.EqualTo(FeedbackLevel.Level2));

            Assert.That(this.balancer.TryUpdateServer(this.servers[3], FeedbackLevel.Level4, SecondPriority));

            Assert.That(this.balancer.TryUpdateServer(this.servers[4], FeedbackLevel.Level4, SecondPriority));

            Assert.That(this.balancer.AverageWorkload, Is.EqualTo(FeedbackLevel.Level4));

            for (var i = 0; i < 3; i++)
            {
                this.balancer.TryUpdateServer(this.servers[i], FeedbackLevel.Level0, 0);
            }
            Assert.That(this.balancer.AverageWorkload, Is.EqualTo(FeedbackLevel.Level2));

            // Checking that Highest loading also works fine
            for (var i = 0; i < 3; i++)
            {
                this.balancer.TryUpdateServer(this.servers[i], FeedbackLevel.Highest, 0);
            }
            for (var i = 3; i < serversInUse; i++)
            {
                this.balancer.TryUpdateServer(this.servers[i], FeedbackLevel.Highest, SecondPriority);
            }
            Assert.That(this.balancer.AverageWorkload, Is.EqualTo(FeedbackLevel.Highest));
        }

        [Test]
        public void PriorityRemoveTest()
        {
            this.balancer = new LoadBalancer<Server>
            {
                PriorityDownThreshold = FeedbackLevel.Level3,
                PriorityUpThreshold = FeedbackLevel.Level5
            };

            const int serversInUse = 10;
            const int SecondPriority = 2;
            // add monster machine
            this.balancer.TryAddServer(this.servers[0], FeedbackLevel.Level3);
            for (var i = 1; i < serversInUse; i++)
            {
                var result = this.balancer.TryAddServer(this.servers[i], FeedbackLevel.Lowest, SecondPriority);
                Assert.IsTrue(result);
            }

            // priority 2 is not used so, Level3 is load for available servers
            Assert.That(this.balancer.AverageWorkloadForAvailableServers, Is.EqualTo(FeedbackLevel.Level3));

            // we reached value UP and servers from priority 2 were added. this is 9 servers, so AverageWorkloadForAvailableServers goes to 0
            this.balancer.TryUpdateServer(this.servers[0], FeedbackLevel.Level5);
            Assert.That(this.balancer.AverageWorkloadForAvailableServers, Is.EqualTo(FeedbackLevel.Level0));

            // we go further to see that priority servers are not removed
            this.balancer.TryUpdateServer(this.servers[1], FeedbackLevel.Level1, SecondPriority);
            Assert.That(this.balancer.AverageWorkloadForAvailableServers, Is.EqualTo(FeedbackLevel.Level1));

            // loading of monster machine went down but did not reach valueDown, so although priority 2 servers are not loaded at all we do not remove them
            this.balancer.TryUpdateServer(this.servers[0], FeedbackLevel.Level4);
            Assert.That(this.balancer.AverageWorkloadForAvailableServers, Is.EqualTo(FeedbackLevel.Level0));

            // now we reached valueDown and priority 2 servers were removed. We see that AverageWorkloadForAvailableServers is equal to monster machine loading
            this.balancer.TryUpdateServer(this.servers[1], FeedbackLevel.Level0, SecondPriority);
            this.balancer.TryUpdateServer(this.servers[0], FeedbackLevel.Level3);
            Assert.That(this.balancer.AverageWorkloadForAvailableServers, Is.EqualTo(FeedbackLevel.Level3));
        }

        [Test]
        public void ServerBunch_ReserveRatioTest()
        {
            var bunch = new ServerBunch<Server>(0, 0.2f);

            const int serversInUse = 10;
            for (var i = 0; i < serversInUse; i++)
            {
                bunch.TryAddServer(this.servers[i], FeedbackLevel.Lowest, 20);
            }

            Assert.That(bunch.GetServers().Count(s => s.IsReserved), Is.EqualTo(2));
            Assert.That(bunch.GetServers().Count(s => s.IsInReserve), Is.EqualTo(2));

            ServerStateData<Server> takenFromReserve;
            ServerStateData<Server> serverToRemove;
            bunch.TryGetServer(this.servers[0], out serverToRemove);
            bunch.RemoveServer(serverToRemove, out takenFromReserve);

            Assert.That(takenFromReserve.IsInReserve, Is.False);
            Assert.That(takenFromReserve.IsReserved, Is.False);

            Assert.That(bunch.GetServers().Count(s => s.IsReserved), Is.EqualTo(1));
            Assert.That(bunch.GetServers().Count(s => s.IsInReserve), Is.EqualTo(1));

            bunch.TryAddServer(this.servers[0], FeedbackLevel.Lowest, 20);

            Assert.That(bunch.GetServers().Count(s => s.IsReserved), Is.EqualTo(2));
            Assert.That(bunch.GetServers().Count(s => s.IsInReserve), Is.EqualTo(2));
        }

        [Test]
        public void ServerBunch_ServersInUseLoadTest()
        {
            var bunch = new ServerBunch<Server>(0, 0.2f);

            const int serversInUse = 10;
            for (var i = 0; i < serversInUse; i++)
            {
                bunch.TryAddServer(this.servers[i], FeedbackLevel.Lowest, 20);
            }

            const FeedbackLevel newLoad = FeedbackLevel.Level6;
            foreach (var server in bunch.GetServers())
            {
                if (!server.IsReserved)
                {
                    bunch.UpdateTotalWorkload(server, FeedbackLevel.Level0, newLoad);
                }
            }

            Assert.That(bunch.ServersInUseAverageWorkload, Is.EqualTo((int)newLoad));

            // check that update of reserved server does not have any influence
            ServerStateData<Server> firstReservedServer = null;
            foreach (var server in bunch.GetServers())
            {
                if (server.IsReserved)
                {
                    bunch.UpdateTotalWorkload(server, FeedbackLevel.Level0, FeedbackLevel.Level9);
                    firstReservedServer = server;
                    break;
                }
            }
            Assert.That(bunch.ServersInUseAverageWorkload, Is.EqualTo((int)newLoad));

            // return load back
            bunch.UpdateTotalWorkload(firstReservedServer, FeedbackLevel.Level0, FeedbackLevel.Level0);

            var serverFromReserve = bunch.GetServerFromReserve();

            Assert.That(serverFromReserve, Is.SameAs(firstReservedServer));

            Assert.That(bunch.ServersInUseAverageWorkload, Is.EqualTo((int)newLoad - 1));

            bunch.ReturnServerIntoReserve(serverFromReserve);

            Assert.That(bunch.ServersInUseAverageWorkload, Is.EqualTo((int)newLoad));
        }

        [Test]
        public void ServerBunch_RemoveReservedServerTest()
        {
            var bunch = new ServerBunch<Server>(0, 0.2f);

            const int serversInUse = 10;
            for (var i = 0; i < serversInUse; i++)
            {
                bunch.TryAddServer(this.servers[i], FeedbackLevel.Lowest, 20);
            }

            Assert.That(bunch.GetServers().Count(s => s.IsReserved), Is.EqualTo(2));
            Assert.That(bunch.ReservedServersCount, Is.EqualTo(2));
            Assert.That(bunch.ServersUsedFromReserveCount, Is.EqualTo(0));

            Assert.That(bunch.GetServers().Count(s => s.IsInReserve), Is.EqualTo(2));


            ServerStateData<Server> serverToRemove = bunch.GetServerFromReserve();

            var bunchServers = bunch.GetServers().ToArray();

            Assert.That(bunchServers.Count(s => s.IsInReserve), Is.EqualTo(1));
            Assert.That(bunch.ReservedServersCount, Is.EqualTo(2));
            Assert.That(bunch.ServersUsedFromReserveCount, Is.EqualTo(1));

            ServerStateData<Server> takenFromReserve;
            bunch.RemoveServer(serverToRemove, out takenFromReserve);

            Assert.That(bunchServers.Count(s => s.IsInReserve), Is.EqualTo(1));
            Assert.That(bunch.ReservedServersCount, Is.EqualTo(1));
            Assert.That(bunch.ServersUsedFromReserveCount, Is.EqualTo(0));

            Assert.That(takenFromReserve, Is.Null);
        }

        [Test]
        public void ServerReserveRatioTest02()
        {
            this.balancer = new LoadBalancer<Server>
            {
                PriorityDownThreshold = FeedbackLevel.Level7,
                PriorityUpThreshold = FeedbackLevel.Level8,
                ReserveRatio = 0.2f
            };

            const int serversInUse = 10;
            for (var i = 0; i < serversInUse; i++)
            {
                var result = this.balancer.TryAddServer(this.servers[i], FeedbackLevel.Lowest);
                Assert.IsTrue(result);
            }

            var serversInLB = this.balancer.GetServerStates();
            Assert.That(serversInLB.Count(s => s.IsReserved), Is.EqualTo(2));
            Assert.That(serversInLB.Count(s => s.IsInReserve), Is.EqualTo(2));

            this.balancer.TryRemoveServer(this.servers[0]);

            serversInLB = this.balancer.GetServerStates();
            Assert.That(serversInLB.Count(s => s.IsReserved), Is.EqualTo(1));
            Assert.That(serversInLB.Count(s => s.IsInReserve), Is.EqualTo(1));

            Assert.IsTrue(this.balancer.TryAddServer(this.servers[0], FeedbackLevel.Lowest));

            serversInLB = this.balancer.GetServerStates();
            Assert.That(serversInLB.Count(s => s.IsReserved), Is.EqualTo(2));
            Assert.That(serversInLB.Count(s => s.IsInReserve), Is.EqualTo(2));
        }

        [Test]
        public void ServerReserveRatioTest08()
        {
            this.balancer = new LoadBalancer<Server>
            {
                PriorityDownThreshold = FeedbackLevel.Level7,
                PriorityUpThreshold = FeedbackLevel.Level8,
                ReserveRatio = 0.8f
            };

            const int serversInUse = 10;
            for (var i = 0; i < serversInUse; i++)
            {
                var result = this.balancer.TryAddServer(this.servers[i], FeedbackLevel.Lowest);
                Assert.IsTrue(result);
            }

            var serversInLB = this.balancer.GetServerStates();
            Assert.That(serversInLB.Count(s => s.IsReserved), Is.EqualTo(8));
            Assert.That(serversInLB.Count(s => s.IsInReserve), Is.EqualTo(8));

            this.balancer.TryRemoveServer(this.servers[0]);

            serversInLB = this.balancer.GetServerStates();
            Assert.That(serversInLB.Count(s => s.IsReserved), Is.EqualTo(7));
            Assert.That(serversInLB.Count(s => s.IsInReserve), Is.EqualTo(7));

            Assert.IsTrue(this.balancer.TryAddServer(this.servers[0], FeedbackLevel.Lowest));

            serversInLB = this.balancer.GetServerStates();
            Assert.That(serversInLB.Count(s => s.IsReserved), Is.EqualTo(8));
            Assert.That(serversInLB.Count(s => s.IsInReserve), Is.EqualTo(8));
        }

        [Test]
        public void ServerReserveTest()
        {
            this.balancer = new LoadBalancer<Server>
            {
                PriorityDownThreshold = FeedbackLevel.Level7,
                PriorityUpThreshold = FeedbackLevel.Level8,
                ReserveRatio = 0.2f
            };

            const int serversInUse = 10;
            for (var i = 0; i < serversInUse; i++)
            {
                var result = this.balancer.TryAddServer(this.servers[i], FeedbackLevel.Lowest);
                Assert.IsTrue(result);
            }

            var serversInLB = this.balancer.GetServerStates();
            Assert.That(serversInLB.Count(s => s.IsReserved), Is.EqualTo(2));
            Assert.That(serversInLB.Count(s => s.IsInReserve), Is.EqualTo(2));

            foreach (var serverStateData in serversInLB)
            {
                if (!serverStateData.IsReserved)
                {
                    this.balancer.TryUpdateServer(serverStateData.Server, FeedbackLevel.Level8);
                }
            }

            Assert.That(serversInLB.Count(s => s.IsReserved), Is.EqualTo(2));
            Assert.That(serversInLB.Count(s => s.IsInReserve), Is.EqualTo(1));

            // this will set LoadLeve8 to just added server
            foreach (var serverStateData in serversInLB)
            {
                if (!serverStateData.IsInReserve)
                {
                    this.balancer.TryUpdateServer(serverStateData.Server, FeedbackLevel.Level8);
                }
            }

            Assert.That(serversInLB.Count(s => s.IsReserved), Is.EqualTo(2));
            Assert.That(serversInLB.Count(s => s.IsInReserve), Is.EqualTo(0));

            foreach (var serverStateData in serversInLB.Take(8))
            {
                this.balancer.TryUpdateServer(serverStateData.Server, FeedbackLevel.Level6);
            }

            Assert.That(serversInLB.Count(s => s.IsInReserve), Is.EqualTo(1));

            foreach (var serverStateData in serversInLB)
            {
                this.balancer.TryUpdateServer(serverStateData.Server, FeedbackLevel.Level6);
            }

            Assert.That(serversInLB.Count(s => s.IsInReserve), Is.EqualTo(2));
        }

        [Test]
        public void ReserveSortingTest()
        {
            this.balancer = new LoadBalancer<Server>
            {
                PriorityDownThreshold = FeedbackLevel.Level7,
                PriorityUpThreshold = FeedbackLevel.Level8,
                ReserveRatio = 0.2f
            };

            var serversInUse = 5;
            for (var i = 0; i < serversInUse; i++)
            {
                var result = this.balancer.TryAddServer(this.servers[i], FeedbackLevel.Lowest);
                Assert.IsTrue(result);
            }

            var serversInLB = this.balancer.GetServerStates();
            Assert.That(serversInLB.Count(s => s.IsReserved), Is.EqualTo(1));
            Assert.That(serversInLB.Count(s => s.IsInReserve), Is.EqualTo(1));
            Assert.That(serversInLB.First(s => s.IsReserved).Server.Name, Is.EqualTo(this.servers[serversInUse -1].Name));

            Assert.IsTrue(this.balancer.TryAddServer(this.servers[serversInUse ], FeedbackLevel.Lowest));

            serversInLB = this.balancer.GetServerStates();
            Assert.That(serversInLB.Count(s => s.IsReserved), Is.EqualTo(1));
            Assert.That(serversInLB.Count(s => s.IsInReserve), Is.EqualTo(1));
            Assert.That(serversInLB.First(s => s.IsReserved).Server.Name, Is.EqualTo(this.servers[serversInUse].Name));

            this.balancer.TryRemoveServer(this.servers[serversInUse]);

            serversInLB = this.balancer.GetServerStates();
            Assert.That(serversInLB.Count(s => s.IsReserved), Is.EqualTo(1));
            Assert.That(serversInLB.Count(s => s.IsInReserve), Is.EqualTo(1));
            Assert.That(serversInLB.First(s => s.IsReserved).Server.Name, Is.EqualTo(this.servers[serversInUse - 1].Name));

            this.balancer.TryUpdateServer(this.servers[0], FeedbackLevel.Level2);

            serversInLB = this.balancer.GetServerStates();
            Assert.That(serversInLB.Count(s => s.IsReserved), Is.EqualTo(1));
            Assert.That(serversInLB.Count(s => s.IsInReserve), Is.EqualTo(1));
            Assert.That(serversInLB.First(s => s.IsReserved).Server.Name, Is.EqualTo(this.servers[serversInUse - 1].Name));

        }

        [Test]
        public void ServerReserveRemoveReservedServerTest()
        {
            this.balancer = new LoadBalancer<Server>
            {
                PriorityDownThreshold = FeedbackLevel.Level7,
                PriorityUpThreshold = FeedbackLevel.Level8,
                ReserveRatio = 0.2f
            };

            const int serversInUse = 10;
            for (var i = 0; i < serversInUse; i++)
            {
                var result = this.balancer.TryAddServer(this.servers[i], FeedbackLevel.Lowest);
                Assert.IsTrue(result);
            }

            var serversInLB = this.balancer.GetServerStates();
            Assert.That(serversInLB.Count(s => s.IsReserved), Is.EqualTo(2));
            Assert.That(serversInLB.Count(s => s.IsInReserve), Is.EqualTo(2));

            serversInLB = this.balancer.GetServerStates();
            foreach (var serverStateData in serversInLB)
            {
                if (serverStateData.IsReserved)
                {
                    this.balancer.TryRemoveServer(serverStateData.Server);
                }
            }

            serversInLB = this.balancer.GetServerStates();
            Assert.That(serversInLB.Count(s => s.IsReserved), Is.EqualTo(1));
            Assert.That(serversInLB.Count(s => s.IsInReserve), Is.EqualTo(1));
        }

        /// <summary>
        /// all cases of removal in one test
        /// </summary>
        [Test]
        public void ServerReserveServerRemovalTest()
        {
            this.balancer = new LoadBalancer<Server>
            {
                PriorityDownThreshold = FeedbackLevel.Level7,
                PriorityUpThreshold = FeedbackLevel.Level8,
                ReserveRatio = 0.2f
            };

            const int serversInUse = 10;
            for (var i = 0; i < serversInUse; i++)
            {
                var result = this.balancer.TryAddServer(this.servers[i], FeedbackLevel.Lowest);
                Assert.IsTrue(result);
            }

            var serversInLB = this.balancer.GetServerStates();
            Assert.That(serversInLB.Count(s => s.IsReserved), Is.EqualTo(2));
            Assert.That(serversInLB.Count(s => s.IsInReserve), Is.EqualTo(2));

            // remove server from reserve
            serversInLB = this.balancer.GetServerStates();
            foreach (var serverStateData in serversInLB)
            {
                if (serverStateData.IsReserved)
                {
                    this.balancer.TryRemoveServer(serverStateData.Server);
                    break;
                }
            }

            serversInLB = this.balancer.GetServerStates();
            Assert.That(serversInLB.Count(s => s.IsReserved), Is.EqualTo(1));
            Assert.That(serversInLB.Count(s => s.IsInReserve), Is.EqualTo(1));

            // remove last server from reserve
            serversInLB = this.balancer.GetServerStates();
            foreach (var serverStateData in serversInLB)
            {
                if (serverStateData.IsReserved)
                {
                    this.balancer.TryRemoveServer(serverStateData.Server);
                    break;
                }
            }

            // new reserve server selected
            serversInLB = this.balancer.GetServerStates();
            Assert.That(serversInLB.Count(s => s.IsReserved), Is.EqualTo(1));
            Assert.That(serversInLB.Count(s => s.IsInReserve), Is.EqualTo(1));

            // normal server remove, but there is still enough servers for reserve
            this.balancer.TryRemoveServer(this.servers[0]);

            serversInLB = this.balancer.GetServerStates();
            Assert.That(serversInLB.Count(s => s.IsReserved), Is.EqualTo(1));
            Assert.That(serversInLB.Count(s => s.IsInReserve), Is.EqualTo(1));

            // normal servers remove and there is not enough servers for reserve
            this.balancer.TryRemoveServer(this.servers[1]);
            this.balancer.TryRemoveServer(this.servers[2]);

            serversInLB = this.balancer.GetServerStates();
            Assert.That(serversInLB.Count(s => s.IsReserved), Is.EqualTo(1));
            Assert.That(serversInLB.Count(s => s.IsInReserve), Is.EqualTo(1));
        }

        [Test]
        public void ServerReserveUpdateReservedServerLoadTest()
        {
            this.balancer = new LoadBalancer<Server>
            {
                PriorityDownThreshold = FeedbackLevel.Level7,
                PriorityUpThreshold = FeedbackLevel.Level8,
                ReserveRatio = 0.2f
            };

            const int serversInUse = 10;
            for (var i = 0; i < serversInUse; i++)
            {
                var result = this.balancer.TryAddServer(this.servers[i], FeedbackLevel.Lowest);
                Assert.IsTrue(result);
            }

            var serversInLB = this.balancer.GetServerStates();
            Assert.That(serversInLB.Count(s => s.IsReserved), Is.EqualTo(2));
            Assert.That(serversInLB.Count(s => s.IsInReserve), Is.EqualTo(2));

            foreach (var serverStateData in serversInLB)
            {
                if (serverStateData.IsInReserve)
                {
                    this.balancer.TryUpdateServer(serverStateData.Server, FeedbackLevel.Level8);
                }
            }

            Assert.That(serversInLB.Count(s => s.IsInReserve), Is.EqualTo(2));
            Assert.That(serversInLB.Count(s => s.IsInReserve && s.IsInAvailableList), Is.EqualTo(0));
        }

        [Test]
        public void ServerReserveUpdateReservedServerStateTest()
        {
            this.balancer = new LoadBalancer<Server>
            {
                PriorityDownThreshold = FeedbackLevel.Level7,
                PriorityUpThreshold = FeedbackLevel.Level8,
                ReserveRatio = 0.2f
            };

            const int serversInUse = 10;
            for (var i = 0; i < serversInUse; i++)
            {
                var result = this.balancer.TryAddServer(this.servers[i], FeedbackLevel.Lowest);
                Assert.IsTrue(result);
            }

            var serversInLB = this.balancer.GetServerStates();
            Assert.That(serversInLB.Count(s => s.IsReserved), Is.EqualTo(2));
            Assert.That(serversInLB.Count(s => s.IsInReserve), Is.EqualTo(2));

            foreach (var serverStateData in serversInLB)
            {
                if (serverStateData.IsInReserve)
                {
                    this.balancer.TryUpdateServer(serverStateData.Server, FeedbackLevel.Level2, 0, ServerState.Offline);
                }
            }

            Assert.That(serversInLB.Count(s => s.IsInReserve), Is.EqualTo(2));
            Assert.That(serversInLB.Count(s => s.IsInReserve && s.IsInAvailableList), Is.EqualTo(0));

            foreach (var serverStateData in serversInLB)
            {
                if (serverStateData.IsInReserve)
                {
                    this.balancer.TryUpdateServer(serverStateData.Server, FeedbackLevel.Level2, 0, ServerState.Normal);
                }
            }

            Assert.That(serversInLB.Count(s => s.IsInReserve), Is.EqualTo(2));
            Assert.That(serversInLB.Count(s => s.IsInReserve && s.IsInAvailableList), Is.EqualTo(0));
        }

        [Test]
        public void ServerReserveWithPrioritiesTest()
        {
            this.balancer = new LoadBalancer<Server>
            {
                PriorityDownThreshold = FeedbackLevel.Level7,
                PriorityUpThreshold = FeedbackLevel.Level8,
                ReserveRatio = 0.2f
            };

            const int serversInUse = 10;
            for (var i = 0; i < serversInUse/2; i++)
            {
                var result = this.balancer.TryAddServer(this.servers[i], FeedbackLevel.Lowest);
                Assert.IsTrue(result);
            }
            for (var i = serversInUse/2; i < serversInUse; i++)
            {
                var result = this.balancer.TryAddServer(this.servers[i], FeedbackLevel.Lowest, priority:1);
                Assert.IsTrue(result);
            }

            var serversInLB = this.balancer.GetServerStates();
            Assert.That(serversInLB.Count(s => s.IsReserved), Is.EqualTo(2));
            Assert.That(serversInLB.Count(s => s.IsInReserve), Is.EqualTo(2));

            foreach (var serverStateData in serversInLB)
            {
                if (!serverStateData.IsReserved && serverStateData.Priority == 0)
                {
                    this.balancer.TryUpdateServer(serverStateData.Server, FeedbackLevel.Level8);
                }
            }

            Assert.That(serversInLB.Count(s => s.IsReserved), Is.EqualTo(2));
            Assert.That(serversInLB.Count(s => s.IsInReserve), Is.EqualTo(1));

            // this will set LoadLeve8 to just added from reserve server
            foreach (var serverStateData in serversInLB)
            {
                if (serverStateData.IsInAvailableList && serverStateData.IsReserved)
                {
                    // after update servers with priority 1 will be added to list
                    this.balancer.TryUpdateServer(serverStateData.Server, FeedbackLevel.Level8);
                }
            }

            Assert.That(serversInLB.Count(s => s.IsInAvailableList), Is.EqualTo(9));
            Assert.That(serversInLB.Count(s => s.IsInReserve), Is.EqualTo(1));

            foreach (var serverStateData in serversInLB)
            {
                if (serverStateData.IsInAvailableList)
                {
                    // after update priorty 1 servers reserve will be included
                    this.balancer.TryUpdateServer(serverStateData.Server, FeedbackLevel.Level8);
                }
            }

            Assert.That(serversInLB.Count(s => s.IsInAvailableList), Is.EqualTo(9));
            Assert.That(serversInLB.Count(s => s.IsInReserve), Is.EqualTo(1));

            foreach (var serverStateData in serversInLB)
            {
                if (serverStateData.IsInAvailableList)
                {
                    // after update priorty 1 reserve server will be included
                    this.balancer.TryUpdateServer(serverStateData.Server, FeedbackLevel.Level8, serverStateData.Priority);
                }
            }

            Assert.That(serversInLB.Count(s => s.IsInAvailableList), Is.EqualTo(10));
            Assert.That(serversInLB.Count(s => s.IsInReserve), Is.EqualTo(0));

            // pririty 0 servers loading now low. we get rid of priority 1 servers and all reserve servers
            foreach (var serverStateData in serversInLB.Take(5))
            {
                this.balancer.TryUpdateServer(serverStateData.Server, FeedbackLevel.Level3);
            }

            Assert.That(serversInLB.Count(s => s.IsInAvailableList), Is.EqualTo(4));
            Assert.That(serversInLB.Count(s => s.IsInReserve), Is.EqualTo(2));
        }

        [Test]
        public void ServerReserveUpdateLoadForUsedReserveTest()
        {
            this.balancer = new LoadBalancer<Server>
            {
                PriorityDownThreshold = FeedbackLevel.Level5,
                PriorityUpThreshold = FeedbackLevel.Level7,
                ReserveRatio = 0.2f
            };

            const int serversInUse = 5;
            for (var i = 0; i < serversInUse; i++)
            {
                var result = this.balancer.TryAddServer(this.servers[i], FeedbackLevel.Lowest);
                Assert.IsTrue(result);
            }

            var serversInLB = this.balancer.GetServerStates();
            Assert.That(serversInLB.Count(s => s.IsReserved), Is.EqualTo(1));
            Assert.That(serversInLB.Count(s => s.IsInReserve), Is.EqualTo(1));

            foreach (var serverStateData in serversInLB)
            {
                if (!serverStateData.IsReserved)
                {
                    this.balancer.TryUpdateServer(serverStateData.Server, FeedbackLevel.Level7);
                }
            }

            Assert.That(serversInLB.Count(s => s.IsReserved), Is.EqualTo(1));
            Assert.That(serversInLB.Count(s => s.IsInReserve), Is.EqualTo(0));

            // this will set LoadLeve8 to just added server
            foreach (var serverStateData in serversInLB)
            {
                if (serverStateData.IsReserved)
                {
                    this.balancer.TryUpdateServer(serverStateData.Server, FeedbackLevel.Level6);
                }
            }
        }

        #region Helpers

        private void AssignServerLoop(int count)
        {
            this.ResetServerCount();

            for (int i = 0; i < count; i++)
            {
                Server server;
                bool result = this.balancer.TryGetServer(out server);
                Assert.IsTrue(result);
                server.Count++;
            }
        }

        private void AssignServerLoopWithFilter(int count, Func<Server, bool> filter)
        {
            this.ResetServerCount();

            for (int i = 0; i < count; i++)
            {
                Server server;
                bool result = this.balancer.TryGetServer(out server, filter);
                Assert.IsTrue(result);
                server.Count++;
            }
        }

        private void ResetServerCount()
        {
            for (int i = 0; i < this.servers.Count; i++)
            {
                this.servers[i].Count = 0;
            }
        }

        private void CheckServer(Server server, int count, int expectedPercent, int toleranceInPercent)
        {
            int expectedCount = count * expectedPercent / 100;
            int tolerance = Math.Abs(expectedCount * toleranceInPercent / 100);

            int difference = Math.Abs(expectedCount - server.Count);
            if (difference > 2 * tolerance)
            {
                Assert.Fail(
                    "{0} has an unexpected count of assignments. Expected a value between {1} and {2} but is {3}", 
                    server.Name, 
                    expectedCount - tolerance, 
                    expectedCount + tolerance, 
                    server.Count);
            }
        }

        private void TryAddServer(Server server, FeedbackLevel loadLevel, bool expectedResult = true)
        {
            var result = this.balancer.TryAddServer(server, loadLevel);
            Assert.AreEqual(result, expectedResult);
        }

        private void TryUpdateServer(Server server, FeedbackLevel newLoadLevel, bool expectedResult = true)
        {
            var result = this.balancer.TryUpdateServer(server, newLoadLevel);
            Assert.AreEqual(expectedResult, result, "Unexpected update server result.");
        }

        private void TryGetServer(out Server server, bool expectedResult = true)
        {
            var result = this.balancer.TryGetServer(out server);
            Assert.AreEqual(expectedResult, result);
        }

        private void TryRemoveServer(Server server, bool expectedResult = true)
        {
            bool result = this.balancer.TryRemoveServer(server);
            Assert.AreEqual(expectedResult, result);
        }

        private void CheckLoadBalancerProperties(int totalWorkload, int totalWeight, int averageWorkloadPercent)
        {
            Assert.AreEqual(totalWorkload, this.balancer.TotalWorkload);
            Assert.AreEqual(totalWeight, this.balancer.ServersInUseWeight);
            Assert.AreEqual(averageWorkloadPercent, 25 * (int)this.balancer.AverageWorkload);
        }

        #endregion
    }
}
