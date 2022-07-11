using NUnit.Framework;

namespace Photon.Hive.Tests
{
    [TestFixture]
    public class GameStateTests
    {
        [Test]
        public void SuppressFlagsTest()
        {
            var state = new GameState
            {
                SuppressPlayerInfo = true
            };

            Assert.That(state.SuppressRoomEvents, Is.True);
            Assert.That(state.SuppressPlayerInfo, Is.True);

            state = new GameState
            {
                SuppressRoomEvents = true
            };

            Assert.That(state.SuppressRoomEvents, Is.True);
            Assert.That(state.SuppressPlayerInfo, Is.False);
        }
    }
}
