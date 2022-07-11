using NUnit.Framework;
using Photon.Hive.Plugin;
using System.Collections;
using System.Collections.Generic;

namespace Photon.Hive.Tests
{
    [TestFixture]
    public class PropertyBagTests
    {
        [Test]
        public void TotalSizeTest()
        {
            var bag = new PropertyBag<object>();

            var h = new Hashtable
            {
                {1, "xxx" },
                {2, "yyy" },
                {3, "zzz" }
            };

            var meta = new Dictionary<object, KeyValuePair<int, int>>
            {
                {1, new KeyValuePair<int, int>(4, 4) },//real size does not matter. we need something to check calculations
                {2, new KeyValuePair<int, int>(4, 4) },
                {3, new KeyValuePair<int, int>(4, 4) },
            };

            bool valuesChanged = false;
            bag.SetPropertiesCAS(h, null, ref valuesChanged, out _, meta);

            Assert.That(bag.TotalSize, Is.EqualTo(24));

            h = new Hashtable
            {
                {1, "xxx123" },
                {2, "yyy123" },
                {3, "zzz123" }
            };

            meta = new Dictionary<object, KeyValuePair<int, int>>
            {
                {1, new KeyValuePair<int, int>(4, 7) },//real size does not matter. we need something to check calculations
                {2, new KeyValuePair<int, int>(4, 7) },
                {3, new KeyValuePair<int, int>(4, 7) },
            };

            valuesChanged = false;
            bag.SetPropertiesCAS(h, null, ref valuesChanged, out _, meta);

            Assert.That(bag.TotalSize, Is.EqualTo(33));


            h = new Hashtable
            {
                {1, "xxx2345" },
                {2, "yyy2345" },
                {3, "zzz2345" }
            };

            meta = new Dictionary<object, KeyValuePair<int, int>>
            {
                {1, new KeyValuePair<int, int>(4, 10) },//real size does not matter. we need something to check calculations
                {2, new KeyValuePair<int, int>(4, 10) },
                {3, new KeyValuePair<int, int>(4, 10) },
            };

            valuesChanged = false;
            bag.SetPropertiesCAS(h, null, ref valuesChanged, out _, meta);

            Assert.That(bag.TotalSize, Is.EqualTo(42));
        }

        [Test]
        public void TotalSizeAfterClearTest()
        {
            var bag = new PropertyBag<object>();

            var h = new Hashtable
            {
                {1, "xxx" },
                {2, "yyy" },
                {3, "zzz" }
            };

            var meta = new Dictionary<object, KeyValuePair<int, int>>
            {
                {1, new KeyValuePair<int, int>(4, 4) },//real size does not matter. we need something to check calculations
                {2, new KeyValuePair<int, int>(4, 4) },
                {3, new KeyValuePair<int, int>(4, 4) },
            };

            bool valuesChanged = false;
            bag.SetPropertiesCAS(h, null, ref valuesChanged, out _, meta);

            Assert.That(bag.TotalSize, Is.EqualTo(24));

            bag.Clear();
            Assert.That(bag.TotalSize, Is.EqualTo(0));
        }

        [Test]
        public void NoMetaDataTest()
        {
            var bag = new PropertyBag<object>();

            var h = new Hashtable
            {
                {1, "xxx" },
                {2, "yyy" },
                {3, "zzz" }
            };

            bool valuesChanged = false;
            bag.SetPropertiesCAS(h, null, ref valuesChanged, out _, null);

            Assert.That(bag.TotalSize, Is.EqualTo(0));

            h = new Hashtable
            {
                {1, "xxx123" },
                {2, "yyy123" },
                {3, "zzz123" }
            };

            valuesChanged = false;
            bag.SetPropertiesCAS(h, null, ref valuesChanged, out _, null);

            Assert.That(bag.TotalSize, Is.EqualTo(0));
        }

        [Test]
        public void TotalSizeAfterSettingToNullTest()
        {
            var bag = new PropertyBag<object>();

            var h = new Hashtable
            {
                {1, "xxx" },
                {2, "yyy" },
                {3, "zzz" }
            };

            var meta = new Dictionary<object, KeyValuePair<int, int>>
            {
                {1, new KeyValuePair<int, int>(4, 4) },//real size does not matter. we need something to check calculations
                {2, new KeyValuePair<int, int>(4, 4) },
                {3, new KeyValuePair<int, int>(4, 4) },
            };

            bool valuesChanged = false;
            bag.SetPropertiesCAS(h, null, ref valuesChanged, out _, meta);

            Assert.That(bag.TotalSize, Is.EqualTo(24));

            h = new Hashtable
            {
                {1, "xxx123" },
                {2, "yyy123" },
                {3, null }
            };

            meta = new Dictionary<object, KeyValuePair<int, int>>
            {
                {1, new KeyValuePair<int, int>(4, 7) },//real size does not matter. we need something to check calculations
                {2, new KeyValuePair<int, int>(4, 7) },
                {3, new KeyValuePair<int, int>(4, 0) },
            };

            valuesChanged = false;
            bag.SetPropertiesCAS(h, null, ref valuesChanged, out _, meta);

            Assert.That(bag.TotalSize, Is.EqualTo(26));
        }

        [Test]
        public void TotalSizeAfterRemovingTest()
        {
            var bag = new PropertyBag<object> {DeleteNullProps = true};

            var h = new Hashtable
            {
                {3, "zzz" }
            };

            var meta = new Dictionary<object, KeyValuePair<int, int>>
            {
                {3, new KeyValuePair<int, int>(4, 4) },
            };

            bool valuesChanged = false;
            bag.SetPropertiesCAS(h, null, ref valuesChanged, out _, meta);

            Assert.That(bag.TotalSize, Is.EqualTo(8));

            h = new Hashtable
            {
                {3, null }
            };

            meta = new Dictionary<object, KeyValuePair<int, int>>
            {
                {3, new KeyValuePair<int, int>(4, 0) },
            };

            valuesChanged = false;
            bag.SetPropertiesCAS(h, null, ref valuesChanged, out _, meta);

            Assert.That(bag.TotalSize, Is.EqualTo(0));
        }

        [Test]
        public void GetNonExistingProperties()
        {
            var bag = new PropertyBag<object>();

            bag.Set(1, 1);
            bag.Set(2, 1);
            bag.Set(3, 1);
            bag.Set(4, 1);

            var a = new object[] {5, 6, 7};
            var result = bag.GetProperties((IEnumerable)a);

            Assert.That(result, Is.Empty);

            result = bag.GetProperties((IEnumerable<object>)a);

            Assert.That(result, Is.Empty);

            result = bag.GetProperties((IList<object>)a);
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void GetExistingProperties()
        {
            var bag = new PropertyBag<object>();

            bag.Set(1, 1);
            bag.Set(2, 1);
            bag.Set(3, 1);
            bag.Set(4, 1);

            var a = new object[] {1, 2, 3};
            var result = bag.GetProperties((IEnumerable)a);

            Assert.That(result.Count, Is.EqualTo(3));

            result = bag.GetProperties((IEnumerable<object>)a);

            Assert.That(result.Count, Is.EqualTo(3));

            result = bag.GetProperties((IList<object>)a);
            Assert.That(result.Count, Is.EqualTo(3));
        }
    }
}
