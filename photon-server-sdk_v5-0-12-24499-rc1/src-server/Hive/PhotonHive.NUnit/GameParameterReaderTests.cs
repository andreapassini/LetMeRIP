using System.Collections;
using NUnit.Framework;
using Photon.Hive.Common;
using Photon.Hive.Operations;

namespace Photon.Hive.Tests
{
    [TestFixture]
    public class GameParameterReaderTests
    {
        [Test]
        public void TryReadBooleanTest()
        {
            var hash = new Hashtable();

            bool? res;
            object rawValue;
            Assert.That(GameParameterReader.TryReadBooleanParameter(hash, GameParameter.IsOpen, out res, out rawValue, null), Is.True);
            Assert.That(res, Is.Null);
            Assert.That(rawValue, Is.Null);

            hash.Add((byte)GameParameter.IsOpen, true);

            Assert.That(GameParameterReader.TryReadBooleanParameter(hash, GameParameter.IsOpen, out res, out rawValue, null), Is.True);
            Assert.That(res, Is.True);
            Assert.That(rawValue, Is.Not.Null.And.True);

            hash.Clear();
            hash.Add((int)GameParameter.IsOpen, false);

            Assert.That(GameParameterReader.TryReadBooleanParameter(hash, GameParameter.IsOpen, out res, out rawValue, null), Is.True);
            Assert.That(res, Is.False);
            Assert.That(rawValue, Is.Not.Null.And.False);
        }

        [Test]
        public void TryReadByteTest()
        {
            var hash = new Hashtable();

            byte? res;
            object rawValue;
            Assert.That(GameParameterReader.TryReadByteParameter(hash, GameParameter.MaxPlayers, out res, out rawValue, null), Is.True);
            Assert.That(res, Is.Null);
            Assert.That(rawValue, Is.Null);

            hash.Add((byte)GameParameter.MaxPlayers, 123);

            Assert.That(GameParameterReader.TryReadByteParameter(hash, GameParameter.MaxPlayers, out res, out rawValue, null), Is.True);
            Assert.That(res, Is.Not.Null.And.EqualTo(123));
            Assert.That(rawValue, Is.Not.Null.And.EqualTo(123));

            hash.Clear();
            hash.Add((int)GameParameter.MaxPlayers, 123);

            Assert.That(GameParameterReader.TryReadByteParameter(hash, GameParameter.MaxPlayers, out res, out rawValue, null), Is.True);
            Assert.That(res, Is.Not.Null.And.EqualTo(123));
            Assert.That(rawValue, Is.Not.Null.And.EqualTo(123));
        }

        [Test]
        public void TryReadByteTest2()
        {
            var hash = new Hashtable();

            byte? res;
            object rawValue;

            hash.Add((byte)GameParameter.MaxPlayers, (byte)123);

            Assert.That(GameParameterReader.TryReadByteParameter(hash, GameParameter.MaxPlayers, out res, out rawValue, null), Is.True);
            Assert.That(res, Is.Not.Null.And.EqualTo(123));
            Assert.That(rawValue, Is.Not.Null.And.EqualTo(123));

            hash.Clear();
            hash.Add((byte)GameParameter.MaxPlayers, 1234);

            Assert.That(GameParameterReader.TryReadByteParameter(hash, GameParameter.MaxPlayers, out res, out rawValue, null), Is.True);
            unchecked
            {
                Assert.That(res, Is.Not.Null.And.EqualTo((byte)1234));
                Assert.That(rawValue, Is.Not.Null.And.EqualTo(1234));
            }
            hash.Clear();
            double doubleValue = 1234.5;

            hash.Add((byte)GameParameter.MaxPlayers, doubleValue);

            Assert.That(GameParameterReader.TryReadByteParameter(hash, GameParameter.MaxPlayers, out res, out rawValue, null), Is.True);
            unchecked
            {
                Assert.That(res, Is.Not.Null.And.EqualTo((byte)doubleValue));
                Assert.That(rawValue, Is.Not.Null.And.EqualTo(doubleValue));
            }

            hash.Clear();
            hash.Add((byte)GameParameter.MaxPlayers, "zzz");

            Assert.That(GameParameterReader.TryReadByteParameter(hash, GameParameter.MaxPlayers, out res, out rawValue, null), Is.False);
        }

    }
}
