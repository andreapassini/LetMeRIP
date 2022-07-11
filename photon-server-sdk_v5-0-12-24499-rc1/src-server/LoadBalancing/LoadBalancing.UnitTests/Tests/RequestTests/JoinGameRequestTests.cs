using NUnit.Framework;
using Photon.Hive.Operations;
using Photon.SocketServer;
using Photon.SocketServer.Rpc.Protocols;
using System.Collections;

namespace Photon.LoadBalancing.UnitTests.Tests.RequestTests
{
    [TestFixture]
    public class JoinGameRequestTests
    {
        [Test]
        public void JoinGameRequestTest()
        {
            var request = new SocketServer.OperationRequest((byte) OperationCode.JoinGame)
            {
                Parameters = new System.Collections.Generic.Dictionary<byte, object>()
                {
                    {(byte)ParameterKey.GameProperties, new Hashtable {{(byte)GameParameter.EmptyRoomTTL, 0}}},
                    {(byte)ParameterKey.GameId, null},
                    {(byte)ParameterKey.RoomOptionFlags, null},
                },
                RequestMetaData = new SocketServer.Rpc.Protocols.RequestMetaData()
            };

            var requestObj = new CreateGameRequest(Photon.SocketServer.Protocol.GpBinaryV162, request, "", 0);
            Assert.IsTrue(requestObj.IsValid, requestObj.GetErrorMessage());
        }

        [Test]
        public void JoinGameRequestTextPropertiesTest()
        {
            var controller = new InboundController((byte)OperationCode.JoinGame, (byte)OperationCode.JoinGame, 
                (byte)ParameterKey.GameProperties, (byte)ParameterKey.GameProperties);
            controller.SetupOperationParameter((byte)OperationCode.JoinGame,
                (byte)ParameterKey.GameProperties, new ParameterData(InboundController.PROVIDE_SIZE_OF_SUB_KEYS));
            Protocol.InboundController = controller;

            var request = new SocketServer.OperationRequest((byte) OperationCode.JoinGame)
            {
                Parameters = new System.Collections.Generic.Dictionary<byte, object>()
                {
                    {
                        (byte)ParameterKey.GameProperties, new Hashtable
                        {
                            {(byte)GameParameter.EmptyRoomTTL, 1}, 
                            {250, new [] {"test1", "test2"}}
                        }
                    },
                    {(byte)ParameterKey.GameId, "test"},
                    {(byte)ParameterKey.RoomOptionFlags, null},
                },
                RequestMetaData = new SocketServer.Rpc.Protocols.RequestMetaData()
            };

            var data = Protocol.GpBinaryV162.SerializeOperationRequest(request);
            Protocol.GpBinaryV162.TryParseOperationRequest(data, out request, out _, out _);

            var requestObj = new JoinGameRequest(Photon.SocketServer.Protocol.GpBinaryV162, request, "", 0);
            Assert.IsTrue(requestObj.IsValid, requestObj.GetErrorMessage());

            var paramMetaData = request.RequestMetaData[(byte) ParameterKey.GameProperties];
            foreach (var v in requestObj.GameProperties.Keys)
            {
                Assert.IsTrue(paramMetaData.SubtypeMetaData.ContainsKey(v));
            }
        }
    }
}
