using System;
using System.Collections.Generic;
using Photon.SocketServer;
using EventData = ExitGames.Client.Photon.EventData;
using OperationRequest = ExitGames.Client.Photon.OperationRequest;
using OperationResponse = ExitGames.Client.Photon.OperationResponse;

namespace Photon.UnitTest.Utils.Basic
{
    public interface INUnitClient : IDisposable
    {
        NetworkProtocolType NetworkProtocol { get; }
        bool Connected { get; }
        string RemoteEndPoint { get; }
        /// <summary>
        /// All incoming message via OnMessage will be buffered into List with timestamp
        /// </summary>
        bool EnableOnMessageReceiveBuffer { get; set; }
        Dictionary<long, object> OnMessageBuffer { get; set; }

        void Connect(string serverAddress, byte[] token = null, object custom = null);
        void Disconnect();

        bool SendRequest(OperationRequest op, bool encrypted);

        void SendMessage(object message);

        EventData WaitForEvent(int millisecodsWaitTime = ConnectPolicy.WaitTime);
        EventData WaitEvent(byte eventCode, int millisecodsWaitTime = ConnectPolicy.WaitTime);
        OperationResponse WaitForOperationResponse(int milliseconsWaitTime = ConnectPolicy.WaitTime);

        bool WaitForConnect(int timeout = ConnectPolicy.WaitTime);
        bool WaitForDisconnect(int timeout = ConnectPolicy.WaitTime);

        void EventQueueClear();
        void OperationResponseQueueClear();
        void InitEncryption();
        void SetupEncryption(Dictionary<byte, object> encryptionData);
    }
}
