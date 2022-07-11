using System;
using NUnit.Framework;
using Photon.Common.Authentication.Encryption;
using Photon.SocketServer;
using Photon.SocketServer.Security;

namespace Photon.LoadBalancing.UnitTests.Tests
{
    [TestFixture]
    public class EncryptionDataGeneratorTests
    {
        [Test]
        public void EncryptinDataGeneationTest([Values]EncryptionModes encryptionMode)
        {
            var d = EncryptionDataGenerator.Generate((byte)encryptionMode);

            var ed = new EncryptionData(Protocol.GpBinaryV162, d);

            Assert.That(ed.IsValid);

            Assert.That((EncryptionModes)ed.EncryptionMode, Is.EqualTo(encryptionMode));

            switch (encryptionMode)
            {
                case EncryptionModes.PayloadEncryption:
                    Assert.That(ed.EncryptionSecret, Is.Not.Null);
                    Assert.That(ed.EncryptionSecret.Length, Is.EqualTo(32));

                    Assert.That(ed.AuthSecret, Is.Null);
                    break;
                case EncryptionModes.PayloadEncryptionWithIV:
                    Assert.That(ed.EncryptionSecret, Is.Not.Null);
                    Assert.That(ed.EncryptionSecret.Length, Is.EqualTo(32));

                    Assert.That(ed.AuthSecret, Is.Null);
                    break;
                case EncryptionModes.PayloadEncryptionWithIVHMAC:
                    Assert.That(ed.EncryptionSecret, Is.Not.Null);
                    Assert.That(ed.EncryptionSecret.Length, Is.EqualTo(32));

                    Assert.That(ed.AuthSecret, Is.Not.Null);
                    Assert.That(ed.AuthSecret.Length, Is.EqualTo(32));
                    break;
                case EncryptionModes.DatagramEncryption:
                    Assert.That(ed.EncryptionSecret, Is.Not.Null);
                    Assert.That(ed.EncryptionSecret.Length, Is.EqualTo(32));

                    Assert.That(ed.AuthSecret, Is.Not.Null);
                    Assert.That(ed.AuthSecret.Length, Is.EqualTo(32));
                    break;
                case EncryptionModes.DatagramEncryptionWithRandomInitialNumbers:
                    Assert.That(ed.EncryptionSecret, Is.Not.Null);
                    Assert.That(ed.EncryptionSecret.Length, Is.EqualTo(32));

                    Assert.That(ed.AuthSecret, Is.Not.Null);
                    Assert.That(ed.AuthSecret.Length, Is.EqualTo(32));
                    break;
                case EncryptionModes.DatagramEncryptionGCM:
                    Assert.That(ed.EncryptionSecret, Is.Not.Null);
                    Assert.That(ed.EncryptionSecret.Length, Is.EqualTo(32));

                    Assert.That(ed.AuthSecret, Is.Null);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(encryptionMode), encryptionMode, null);
            }
        }
    }
}
