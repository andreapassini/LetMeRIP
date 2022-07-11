using System;
using System.Collections;
using NUnit.Framework;
using Photon.Hive.Collections;
using Photon.Hive.Events;
using Photon.Hive.Operations;

namespace Photon.Hive.Tests
{
    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public class RoomEventCacheManagerTests
    {
        [Test]
        public void EventCacheSliceLimitTests()
        {
            RoomEventCacheManager manager = new RoomEventCacheManager();

            const int SlicesCountLimit = 2;
            const int ChchedEventsCountLimit = 4;
            manager.SlicesCountLimit = SlicesCountLimit;
            manager.CachedEventsCountLimit = ChchedEventsCountLimit;

            manager.AddSlice(0);
            manager.AddSlice(1);
            Assert.Throws<EventCacheException>(() => manager.AddSlice(3), "Slice limit is exceeded. Limit is 2");
            manager.RemoveSlice(1);

            Assert.DoesNotThrow(() => manager.AddSlice(3));
        }

        [Test]
        public void EventCacheCachedEventsTotalLimitTests()
        {
            RoomEventCacheManager manager = new RoomEventCacheManager();

            const int SlicesCountLimit = 2;
            const int ChchedEventsCountLimit = 4;
            manager.SlicesCountLimit = SlicesCountLimit;
            manager.CachedEventsCountLimit = ChchedEventsCountLimit;

            string msg;
            manager.AddEventToCurrentSlice(new CustomEvent(0, 0, null), out msg);
            manager.AddEventToCurrentSlice(new CustomEvent(1, 0, null), out msg);

            ++manager.Slice;

            Assert.That(manager.AddEventToCurrentSlice(new CustomEvent(2, 0, null), out msg));
            Assert.That(manager.AddEventToCurrentSlice(new CustomEvent(3, 0, null), out msg));
            Assert.That(manager.AddEventToCurrentSlice(new CustomEvent(3, 0, null), out msg));
            Assert.That(manager.IsTotalLimitExceeded, Is.True);

            manager.RemoveEventsByActor(2);
            Assert.That(manager.IsTotalLimitExceeded, Is.False);
            Assert.That(manager.AddEventToCurrentSlice(new CustomEvent(3, 0, null), out msg));
            Assert.That(manager.IsTotalLimitExceeded, Is.True);
        }

        [Test]
        public void EventCache_ClearCache()
        {
            RoomEventCacheManager manager = new RoomEventCacheManager();

            string msg;
            manager.AddEventToCurrentSlice(new CustomEvent(0, 0, null), out msg);
            manager.AddEventToCurrentSlice(new CustomEvent(1, 0, null), out msg);

            Assert.That(manager.AddEventToCurrentSlice(new CustomEvent(2, 0, null), out msg));
            Assert.That(manager.AddEventToCurrentSlice(new CustomEvent(3, 0, null), out msg));
            Assert.That(manager.AddEventToCurrentSlice(new CustomEvent(3, 0, null), out msg));

            Assert.That(manager.CachedEventsCount, Is.Not.Zero);

            manager.RemoveEventsFromCache(new RaiseEventRequest());

            Assert.That(manager.CachedEventsCount, Is.Zero);

            Assert.That(manager.AddEventToCurrentSlice(new CustomEvent(2, 0, null), out msg));
            Assert.That(manager.AddEventToCurrentSlice(new CustomEvent(3, 0, null), out msg));
            Assert.That(manager.AddEventToCurrentSlice(new CustomEvent(3, 0, null), out msg));

            Assert.That(manager.CachedEventsCount, Is.Not.Zero);

            manager.RemoveEventsFromCache(0, null, null);

            Assert.That(manager.CachedEventsCount, Is.Zero);

        }

        [Test]
        public void EventCache_RemoveEventsPerActor()
        {
            RoomEventCacheManager manager = new RoomEventCacheManager();

            string msg;
            manager.AddEventToCurrentSlice(new CustomEvent(0, 0, null), out msg);
            manager.AddEventToCurrentSlice(new CustomEvent(1, 0, null), out msg);

            Assert.That(manager.AddEventToCurrentSlice(new CustomEvent(2, 0, null), out msg));
            Assert.That(manager.AddEventToCurrentSlice(new CustomEvent(3, 0, null), out msg));
            Assert.That(manager.AddEventToCurrentSlice(new CustomEvent(3, 0, null), out msg));

            manager.RemoveEventsFromCache(new RaiseEventRequest{Actors = new []{3},});

            Assert.That(manager.CachedEventsCount, Is.EqualTo(3));

            Assert.That(manager.AddEventToCurrentSlice(new CustomEvent(2, 0, null), out msg));
            Assert.That(manager.AddEventToCurrentSlice(new CustomEvent(3, 1, null), out msg));
            Assert.That(manager.AddEventToCurrentSlice(new CustomEvent(3, 2, null), out msg));

            Assert.That(manager.CachedEventsCount, Is.EqualTo(6));

            manager.RemoveEventsFromCache(0, new int[] {3}, null);

            Assert.That(manager.CachedEventsCount, Is.EqualTo(4));
        }

        [Test]
        public void EventCache_RemoveEventsPerEventCodeAndActor()
        {
            RoomEventCacheManager manager = new RoomEventCacheManager();

            string msg;
            manager.AddEventToCurrentSlice(new CustomEvent(0, 0, null), out msg);
            manager.AddEventToCurrentSlice(new CustomEvent(1, 0, null), out msg);

            Assert.That(manager.AddEventToCurrentSlice(new CustomEvent(2, 0, null), out msg));
            Assert.That(manager.AddEventToCurrentSlice(new CustomEvent(3, 1, null), out msg));
            Assert.That(manager.AddEventToCurrentSlice(new CustomEvent(3, 2, null), out msg));

            Assert.That(manager.CachedEventsCount, Is.EqualTo(5));

            byte eventCode = 2;
            manager.RemoveEventsFromCache(eventCode, new int[] {3}, null);

            Assert.That(manager.CachedEventsCount, Is.EqualTo(4));

            eventCode = 1;
            manager.RemoveEventsFromCache(eventCode, new int[] {3}, null);

            Assert.That(manager.CachedEventsCount, Is.EqualTo(3));

        }

        public enum DataOptions
        {
            Null, 
            Frst,
            Scnd,
            Wrng
        }

        public enum UserIds
        {
            NullId,
            UsrId3,
            UsrId2,
            UrId23,
            WrogId
        }

        [TestCase(0, UserIds.NullId, DataOptions.Null, 0)]//completly remove all events
        [TestCase(0, UserIds.UsrId3, DataOptions.Null, 3)] // remove using actor ids
        [TestCase(0, UserIds.UrId23, DataOptions.Null, 2)] // remove using actor ids
        [TestCase(0, UserIds.NullId, DataOptions.Frst, 2)] // remove using data as filter
        [TestCase(0, UserIds.NullId, DataOptions.Scnd, 4)] // remove using data as filter
        [TestCase(1, UserIds.NullId, DataOptions.Null, 4)]//remove using event code
        [TestCase(1, UserIds.UsrId3, DataOptions.Null, 4)] // remove using event code and actor ids
        [TestCase(1, UserIds.WrogId, DataOptions.Null, 5)] // remove using event code and actor ids, not existing actor id
        [TestCase(5, UserIds.UsrId3, DataOptions.Null, 5)] // remove using event code and actor ids, non existing event code
        [TestCase(1, UserIds.NullId, DataOptions.Frst, 4)] // remove using event code and data as filter
        [TestCase(1, UserIds.NullId, DataOptions.Wrng, 5)] // remove using event code and data as filter, not existing data
        [TestCase(2, UserIds.NullId, DataOptions.Wrng, 5)] // remove using event code and data as filter, non existing combination
        [TestCase(0, UserIds.UsrId3, DataOptions.Frst, 4)] // remove using actor ids and data
        [TestCase(1, UserIds.UsrId3, DataOptions.Frst, 4)] // remove using event code, actor ids and data
        [TestCase(3, UserIds.UsrId2, DataOptions.Scnd, 4)] // remove using event code, actor ids and data, non existing combination
        public void EventCache_RemoveEventsPerUsingDifferentCombinations(byte eventCode, UserIds userIds, DataOptions dataOptions, int expectedCount)
        {
            var data = GetTestData(dataOptions);
            var actors = this.GetActorsArray(userIds);

            var manager = AddEventsToCacheManager();

            manager.RemoveEventsFromCache(new RaiseEventRequest{Actors = actors, EvCode = eventCode, Data = data});

            Assert.That(manager.CachedEventsCount, Is.EqualTo(expectedCount));

            manager = AddEventsToCacheManager();

            manager.RemoveEventsFromCache(eventCode, actors, data);

            Assert.That(manager.CachedEventsCount, Is.EqualTo(expectedCount));

        }

        [Test]
        public void TestRemoveEventsForActorsNotInList()
        {
            var actors = this.GetActorsArray(UserIds.UrId23);

            var manager = AddEventsToCacheManager();

            manager.RemoveEventsForActorsNotInList(actors);

            Assert.That(manager.CachedEventsCount, Is.EqualTo(4));
        }

        #region Methods

        private int[] GetActorsArray(UserIds userIds)
        {
            switch (userIds)
            {
                case UserIds.NullId:
                    return null;
                case UserIds.UsrId3:
                    return new[] {3};
                case UserIds.UsrId2:
                    return new[] {2};
                case UserIds.UrId23:
                    return new[] {2, 3};
                case UserIds.WrogId:
                    return new[] {6};
                default:
                    throw new ArgumentOutOfRangeException("userIds", userIds, null);
            }
        }

        private static object GetTestData(DataOptions dataOptions)
        {
            switch (dataOptions)
            {
                case DataOptions.Null:
                    return null;
                case DataOptions.Frst:
                    return new Hashtable{{1, 1}};
                case DataOptions.Scnd:
                    return new Hashtable{{2, 2}};
                case DataOptions.Wrng:
                    return new Hashtable{{5, 1}};
                default:
                    throw new ArgumentOutOfRangeException("dataOptions", dataOptions, null);
            }
        }

        private static RoomEventCacheManager AddEventsToCacheManager()
        {
            RoomEventCacheManager manager = new RoomEventCacheManager();

            string msg;
            manager.AddEventToCurrentSlice(new CustomEvent(0, 0, GetTestData(DataOptions.Frst)), out msg);
            manager.AddEventToCurrentSlice(new CustomEvent(1, 0, GetTestData(DataOptions.Frst)), out msg);

            Assert.That(manager.AddEventToCurrentSlice(new CustomEvent(2, 3, GetTestData(DataOptions.Scnd)), out msg));
            Assert.That(manager.AddEventToCurrentSlice(new CustomEvent(3, 1, GetTestData(DataOptions.Frst)), out msg));
            Assert.That(manager.AddEventToCurrentSlice(new CustomEvent(3, 2, null), out msg));

            Assert.That(manager.CachedEventsCount, Is.EqualTo(5));

            return manager;
        }

        #endregion
    }
}
