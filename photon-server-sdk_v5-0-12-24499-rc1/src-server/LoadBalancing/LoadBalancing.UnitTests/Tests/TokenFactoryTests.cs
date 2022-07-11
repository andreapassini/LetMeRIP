using System;
using System.Threading;
using NUnit.Framework;
using Photon.Common.Authentication;
using Photon.LoadBalancing.Operations;

namespace Photon.LoadBalancing.UnitTests.Tests
{
    [TestFixture]
    public class TokenFactoryTests
    {
        [Test]
        public void BinaryFormatTests()
        {
            var secret = Guid.NewGuid().ToString();
            var hmac = Guid.NewGuid().ToString();

            var tc = new AuthTokenFactory();
            tc.Initialize(secret, hmac, new TimeSpan(0, 1, 0, 0), Environment.MachineName);

            var authRequest = new AuthenticateRequest
            {
                ApplicationId = Guid.NewGuid().ToString(),
                UserId = "UserId"
            };
            var token = tc.CreateAuthenticationToken(authRequest.UserId, authRequest);

            var tokenByteArray = tc.EncryptAuthenticationTokenBinary(token, false);
            string errorMsg;
            Assert.That(tc.DecryptAuthenticationTokenBinary(tokenByteArray, 0, tokenByteArray.Length, out token, out errorMsg), Is.True);
        }

        [Test]
        public void FinalExpireAtTests()
        {
            var secret = Guid.NewGuid().ToString();
            var hmac = Guid.NewGuid().ToString();

            var tc = new AuthTokenFactory();
            tc.Initialize(secret, hmac, new TimeSpan(1, 0, 0), Environment.MachineName);

            var authRequest = new AuthenticateRequest
            {
                ApplicationId = Guid.NewGuid().ToString(),
                UserId = "UserId"
            };
            var token = tc.CreateAuthenticationToken(authRequest.UserId, authRequest);

            token.FinalExpireAtTicks = DateTime.UtcNow.Add(new TimeSpan(1, 0, 1)).Ticks;

            Thread.Sleep(100);

            var tokenByteArray = tc.EncryptAuthenticationTokenBinary(token, true);

            AuthenticationToken token2;
            string errorMsg;
            Assert.That(tc.DecryptAuthenticationTokenBinary(tokenByteArray, 0, tokenByteArray.Length, out token2, out errorMsg), Is.True);

            Assert.AreEqual(token.FinalExpireAtTicks, token2.FinalExpireAtTicks);
            Assert.AreEqual(token.ExpireAtTicks, token2.ExpireAtTicks);

            tokenByteArray = tc.EncryptAuthenticationTokenBinary(token, true);
            Assert.Less(token2.ExpireAtTicks, token.ExpireAtTicks);

            AuthenticationToken token3;
            Assert.That(tc.DecryptAuthenticationTokenBinary(tokenByteArray, 0, tokenByteArray.Length, out token3, out errorMsg), Is.True);

            Assert.AreEqual(token.FinalExpireAtTicks, token3.FinalExpireAtTicks);
            Assert.AreEqual(token.ExpireAtTicks, token3.ExpireAtTicks);
        }

        [Test]
        public void TokenIssuerTests()
        {
            var secret = Guid.NewGuid().ToString();
            var hmac = Guid.NewGuid().ToString();

            var tc = new AuthTokenFactory();
            tc.Initialize(secret, hmac, new TimeSpan(1, 0, 0), Environment.MachineName);

            var authRequest = new AuthenticateRequest
            {
                ApplicationId = Guid.NewGuid().ToString(),
                UserId = "UserId"
            };
            var token = tc.CreateAuthenticationToken(authRequest.UserId, authRequest);

            Assert.AreEqual(Environment.MachineName, token.TokenIssuer);
        }

        [Test]
        public void TokenIssuerTests2()
        {
            var secret = Guid.NewGuid().ToString();
            var hmac = Guid.NewGuid().ToString();

            var tc = new AuthTokenFactory();
            tc.Initialize(secret, hmac, new TimeSpan(1, 0, 0), Environment.MachineName);

            var tc2 = new AuthTokenFactory();
            tc2.Initialize(secret, hmac, new TimeSpan(1, 0, 0), "SecondName");

            var authRequest = new AuthenticateRequest
            {
                ApplicationId = Guid.NewGuid().ToString(),
                UserId = "UserId"
            };
            var token = tc.CreateAuthenticationToken(authRequest.UserId, authRequest);

            var encrypted = tc2.EncryptAuthenticationToken(token, true);

            AuthenticationToken token2;
            string errorMsg;
            tc.DecryptAuthenticationToken(encrypted, out token2, out errorMsg);

            Assert.That(token2.TokenIssuer, Is.EqualTo("SecondName"));
        }

    }
}
