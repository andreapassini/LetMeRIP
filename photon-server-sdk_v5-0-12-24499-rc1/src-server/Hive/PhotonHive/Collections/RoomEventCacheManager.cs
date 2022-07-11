using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ExitGames.Logging;
using Photon.Hive.Caching;
using Photon.Hive.Diagnostics;
using Photon.Hive.Events;
using Photon.Hive.Operations;

namespace Photon.Hive.Collections
{
    public class RoomEventCacheManager
    {
        #region Constants and Fields

        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private readonly SortedList<int, RoomEventCache> eventCache = new SortedList<int, RoomEventCache>();

        private IHiveGameAppCounters gameAppCounters = NullHiveGameAppCounters.Instance;

        private int currentSlice;

        private int cachedEventsCount;

        #endregion

        #region Constructors/Destructors

        public RoomEventCacheManager()
        {
            this.SlicesCountLimit = 1000;
            this.CachedEventsCountLimit = 10000;
        }
        #endregion

        #region Properties
        public int Count { get { return this.eventCache.Count; } }

        public int CachedEventsCount
        {
            get { return this.cachedEventsCount; }
        }

        public int Slice
        {
            get
            {
                return this.currentSlice;
            }
            
            set
            {
                this.AddSliceNX(value);
                this.currentSlice = value;
            }
        }

        public IEnumerable<int> Slices
        {
            get { return this.eventCache.Keys; }
        }

        public int CachedEventsCountLimit { get; set; }
        public int SlicesCountLimit { get; set; }

        public int MaxCachedEventsInTotal { get; private set; }

        public int MaxCachedEventsPerSlice { get; private set; }

        public int MaxSlicesCount { get; private set; }

        public bool IsSlicesLimitExceeded { get { return this.eventCache.Count > this.SlicesCountLimit; } }

        public bool IsTotalLimitExceeded { get { return this.cachedEventsCount > this.CachedEventsCountLimit; } }

        public IEnumerable<CustomEvent> this[int slice]
        {
            get { return this.eventCache[slice]; }
        }

        public bool Discarded { get; private set; }
        #endregion//Properties

        #region Publics

        public void SetGameAppCounters(IHiveGameAppCounters counters)
        {
            if (counters == null)
            {
                log.Error("Someone sets null counters to EventCache manager");
                counters = NullHiveGameAppCounters.Instance;
            }
            this.gameAppCounters = counters;
        }

        public int GetSliceSize(int slice)
        {
            return this.eventCache[slice].Count;
        }

        public CustomEvent GetCustomEvent(int slice, int number)
        {
            return this.eventCache[slice][number];
        }

        public bool HasSlice(int sliceId)
        {
            return this.eventCache.ContainsKey(sliceId);
        }

        public void AddSlice(int sliceId)
        {
            if (this.eventCache.Count == this.SlicesCountLimit)
            {
                throw new EventCacheException("Slice limit is exceeded. Limit is " + this.SlicesCountLimit);
            }
            this.eventCache.Add(sliceId, new RoomEventCache());
            this.gameAppCounters.EventCacheSliceCountIncrement();

            if (this.MaxSlicesCount < this.eventCache.Count)
            {
                this.MaxSlicesCount = this.eventCache.Count;
            }
        }

        public bool RemoveSlice(int slice)
        {
            if (slice == this.currentSlice)
                return false;

            IntRemoveSlice(slice);

            return true;
        }

        public bool RemoveUpToSlice(int slice)
        {
            var result = false;

            var first = this.eventCache.Keys.First();

            for (var i = first; i < slice; i++)
            {
                if (IntRemoveSlice(i))
                {
                    result = true;
                }
            }
            return result;
        }

        public void RemoveEventsByActor(int actorNr)
        {
            foreach (var cache in this.eventCache.Values)
            {
                var count = cache.RemoveEventsByActor(actorNr);
                this.gameAppCounters.EventCacheTotalEventsDecrementBy(count);
                this.cachedEventsCount -= count;
            }
        }

        public void RemoveEventsFromCache(RaiseEventRequest raiseEventRequest)
        {
            this.RemoveEventsFromCache(raiseEventRequest.EvCode, raiseEventRequest.Actors, raiseEventRequest.Data);
        }

        public void RemoveEventsFromCache(byte evCode, IList<int> actors, object data)
        {
            foreach (var slice in this.eventCache.Values)
            {
                var removedCount = slice.RemoveEvents(evCode, actors, data);
                this.gameAppCounters.EventCacheTotalEventsDecrementBy(removedCount);
                this.cachedEventsCount -= removedCount;
            }
        }

        public void RemoveEventsForActorsNotInList(IEnumerable<int> currentActorNumbers)
        {
            foreach (var slice in this.eventCache.Values)
            {
                var removedCount = slice.RemoveEventsForActorsNotInList(currentActorNumbers);
                this.gameAppCounters.EventCacheTotalEventsDecrementBy(removedCount);
                this.cachedEventsCount -= removedCount;
            }
        }

        public bool AddEvent(int slice, CustomEvent customEvent, out string msg)
        {
            this.AddSliceNX(slice);
            var sliceObj = this.eventCache[slice];

            sliceObj.AddEvent(customEvent);
            this.gameAppCounters.EventCacheTotalEventsIncrement();
            ++this.cachedEventsCount;

            if (this.MaxCachedEventsInTotal < this.cachedEventsCount)
            {
                this.MaxCachedEventsInTotal = this.cachedEventsCount;
            }

            if (this.MaxCachedEventsPerSlice < sliceObj.Count)
            {
                this.MaxCachedEventsPerSlice = sliceObj.Count;
            }

            msg = string.Empty;
            return true;
        }

        public bool AddEventToCurrentSlice(CustomEvent customEvent, out string msg)
        {
            return this.AddEvent(this.Slice, customEvent, out msg);
        }

        public void SetDeserializedData(Dictionary<int, object[]> dict)
        {
            foreach (var slice in dict)
            {
                if (this.eventCache.ContainsKey(slice.Key))
                {
                    RoomEventCache cache;
                    if (this.eventCache.TryGetValue(slice.Key, out cache))
                    {
                        this.gameAppCounters.EventCacheTotalEventsDecrementBy(cache.Count);
                        this.cachedEventsCount -= cache.Count;
                    }
                    this.eventCache.Remove(slice.Key);
                }
                this.eventCache.Add(slice.Key, new RoomEventCache());
                foreach (IList<object> evdata in slice.Value)
                {
                    this.eventCache[slice.Key].AddEvent(new CustomEvent(evdata));
                    this.gameAppCounters.EventCacheTotalEventsIncrement();
                    ++this.cachedEventsCount;
                }
            }
        }

        public Dictionary<int, ArrayList> GetSerializationData(out int evCount)
        {
            evCount = 0;
            var events = new Dictionary<int, ArrayList>();
            foreach (var aslice in this.eventCache.Where(aslice => aslice.Value.Count > 0))
            {
                var evList = new ArrayList();
                foreach (var ev in aslice.Value)
                {
                    evList.Add(ev.AsList());
                    evCount++;
                }
                events.Add(aslice.Key, evList);
            }
            return events;
        }

        public void DiscardCache()
        {
            if (this.Discarded)
            {
                return;
            }

            if (log.IsDebugEnabled)
            {
                log.Debug("Discarding event cache");
            }
            var slices = this.eventCache.Keys.ToArray();
            foreach (var slice in slices)
            {
                this.IntRemoveSlice(slice);
            }

            if (log.IsDebugEnabled)
            {
                log.Debug("Event cache discarded");
            }
            this.Discarded = true;
        }

        #endregion

        #region Methods

        private bool IntRemoveSlice(int slice)
        {
            RoomEventCache cache;
            if (this.eventCache.TryGetValue(slice, out cache))
            {
                this.gameAppCounters.EventCacheTotalEventsDecrementBy(cache.Count);
                this.cachedEventsCount -= cache.Count;

                this.eventCache.Remove(slice);
                this.gameAppCounters.EventCacheSliceCountDecrement();
                return true;
            }
            return false;
        }

        private void AddSliceNX(int sliceId)
        {
            if (!this.HasSlice(sliceId) || this.eventCache[sliceId] == null)
            {
                this.AddSlice(sliceId);
            }
        }

        #endregion

    }
}
