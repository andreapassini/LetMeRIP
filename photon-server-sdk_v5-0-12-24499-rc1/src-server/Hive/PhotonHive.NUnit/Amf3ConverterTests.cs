using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Photon.Hive.Common;
using Photon.Hive.Operations;

namespace Photon.Hive.Tests
{
    [TestFixture]
    public class Amf3ConverterTests
    {
        [Test]
        public void GameParametersInHashtableTest()
        {
            var values = Enum.GetValues(typeof(GameParameter)).Cast<GameParameter>();

            var hash = new Hashtable();

            foreach (var value in values)
            {
                hash.Add(((byte)value).ToString(), 123);
            }

            Utilities.ConvertAs3WellKnownPropertyKeys(hash, null, null, null);

            foreach (var value in values)
            {
                if (!IsExcluded(value))
                {
                    Assert.That(hash.Contains((byte)value), Is.True, "{0}({1}) not found in converted container", value, (byte)value);
                    Assert.That(hash.Contains(((byte)value).ToString()), Is.False, "{0}({1}) found in converted container", value, ((byte)value));
                }
                else
                {
                    Assert.That(hash.Contains((byte)value), Is.False, "{0}({1}) found in converted container", value, (byte)value);
                    Assert.That(hash.Contains(((byte)value).ToString()), Is.True, "{0}({1}) not found in converted container", value, ((byte)value));
                }
            }
        }

        [Test]
        public void GameParametersInIListTest()
        {
            var values = Enum.GetValues(typeof(GameParameter)).Cast<GameParameter>();

            var hash = new List<object>();

            foreach (var value in values)
            {
                hash.Add(((byte)value).ToString());
            }

            Utilities.ConvertAs3WellKnownPropertyKeys(hash, null);

            foreach (var value in values)
            {
                if (!IsExcluded(value))
                {
                    Assert.That(hash.Contains((byte)value), Is.True, "{0}({1}) not found in converted container", value, (byte)value);
                    Assert.That(hash.Contains(((byte)value).ToString()), Is.False, "{0}({1}) found in converted container", value, ((byte)value));
                }
                else
                {
                    Assert.That(hash.Contains((byte)value), Is.False, "{0}({1}) found in converted container", value, (byte)value);
                    Assert.That(hash.Contains(((byte)value).ToString()), Is.True, "{0}({1}) not found in converted container", value, ((byte)value));
                }
            }

        }

        [Test]
        public void ActorParametersInHashtableTest()
        {
            var values = Enum.GetValues(typeof(ActorParameter)).Cast<ActorParameter>();

            var hash = new Hashtable();

            foreach (var value in values)
            {
                hash.Add(((byte)value).ToString(), 123);
            }

            Utilities.ConvertAs3WellKnownPropertyKeys(null, hash, null, null);

            foreach (var value in values)
            {
                if (!IsExcluded(value))
                {
                    Assert.That(hash.Contains((byte)value), Is.True, "{0}({1}) not found in converted container", value, (byte)value);
                    Assert.That(hash.Contains(((byte)value).ToString()), Is.False, "{0}({1}) found in converted container", value, ((byte)value));
                }
                else
                {
                    Assert.That(hash.Contains((byte)value), Is.False, "{0}({1}) found in converted container", value, (byte)value);
                    Assert.That(hash.Contains(((byte)value).ToString()), Is.True, "{0}({1}) not found in converted container", value, ((byte)value));
                }
            }
        }

        [Test]
        public void ActorParametersInIListTest()
        {
            var values = Enum.GetValues(typeof(ActorParameter)).Cast<ActorParameter>();

            var list = new List<object>();

            foreach (var value in values)
            {
                list.Add(((byte)value).ToString());
            }

            Utilities.ConvertAs3WellKnownPropertyKeys(null, list);

            foreach (var value in values)
            {
                if (!IsExcluded(value))
                {
                    Assert.That(list.Contains((byte)value), Is.True, "{0}({1}) not found in converted container", value, (byte)value);
                    Assert.That(list.Contains(((byte)value).ToString()), Is.False, "{0}({1}) found in converted container", value, ((byte)value));
                }
                else
                {
                    Assert.That(list.Contains((byte)value), Is.False, "{0}({1}) found in converted container", value, (byte)value);
                    Assert.That(list.Contains(((byte)value).ToString()), Is.True, "{0}({1}) not found in converted container", value, ((byte)value));
                }
            }
        }

        private static bool IsExcluded(GameParameter value)
        {
            switch (value)
            {
                case GameParameter.Removed:
                case GameParameter.PlayerCount:
                case GameParameter.MinValue:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsExcluded(ActorParameter value)
        {
            switch(value)
            {
                case ActorParameter.IsInactive:
                case ActorParameter.UserId:
                {
                    return true;
                }
                default:
                    return false;
            }
        }
    }
}
