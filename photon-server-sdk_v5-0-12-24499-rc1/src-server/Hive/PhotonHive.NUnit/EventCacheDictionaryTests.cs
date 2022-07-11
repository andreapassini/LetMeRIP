using System.Collections;
using NUnit.Framework;
using Photon.Hive.Caching;

namespace Photon.Hive.Tests
{
    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public class EventCacheDictionaryTests
    {
        [Test]
        public void ActorsEventCacheCachedEventsTotalLimitTests()
        {
            var eventCache = new EventCacheDictionary();

            const int ChchedEventsCountLimit = 2;
            eventCache.CachedEventsCountLimit = ChchedEventsCountLimit;

            string msg;
            Assert.That(eventCache.MergeEvent(1, 1, new Hashtable(), out msg));
            Assert.That(eventCache.MergeEvent(2, 1, new Hashtable(), out msg));

            Assert.That(eventCache.MergeEvent(3, 1, new Hashtable(), out msg), Is.True);
            Assert.That(eventCache.IsTotalLimitExceeded, Is.True);

            eventCache.RemoveEvent(1, 1);
            Assert.That(eventCache.IsTotalLimitExceeded, Is.False);

            Assert.That(eventCache.MergeEvent(2, 2, new Hashtable(), out msg));
            Assert.That(eventCache.IsTotalLimitExceeded, Is.True);
        }

        [Test]
        public void MergeEvent()
        {
            var eventCache = new EventCacheDictionary();

            string msg;
            Assert.That(eventCache.MergeEvent(1, 1, new Hashtable{{1, 1}}, out msg));

            var eventData = new Hashtable{{1, 2}};

            Assert.That(eventCache.MergeEvent(1, 1, eventData, out msg), Is.True);

            EventCache ec;
            Assert.That(eventCache.TryGetEventCache(1, out ec), Is.True);

            Hashtable ed;
            Assert.That(ec.TryGetValue(1, out ed), Is.True);

            Assert.That(ed, Is.EqualTo(eventData));
        }

        [Test]
        public void ReplaceEvent()
        {
            var eventCache = new EventCacheDictionary();

            var eventData = new Hashtable{{1, 1}};
            string msg;
            Assert.That(eventCache.MergeEvent(1, 1, eventData, out msg));

            var eventData2 = new Hashtable{{2, 2}};

            eventCache.ReplaceEvent(1, 1, eventData2);

            EventCache ec;
            Assert.That(eventCache.TryGetEventCache(1, out ec), Is.True);

            Hashtable ed;
            Assert.That(ec.TryGetValue(1, out ed), Is.True);

            Assert.That(ed, Is.EqualTo(eventData2));
        }

        [TestCase("UseMerge")]
        [TestCase("UseReplace")]
        public void RemoveEvent(string testCase)
        {
            var eventCache = new EventCacheDictionary();

            var eventData = new Hashtable{{1, 1}};
            string msg;
            Assert.That(eventCache.MergeEvent(1, 1, eventData, out msg));

            if (testCase == "UseMerge")
            {
                Assert.That(eventCache.MergeEvent(1, 1, null, out msg), Is.True);
            }
            else
            {
                Assert.That(eventCache.RemoveEvent(1, 1), Is.True);
            }

        }

        [Test]
        public void RemoveCache()
        {
            var eventCache = new EventCacheDictionary();

            string msg;
            Assert.That(eventCache.MergeEvent(1, 1, new Hashtable{{1, 1}}, out msg));

            var eventData = new Hashtable{{1, 2}};

            Assert.That(eventCache.MergeEvent(1, 1, eventData, out msg), Is.True);

            EventCache ec;
            Assert.That(eventCache.TryGetEventCache(1, out ec), Is.True);

            Assert.That(eventCache.RemoveEventCache(1), Is.True);

            Assert.That(eventCache.TryGetEventCache(1, out ec), Is.False);
        }

    }
}
