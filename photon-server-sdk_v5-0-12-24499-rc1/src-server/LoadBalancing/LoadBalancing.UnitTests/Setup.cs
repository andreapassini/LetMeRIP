using System;
using System.IO;

using ExitGames.Logging.Log4Net;

using log4net;
using log4net.Config;

using NUnit.Framework;

using Photon.LoadBalancing.UnitTests.UnifiedServer.OfflineExtra;

using LogManager = ExitGames.Logging.LogManager;

namespace Photon.LoadBalancing.UnitTests
{

    [SetUpFixture]
    public class SetupFixture
    {
        [OneTimeSetUp]
        public void Setup()
        {
            Environment.CurrentDirectory = TestContext.CurrentContext.TestDirectory;

            Console.WriteLine("Initializing log4net ..");
            LogManager.SetLoggerFactory(Log4NetLoggerFactory.Instance);
            GlobalContext.Properties["LogName"] = "TestLog.log";
#if NETSTANDARD || NETCOREAPP
            var logRepository = log4net.LogManager.GetRepository(Assembly.GetEntryAssembly());
            XmlConfigurator.ConfigureAndWatch(logRepository, new FileInfo("tests_log4net.config"));
#else
            XmlConfigurator.ConfigureAndWatch(new FileInfo("tests_log4net.config"));
#endif
            var _ = new ConfigLoadingApplication();
        }
    }
}
