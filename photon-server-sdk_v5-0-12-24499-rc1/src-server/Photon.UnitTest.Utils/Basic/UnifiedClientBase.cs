using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using NUnit.Framework;

namespace Photon.UnitTest.Utils.Basic
{
    public abstract class UnifiedClientBase : IDisposable
    {
        public int WaitTimeout;

        protected UnifiedClientBase(INUnitClient client)
        {
            this.Client = client;
        }

        public object Token { get; set; }
        public string UserId { get; set; }

        public INUnitClient Client { get; private set; }

        public bool Connected { get { return this.Client.Connected; } }
        public string RemoteEndPoint { get { return this.Client.RemoteEndPoint; } }

        public void EventQueueClear()
        {
            if (this.Client != null)
            {
                this.Client.EventQueueClear();
            }
        }

        public void OperationResponseQueueClear()
        {
            if (this.Client != null)
            {
                this.Client.OperationResponseQueueClear();
            }
        }

        public virtual void Connect(string serverAddress, byte[] token = null, object custom = null)
        {
            if (this.Connected)
            {
                this.Disconnect();
            }

            this.Client.Connect(serverAddress, token, custom);
            Assert.IsTrue(this.Client.WaitForConnect(ConnectPolicy.WaitTime) && this.Client.Connected,
                "Test was unable to connect to server (addr:{0}, for specified time ({1})",serverAddress, ConnectPolicy.WaitTime);
        }

        public void Disconnect()
        {
            if (this.Client != null)
            {
                this.Client.Disconnect();
                this.Client.WaitForDisconnect();
                this.Client.EventQueueClear();
                this.Client.OperationResponseQueueClear();
            }
        }

        public EventData WaitForEvent(int millisecodsWaitTime = ConnectPolicy.WaitTime)
        {
            if (this.Client != null)
            {
                return this.Client.WaitForEvent(millisecodsWaitTime);
            }
            throw new Exception("Client is not set");
        }

        public EventData WaitForEvent(byte eventCode, int millisecodsWaitTime = ConnectPolicy.WaitTime)
        {
            var timeout = Environment.TickCount + millisecodsWaitTime;
            while (Environment.TickCount < timeout)
            {
                var eventData = this.WaitForEvent(timeout - Environment.TickCount);
                if (eventCode == eventData.Code)
                {
                    return eventData;
                }
            }
            throw new TimeoutException();
        }

        public ExitGames.Client.Photon.EventData WaitEvent(byte eventCode, int millisecodsWaitTime = ConnectPolicy.WaitTime)
        {
            if (this.Client != null)
            {
                return this.Client.WaitEvent(eventCode, millisecodsWaitTime);
            }
            throw new Exception("Client is not set");
        }

        public bool TryWaitForEvent(int waitTime, out EventData eventData)
        {
            eventData = null;
            try
            {
                eventData = this.WaitForEvent(waitTime);
            }
            catch (TimeoutException)
            {
                return false;
            }

            return true;
        }

        public bool TryWaitForEvent(byte eventCode, int waitTime, out EventData eventData)
        {
            var now = DateTime.Now;
            eventData = null;
            try
            {
                eventData = WaitForEvent(eventCode, waitTime);
            }
            catch (TimeoutException)
            {
                var timeout = DateTime.Now;
                Console.WriteLine("Timeout happened. time passed {0}, expectedTimeout:{1}", timeout - now, waitTime);
                return false;
            }
            return true;
        }

        public bool TryWaitEvent(byte eventCode, int waitTime, out EventData eventData)
        {
            var now = DateTime.Now;
            eventData = null;
            try
            {
                eventData = WaitEvent(eventCode, waitTime);
            }
            catch (TimeoutException)
            {
                var timeout = DateTime.Now;
                Console.WriteLine("Timeout happened. time passed {0}, expectedTimeout:{1}", timeout - now, waitTime);
                return false;
            }
            return true;
        }


        public ExitGames.Client.Photon.OperationResponse WaitForOperationResponse(int milliseconsWaitTime = ConnectPolicy.WaitTime)
        {
            if (this.Client != null)
            {
                return this.Client.WaitForOperationResponse(milliseconsWaitTime);
            }
            throw new Exception("Client is not set");
        }

        public bool TryWaitForOperationResponse(int waitTime, out OperationResponse response)
        {
            response = null;
            try
            {
                response = this.WaitForOperationResponse(waitTime);
            }
            catch (TimeoutException)
            {
                return false;
            }

            return true;
        }

        public bool SendRequest(OperationRequest op, bool encrypted = false)
        {
            if (this.Client != null)
            {
                return this.Client.SendRequest(op, encrypted);
            }
            return false;
        }

        public OperationResponse SendRequestAndWaitForResponse(OperationRequest request, short expectedResult = 0, bool encrypted = false)
        {
            Assert.That(this.SendRequest(request, encrypted), Is.True);
            var response = this.WaitForOperationResponse(this.WaitTimeout);
            Assert.AreEqual(request.OperationCode, response.OperationCode);
            if (response.ReturnCode != expectedResult)
            {
                Assert.Fail("Request failed: opCode={0}, expected return code {1} but got returnCode={2}, msg={3}", request.OperationCode, expectedResult, response.ReturnCode, response.DebugMessage);
            }

            return response;
        }

        public void CheckThereIsNoEvent(byte eventCode, int timeout = 1000)
        {
            EventData eventData;
            Assert.IsFalse(this.TryWaitEvent(eventCode, timeout, out eventData), "Got unexpected event {0}", eventCode);
        }

        /// <summary>
        /// Check that there is specified event
        /// Events should be checked in order which they arrive from server
        /// </summary>
        /// <param name="eventCode"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public EventData CheckThereIsEvent(byte eventCode, int timeout = 1000)
        {
            EventData eventData;
            Assert.IsTrue(this.TryWaitEvent(eventCode, timeout, out eventData), "Did not get expected event {0}", eventCode);
            return eventData;
        }

        protected static OperationRequest CreateOperationRequest(byte operationCode)
        {
            return new OperationRequest
            {
                OperationCode = operationCode,
                Parameters = new Dictionary<byte, object>()
            };
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool dispose)
        {
            if (this.Client != null)
            {
                this.Client.Dispose();
                this.Client = null;
            }
        }

        public void InitEncryption()
        {
            if (this.Client != null)
            {
                if (this.Client.NetworkProtocol != SocketServer.NetworkProtocolType.SecureWebSocket)
                {
                    this.Client.InitEncryption();
                }
            }
        }
    }
}