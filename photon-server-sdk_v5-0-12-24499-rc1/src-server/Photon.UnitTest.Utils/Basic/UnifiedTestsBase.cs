using NUnit.Framework;

namespace Photon.UnitTest.Utils.Basic
{
    public abstract class UnifiedTestsBase
    {
        protected ConnectPolicy connectPolicy;

        protected int WaitTimeout =  ConnectPolicy.WaitTime;

        protected UnifiedTestsBase(ConnectPolicy policy)
        {
            this.connectPolicy = policy;
        }

        protected bool IsOffline { get { return this.connectPolicy.IsOffline; }}

        protected bool IsOnline { get { return !this.IsOffline; } }

        [OneTimeSetUp]
        public void Setup()
        {
            this.connectPolicy.Setup();
            this.FixtureSetup();
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            this.FixtureTearDown();
            this.connectPolicy.TearDown();
        }

        protected virtual void FixtureSetup()
        {
            
        }

        protected virtual void FixtureTearDown()
        {

        }

        protected UnifiedClientBase CreateTestClient()
        {
            var result = this.connectPolicy.CreateTestClient();
            result.WaitTimeout = WaitTimeout;
            return result;
        }

        protected void ConnectToServer(UnifiedClientBase client, string address, byte[] token = null, object custom = null)
        {
            client.Connect(address, token, custom);
            client.InitEncryption();
        }

        /// <summary>
        /// Helper function to dispose a list of test clients
        /// </summary>
        protected static void DisposeClients<T>(params T[] clients) where T : UnifiedClientBase
        {
            if (clients != null)
            {
                for (int i = 0; i < clients.Length; i++)
                {
                    if (clients[i] != null)
                    {
                        clients[i].Dispose();
                    }
                }
            }
        }

        protected static void DisposeClients(UnifiedClientBase client)
        {
            if (client != null)
            {
                client.Dispose();
            }
        }

    }
}