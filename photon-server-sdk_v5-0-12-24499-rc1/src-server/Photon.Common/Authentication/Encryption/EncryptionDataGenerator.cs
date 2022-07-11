using System.Collections.Generic;
using System.Security.Cryptography;
using Photon.SocketServer.Security;

namespace Photon.Common.Authentication.Encryption
{
    public static class EncryptionDataGenerator
    {
        public static Dictionary<byte, object> Generate(byte mode)
        {
            var rnd = new RNGCryptoServiceProvider();

            var result = new Dictionary<byte, object>
            {
                {EncryptionDataParameters.EncryptionMode, mode},
            };

            switch ((EncryptionModes)mode)
            {
                case EncryptionModes.PayloadEncryption:
                case EncryptionModes.PayloadEncryptionWithIV:
                case EncryptionModes.DatagramEncryptionGCM:
                {
                    // encryption secret
                    var key = new byte[32];
                    rnd.GetBytes(key);
                    result.Add(EncryptionDataParameters.EncryptionSecret, key);
                }
                break;
                case EncryptionModes.PayloadEncryptionWithIVHMAC:
                case EncryptionModes.DatagramEncryption:
                case EncryptionModes.DatagramEncryptionWithRandomInitialNumbers:
                {
                    // encryption secret
                    var key = new byte[32];
                    rnd.GetBytes(key);
                    result.Add(EncryptionDataParameters.EncryptionSecret, key);

                    // hmac/auth secret
                    key = new byte[32];
                    rnd.GetBytes(key);
                    result.Add(EncryptionDataParameters.AuthSecret, key);
                }
                break;
            }
            return result;
        }
    }
}
