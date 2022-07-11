// --------------------------------------------------------------------------------------------------------------------
// <copyright file="EventCacheDictionary.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Dictionary implementation to store <see cref="EventCache" /> instances by actor number.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Photon.Hive.Caching
{
    #region

    using System;
    using System.Collections;
    using System.Collections.Generic;

    #endregion

    /// <summary>
    /// Dictionary implementation to store <see cref="EventCache"/> instances by actor number.
    /// </summary>
    [Serializable]
    public class EventCacheDictionary : IEnumerable<KeyValuePair<int, EventCache>>
    {
        #region Fields and constants

        private readonly Dictionary<int, EventCache> dictionary = new Dictionary<int, EventCache>();

        private int totalEventsCached;

        #endregion

        #region .ctr

        public EventCacheDictionary()
        {
            this.CachedEventsCountLimit = 10000;
        }

        #endregion

        #region Properties

        public int CachedEventsCountLimit { get; set; }

        public int MaxCachedEventsPerActor { get; private set; }

        public int MaxCachedEventsInTotal { get; private set; }

        public bool IsTotalLimitExceeded { get { return this.totalEventsCached > this.CachedEventsCountLimit; } }

        public bool Discarded { get; private set; }

        #endregion

        #region Public methods

        public EventCache GetOrCreateEventCache(int actorNumber)
        {
            EventCache eventCache;
            if (this.TryGetEventCache(actorNumber, out eventCache) == false)
            {
                eventCache = new EventCache();
                this.dictionary.Add(actorNumber, eventCache);
            }

            return eventCache;
        }

        public bool TryGetEventCache(int actorNumber, out EventCache eventCache)
        {
            return this.dictionary.TryGetValue(actorNumber, out eventCache);
        }

        public bool RemoveEventCache(int actorNumber)
        {
            EventCache eventCache;
            if (!this.TryGetEventCache(actorNumber, out eventCache))
            {
                return false;
            }
            this.totalEventsCached -= eventCache.Count;

            return this.dictionary.Remove(actorNumber);
        }

        public void ReplaceEvent(int actorNumber, byte eventCode, Hashtable eventData)
        {
            var eventCache = this.GetOrCreateEventCache(actorNumber);
            if (eventData == null)
            {
                eventCache.Remove(eventCode);
            }
            else
            {
                eventCache[eventCode] = eventData;
            }
        }

        public bool RemoveEvent(int actorNumber, byte eventCode)
        {
            EventCache eventCache;
            if (!this.dictionary.TryGetValue(actorNumber, out eventCache))
            {
                return false;
            }

            if (eventCache.Remove(eventCode))
            {
                --this.totalEventsCached;
                return true;
            }
            return false;
        }

        public bool MergeEvent(int actorNumber, byte eventCode, Hashtable eventData, out string msg)
        {
            msg = string.Empty;

            // if avent data is null the event will be removed from the cache
            if (eventData == null)
            {
                this.RemoveEvent(actorNumber, eventCode);
                return true;
            }

            var eventCache = this.GetOrCreateEventCache(actorNumber);

            Hashtable storedEventData;
            if (eventCache.TryGetValue(eventCode, out storedEventData) == false)
            {
                eventCache.Add(eventCode, eventData);
                ++this.totalEventsCached;

                if (this.MaxCachedEventsInTotal < this.totalEventsCached)
                {
                    this.MaxCachedEventsInTotal = this.totalEventsCached;
                }

                if (this.MaxCachedEventsPerActor < eventCache.Count)
                {
                    this.MaxCachedEventsPerActor = eventCache.Count;
                }

                return true;
            }

            foreach (DictionaryEntry pair in eventData)
            {
                // null values are removed
                if (pair.Value == null)
                {
                    storedEventData.Remove(pair.Key);
                }
                else
                {
                    storedEventData[pair.Key] = pair.Value;
                }
            }
            return true;
        }

        public IEnumerator<KeyValuePair<int, EventCache>> GetEnumerator()
        {
            return this.dictionary.GetEnumerator();
        }

        public void Discard()
        {
            this.Discarded = true;
            this.dictionary.Clear();
        }
        #endregion

        #region Methods

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.dictionary.GetEnumerator();
        }

        #endregion

    }
}