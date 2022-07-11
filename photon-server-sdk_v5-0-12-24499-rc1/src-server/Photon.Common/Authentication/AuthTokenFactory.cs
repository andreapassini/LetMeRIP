using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using ExitGames.Logging;
using Photon.Common.Authentication.Encryption;
using Photon.SocketServer.Security;

namespace Photon.Common.Authentication
{
    public class AuthTokenFactory
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private ICryptoProvider CryptoProvider { get; set; }

        private Encryptor encryptor;
        private Decryptor decryptor;

        private TimeSpan ExpirationTime { get; set; }

        private string tokenIssuerName = string.Empty;

        #region Transfer Format Version
        protected const byte Version2 = 0x02;
        #endregion

        public void Initialize(string secret, string authSecret, TimeSpan expirationtime, string tokenIssuer = "")
        {
            Debug.Assert(!string.IsNullOrEmpty(authSecret));

            var sharedKey = System.Text.Encoding.Default.GetBytes(secret);
            var authKey = System.Text.Encoding.Default.GetBytes(authSecret);

            this.ExpirationTime = expirationtime;
            this.tokenIssuerName = tokenIssuer;

            byte[] shaHash;
            byte[] authHash;
            using (var hashProvider = SHA256.Create())
            {
                shaHash = hashProvider.ComputeHash(sharedKey);
                authHash = hashProvider.ComputeHash(authKey);
            }

            this.CryptoProvider = new RijndaelCryptoProvider(shaHash, PaddingMode.PKCS7);
            this.encryptor = new Encryptor();
            this.decryptor = new Decryptor();

            this.encryptor.Init(shaHash, authHash);
            this.decryptor.Init(shaHash, authHash);
        }

        public virtual AuthenticationToken CreateAuthenticationToken(IAuthenticateRequest authRequest, AuthSettings authSettings, string userId, Dictionary<string, object> authCookie)
        {
            var token = new AuthenticationToken
                            {
                                ApplicationId = authRequest.ApplicationId,
                                ApplicationVersion = authRequest.ApplicationVersion,
                                UserId = userId,
                                AuthCookie = authCookie,
                                Flags = authRequest.Flags,
                                CustomAuthProvider = authRequest.ClientAuthenticationType,
                            };

            if (authRequest is IAuthOnceRequest authOnceRequest)
            {
                token.EncryptionData = EncryptionDataGenerator.Generate(authOnceRequest.EncryptionMode);
            }
            this.SetupToken(token);
            return token;
        }

        /// <summary>
        /// Create a renewed Authentication Token on Master server - to be validated on GS
        /// </summary>
        /// <param name="userId"> </param>
        /// <param name="authRequest"></param>
        /// <returns></returns>
        public AuthenticationToken CreateAuthenticationToken(string userId, IAuthenticateRequest authRequest)
        {
            return this.CreateAuthenticationToken(authRequest, null, userId, new Dictionary<string, object>());
        }
       
        public string EncryptAuthenticationToken(AuthenticationToken token, bool renew)
        {
            if (renew)
            {
                this.UpdateValidTo(token);
            }
            token.TokenIssuer = this.tokenIssuerName;

            var tokenData = token.Serialize();
            tokenData = this.CryptoProvider.Encrypt(tokenData);
            return Convert.ToBase64String(tokenData);
        }

        public bool DecryptAuthenticationToken(string authTokenEncrypted, out AuthenticationToken authToken, out string errorMsg)
        {
            return this.DecryptAuthenticationTokenV2(authTokenEncrypted, out authToken, out errorMsg) 
                || this.DecryptAuthenticationTokenV1(authTokenEncrypted, out authToken, out errorMsg);
        }

        public bool DecryptAuthenticationTokenV1(string authTokenEncrypted, out AuthenticationToken authToken, out string errorMsg)
        {
            try
            {
                var tokenData = Convert.FromBase64String(authTokenEncrypted);
                tokenData = this.CryptoProvider.Decrypt(tokenData);
                if (tokenData == null)
                {
                    authToken = null;
                    errorMsg = "Failed to decrypt V1 token";
                    return false;
                }
                return this.TryDeserializeToken(tokenData, out authToken, out errorMsg);
            }
            catch (Exception ex)
            {
                errorMsg = $"DecryptAuthenticationToken failed: Exception Msg:{ex.Message}";

                authToken = null;
                return false;
            }
        }

        public bool DecryptAuthenticationTokenV2(string authTokenEncrypted, out AuthenticationToken authToken, out string errorMsg)
        {
            authToken = null;
            try
            {
                var tokenData = Convert.FromBase64String(authTokenEncrypted);
                switch (tokenData[0])
                {
                    case Version2:
                        return this.DecryptAuthenticationTokenV2(tokenData, out authToken, out errorMsg);
                    default:
                        errorMsg = $"Unknown version of token: {tokenData[0]}";

                        return false;
                }
            }
            catch (Exception ex)
            {
                errorMsg = $"DecryptAuthenticationTokenV2 failed: Exception Msg:{ex.Message}";
                return false;
            }
        }


        public byte[] EncryptAuthenticationTokenBinary(AuthenticationToken token, bool renew)
        {
            if (renew)
            {
                this.UpdateValidTo(token);
            }
            token.TokenIssuer = this.tokenIssuerName;

            var tokenData = token.Serialize();
            return this.EncryptData(tokenData);
        }

        public bool DecryptAuthenticationTokenBinary(byte[] authTokenEncrypted, int offset, int len, out AuthenticationToken authToken, out string errorMsg)
        {
            return this.DecryptAuthenticationTokenV2(authTokenEncrypted, offset, len, out authToken, out errorMsg);
        }

        #region Token Binary Format 2

        public string EncryptAuthenticationTokenV2(AuthenticationToken token, bool renew)
        {
            if (renew)
            {
                this.UpdateValidTo(token);
            }

            token.TokenIssuer = this.tokenIssuerName;

            var tokenData = token.Serialize();
            tokenData = this.EncryptData(tokenData);
            return Convert.ToBase64String(tokenData);
        }

        #endregion

        #region Methods

        protected virtual bool TryDeserializeToken(byte[] tokenData, out AuthenticationToken authToken, out string errorMsg)
        {
            return AuthenticationToken.TryDeserialize(tokenData, out authToken, out errorMsg);
        }

        private bool DecryptAuthenticationTokenV2(byte[] authTokenEncrypted, out AuthenticationToken authToken, out string errorMsg)
        {
            var tokenData = this.DecryptData(authTokenEncrypted, out errorMsg);
            if (tokenData == null)
            {
                authToken = null;
                return false;
            }
            return this.TryDeserializeToken(tokenData, out authToken, out errorMsg);
        }

        private bool DecryptAuthenticationTokenV2(byte[] authTokenEncrypted, int offset, int len, out AuthenticationToken authToken, out string errorMsg)
        {
            var tokenData = this.DecryptData(authTokenEncrypted, offset, len, out errorMsg);
            if (tokenData == null)
            {
                authToken = null;
                return false;
            }
            return this.TryDeserializeToken(tokenData, out authToken, out errorMsg);
        }

        private byte[] EncryptData(byte[] data)
        {
            var dataBuffer = new byte[data.Length + CryptoBase.IV_SIZE + CryptoBase.HMAC_SIZE
                + (CryptoBase.BLOCK_SIZE - data.Length % CryptoBase.BLOCK_SIZE) + 1];// plus one byte for version

            dataBuffer[0] = Version2;
            var offset = 1;
            this.encryptor.Encrypt(data, data.Length, dataBuffer, ref offset);

            var hmac = this.encryptor.FinishHMACThreadSafe(dataBuffer, 0, offset);

            Buffer.BlockCopy(hmac, 0, dataBuffer, offset, hmac.Length);
            return dataBuffer;
        }

        private byte[] DecryptData(byte[] data, out string errorMsg)
        {
            return this.DecryptData(data, 0, data.Length, out errorMsg);
        }

        private byte[] DecryptData(byte[] data, int offset, int len, out string errorMsg)
        {
            if (!this.decryptor.CheckHMACThreadSafe(data, offset, len))
            {
                errorMsg = "Incoming token data does not contain correct HMAC";
                return null;
            }
            errorMsg = string.Empty;
            // offset == 1. we skip version
            return this.decryptor.DecryptBufferWithIV(data, offset + 1, len - CryptoBase.HMAC_SIZE - 1);
        }

        protected void SetupToken(AuthenticationToken token)
        {
            token.TokenIssuer = this.tokenIssuerName;
            token.ExpireAtTicks = DateTime.UtcNow.Add(this.ExpirationTime).Ticks;
            token.SessionId = MakeSessionId();
        }

        private static string MakeSessionId()
        {
            return $"{Guid.NewGuid()}_{DateTime.UtcNow.ToString(CultureInfo.InvariantCulture)}";
        }

        private void UpdateValidTo(AuthenticationToken token)
        {
            var newValidToUtc = DateTime.UtcNow.Add(this.ExpirationTime);

            if (token.IsFinalExpireAtUsed)
            {
                var absoluteValidToUtc = token.FinalExpireAt;
                if (newValidToUtc > absoluteValidToUtc)
                {
                    newValidToUtc = absoluteValidToUtc;
                }
            }

            token.ExpireAtTicks = newValidToUtc.Ticks;
        }

        #endregion
    }
}
