using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Photon.Plugins.Common;

namespace Photon.LoadBalancing.UnitTests.Tests
{
    [TestFixture]
    public class PropertyValueComparerTests
    {
        [Test]
        public void NotEqualByteArrays()
        {
            var v1 = new byte[] { 1, 2, 3 };
            var v2 = new byte[] { 1, 2, 3 , 4};

            Assert.That(PropertyValueComparer.Compare(v1, v2), Is.False);
            Assert.That(PropertyValueComparer.Compare(v2, v1), Is.False);
        }

        [Test]
        public void EqualByteArrays()
        {
            var v1 = new byte[] { 1, 2, 3 };
            var v2 = new byte[] { 1, 2, 3 };

            Assert.That(PropertyValueComparer.Compare(v1, v2), Is.True);
        }

        [Test]
        public void NotEqualStringArrays()
        {
            var v1 = new string[] { "1", "2", "3" };
            var v2 = new string[] { "1", "2", "3", "4" };

            Assert.That(PropertyValueComparer.Compare(v1, v2), Is.False);
            Assert.That(PropertyValueComparer.Compare(v2, v1), Is.False);
        }

        [Test]
        public void EqualStringArrays()
        {
            var v1 = new string[] {"1", "2", "3"};
            var v2 = new string[] {"1", "2", "3"};

            Assert.That(PropertyValueComparer.Compare(v1, v2), Is.True);
        }

        [Test]
        public void NotEqualDictionary()
        {
            var v1 = new Dictionary<byte, string> {{1, "1"}, {2, "2"}};
            var v2 = new Dictionary<byte, string> {{1, "1"}, {3, "3"}};

            Assert.That(PropertyValueComparer.Compare(v1, v2), Is.False);
            Assert.That(PropertyValueComparer.Compare(v2, v1), Is.False);
        }

        [Test]
        public void EqualDictionary()
        {
            var v1 = new Dictionary<byte, string> { { 1, "1" }, { 2, "2" } };
            var v2 = new Dictionary<byte, string> { { 1, "1" }, { 2, "2" } };

            Assert.That(PropertyValueComparer.Compare(v1, v2), Is.True);
        }

        [Test]
        public void NotEqualDictionaryWithByteArray()
        {
            var v1 = new Dictionary<byte, byte[]> { { 1, new byte[] { 1, 2, 3 } } };
            var v2 = new Dictionary<byte, byte[]> { { 1, new byte[] { 1, 2, 3, 4 } } };

            Assert.That(PropertyValueComparer.Compare(v1, v2), Is.False);
            Assert.That(PropertyValueComparer.Compare(v2, v1), Is.False);
        }

        [Test]
        public void EqualDictionaryWithNulls()
        {
            var v1 = new Dictionary<byte, byte[]> { { 1, null } };
            var v2 = new Dictionary<byte, byte[]> { { 1, null } };

            Assert.That(PropertyValueComparer.Compare(v1, v2), Is.True);
        }

        [Test]
        public void NotEqualDictionaryNulls()
        {
            var v1 = new Dictionary<byte, byte[]> { { 1, null } };
            var v2 = new Dictionary<byte, byte[]> { { 1, new byte[] { 1, 2, 3, 4 } } };

            Assert.That(PropertyValueComparer.Compare(v1, v2), Is.False);
            Assert.That(PropertyValueComparer.Compare(v2, v1), Is.False);
        }

        [Test]
        public void EqualDictionaryWithByteArray()
        {
            var v1 = new Dictionary<byte, byte[]> { { 1, new byte[] { 1, 2, 3 } } };
            var v2 = new Dictionary<byte, byte[]> { { 1, new byte[] { 1, 2, 3 } } };

            Assert.That(PropertyValueComparer.Compare(v1, v2), Is.True);
        }

        [Test]
        public void NotEqualHashtableWithByteArray()
        {
            var v1 = new Hashtable { { 1, new byte[] { 1, 2, 3 } } };
            var v2 = new Hashtable { { 1, new byte[] { 1, 2, 3, 4 } } };

            Assert.That(PropertyValueComparer.Compare(v1, v2), Is.False);
            Assert.That(PropertyValueComparer.Compare(v2, v1), Is.False);
        }

        [Test]
        public void EqualHashtableWithByteArray()
        {
            var v1 = new Hashtable { { 1, new byte[] { 1, 2, 3 } } };
            var v2 = new Hashtable { { 1, new byte[] { 1, 2, 3 } } };

            Assert.That(PropertyValueComparer.Compare(v1, v2), Is.True);
        }

        [Test]
        public void NotEqualHashtableWithNulls()
        {
            var v1 = new Hashtable {{1, null}, {2, 3}};
            var v2 = new Hashtable {{1, null}, {3, 4}};

            Assert.That(PropertyValueComparer.Compare(v1, v2), Is.False);
            Assert.That(PropertyValueComparer.Compare(v2, v1), Is.False);
        }

        [Test]
        public void EqualHashtableWithNulls()
        {
            var v1 = new Hashtable { { 1, null }, { 2, 3 } };
            var v2 = new Hashtable { { 1, null }, { 2, 3 } };

            Assert.That(PropertyValueComparer.Compare(v1, v2), Is.True);
        }

        [Test]
        public void NullValueComparison()
        {
            Assert.That(PropertyValueComparer.Compare(null, "x"), Is.False);
            Assert.That(PropertyValueComparer.Compare("x", null), Is.False);
            Assert.That(PropertyValueComparer.Compare(1, null), Is.False);
            Assert.That(PropertyValueComparer.Compare(null, null), Is.True);
        }
    }
}
