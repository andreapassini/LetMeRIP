using System.Collections;
using System.Linq;
using NUnit.Framework;
using Photon.Common;
using Photon.Hive.Operations;
using Photon.Hive.Tests.Disconnected;

namespace Photon.Hive.Tests
{
    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public class ActorManagerTests
    {
        [SetUp]
        public virtual void Setup()
        {
            var app = new HiveApplication();
            app.OnStart("NUnit", "Lite", new DummyApplicationSink(), null, null, null, string.Empty);
        }

        [Test]
        public void ActorsGetActorsByNumbersTest()
        {
            var manager = new TestActorsManager();

            manager.AddInactive(new TestActor(1, string.Empty));
            manager.AddInactive(new TestActor(2, string.Empty));
            manager.AddInactive(new TestActor(3, string.Empty));
            manager.AddInactive(new TestActor(4, string.Empty));

            var actorIds = new int[] {1, 2, 3, 4};

            var actors = manager.ActorsGetActorsByNumbers(actorIds);
            var arr = actors.ToArray();
            Assert.IsEmpty(arr);
            Assert.AreEqual(0, arr.Length);
        }

        [Test]
        public void ActorsGetActorsByNumbersOneDoesNotExistTest()
        {
            var cache = new TestGameCache();
            var manager = new TestActorsManager();

            var peer1 = new DummyPeer();
            var peer2 = new DummyPeer();
            var peer3 = new DummyPeer();
            var peer4 = new DummyPeer();

            var hivePeer1 = new TestHivePeer(peer1.Protocol, peer1);
            var hivePeer2 = new TestHivePeer(peer2.Protocol, peer2);
            var hivePeer3 = new TestHivePeer(peer3.Protocol, peer3);
            var hivePeer4 = new TestHivePeer(peer4.Protocol, peer4);

            var game = new TestGame(new GameCreateOptions("Test", cache, new TestPluginManager(), 300, new TestGame.TestGameStateFactory()));
            Actor actor;
            bool isNewActor;
            ErrorCode errorCode;
            string reason;

            var joinRequest = new JoinGameRequest()
            {
                PublishUserId = true,
                JoinMode = JoinModes.JoinOnly,
                ActorProperties = new Hashtable {{(byte) ActorParameter.UserId, "a1"}}
            };
            Assert.That(manager.TryAddPeerToGame(
                game, hivePeer1, 0, out actor, out isNewActor, out errorCode, out reason, joinRequest), Is.True);

            joinRequest.ActorProperties[(byte)ActorParameter.UserId] = "a2";
            Assert.That(manager.TryAddPeerToGame(
                game, hivePeer2, 0, out actor, out isNewActor, out errorCode, out reason, joinRequest), Is.True);

            joinRequest.ActorProperties[(byte)ActorParameter.UserId] = "a3";
            Assert.That(manager.TryAddPeerToGame(
                game, hivePeer3, 0, out actor, out isNewActor, out errorCode, out reason, joinRequest), Is.True);

            joinRequest.ActorProperties[(byte)ActorParameter.UserId] = "a4";
            manager.TryAddPeerToGame(game, hivePeer4, 0, out actor, out isNewActor, out errorCode, out reason, joinRequest);

            manager.RemovePeerFromGame(game, hivePeer1, 0, false);
            var actorIds = new int[] 
            {
                1,// does not exist
                2,
                3,
                4
            };

            var actors = manager.ActorsGetActorsByNumbers(actorIds);
            var arrayOfActors = actors.ToArray();

            Assert.That(arrayOfActors, Is.Not.Empty);

            manager.RemovePeerFromGame(game, hivePeer3, 0, false);
            actorIds = new int[]
            {
                1,// does not exist
                2,
                3,// does not exist
                4
            };

            actors = manager.ActorsGetActorsByNumbers(actorIds);
            arrayOfActors = actors.ToArray();

            Assert.That(arrayOfActors, Is.Not.Empty);

        }
    }
}
