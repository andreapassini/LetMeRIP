// --------------------------------------------------------------------------------------------------------------------
// <copyright file="SetupFixture.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   The setup fixture.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System;
using Microsoft.Extensions.Configuration;
using Photon.SocketServer;

namespace Photon.Hive.Tests
{
    using System.IO;

    using ExitGames.Logging;
    using ExitGames.Logging.Log4Net;

    using log4net.Config;

    using NUnit.Framework;

    class PhotonApp : ApplicationBase
    {
        public PhotonApp()
            : base(LoadConfiguration())
        { }

        private static IConfiguration LoadConfiguration()
        {
            var cb = new ConfigurationBuilder();
            var cbpath = Path.GetDirectoryName(typeof(PhotonApp).Assembly.CodeBase).Remove(0, 6);
            return cb.AddXmlFile(Path.Combine(cbpath, "Hive.xml.config")).Build();
        }

        protected override PeerBase CreatePeer(InitRequest initRequest)
        {
            throw new NotImplementedException();
        }

        protected override void Setup()
        {
            throw new NotImplementedException();
        }

        protected override void TearDown()
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// The setup fixture.
    /// </summary>
    [SetUpFixture]
    public class SetupFixture
    {
        /// <summary>
        /// The setup.
        /// </summary>
        [OneTimeSetUp]
        public void Setup()
        {
            Environment.CurrentDirectory = TestContext.CurrentContext.TestDirectory;

            LogManager.SetLoggerFactory(Log4NetLoggerFactory.Instance);
            var fileInfo = new FileInfo("log4net.config");
            XmlConfigurator.Configure(fileInfo);

            var _ = new PhotonApp();
        }
    }
}