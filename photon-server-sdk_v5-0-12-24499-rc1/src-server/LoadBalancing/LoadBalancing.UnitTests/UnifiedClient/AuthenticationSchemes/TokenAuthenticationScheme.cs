using ExitGames.Client.Photon;
using Photon.Realtime;
using Photon.UnitTest.Utils.Basic;

namespace Photon.LoadBalancing.UnifiedClient.AuthenticationSchemes
{
    using System.Collections.Generic;

    public class TokenAuthenticationScheme : TokenLessAuthenticationScheme
    {
        public override void SetAuthenticateParameters(IAuthSchemeClient client, ParameterDictionary requestParameters, Dictionary<byte, object> authParameter = null)
        {
            if (authParameter == null)
            {
                authParameter = new Dictionary<byte, object>();
            }

            if (client.Token != null)
            {
                var asStr = client.Token as string;
                if (!string.IsNullOrEmpty(asStr))
                {
                    // we already have a token (from nameserver / master) - just use that
                    if (!authParameter.ContainsKey(ParameterCode.Token))
                    {
                        authParameter[ParameterCode.Token] = client.Token;
                    }
                }
                else
                {
                    var asByteArray = client.Token as byte[];
                    if (asByteArray != null && asByteArray.Length > 0 && !authParameter.ContainsKey(ParameterCode.Token))
                    {
                        authParameter[ParameterCode.Token] = client.Token;
                    }
                }
            }
            else
            {
                // no token yet - pass UserId into authentication: 
                if (!authParameter.ContainsKey(ParameterCode.UserId))
                {
                    authParameter[ParameterCode.UserId] = client.UserId; 
                }

                if (!authParameter.ContainsKey(ParameterCode.ApplicationId))
                {
                    authParameter[ParameterCode.ApplicationId] = "AppId";
                    authParameter[ParameterCode.AppVersion] = "1.0";
                }
            }

            base.SetAuthenticateParameters(client, requestParameters, authParameter);
        }

        public override void HandleAuthenticateResponse(IAuthSchemeClient client, Dictionary<byte, object> response)
        {
            // Master: returns a secret - store and re-use it! 
            // GS: does not return a secret. 
            if (response.ContainsKey(ParameterCode.Token))
            {
                client.Token = (string)response[ParameterCode.Token];
            }
        }
    }
}
