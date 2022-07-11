using System.Text;
using NUnit.Framework;
using Photon.Hive.Operations;
using Photon.SocketServer;

namespace Photon.LoadBalancing.UnitTests.Tests.RequestTests
{
    [TestFixture]
    public class RaiseEventRequestTests
    {
        [Test]
        public void WebFlagsFromJson()
        {
            string json = @"~m~75~m~~j~{""req"":253,""vals"":[244,1,245,{""message"":"""",""senderName"":""user1""},234,1]}";
            byte[] data = Encoding.UTF8.GetBytes(json);

            Protocol.Json.TryParseOperationRequest(data, out var r, out _, out _);

            var req = new RaiseEventRequest(Protocol.Json, r);

            Assert.IsTrue(req.IsValid);
            Assert.IsTrue(req.HttpForward);
            Assert.That(req.WebFlags, Is.EqualTo(1));
        }

        [Test]
        public void WebFlagsFromJsonValueTooBig()
        {
            string json = @"~m~75~m~~j~{""req"":253,""vals"":[244,1,245,{""message"":"""",""senderName"":""user1""},234,257]}";
            byte[] data = Encoding.UTF8.GetBytes(json);

            Protocol.Json.TryParseOperationRequest(data, out var r, out _, out _);

            var req = new RaiseEventRequest(Protocol.Json, r);

            Assert.IsFalse(req.IsValid);
        }

        [Test]
        public void WebFlagsFromJsonNoValue()
        {
            string json = @"~m~75~m~~j~{""req"":253,""vals"":[244,1,245,{""message"":"""",""senderName"":""user1""}]}";

            byte[] data = Encoding.UTF8.GetBytes(json);

            Protocol.Json.TryParseOperationRequest(data, out var r, out _, out _);

            var req = new RaiseEventRequest(Protocol.Json, r);

            Assert.IsTrue(req.IsValid);
        }
    }
}
