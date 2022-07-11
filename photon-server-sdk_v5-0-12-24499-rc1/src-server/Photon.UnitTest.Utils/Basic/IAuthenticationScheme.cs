using System.Collections.Generic;
using ExitGames.Client.Photon;

namespace Photon.UnitTest.Utils.Basic
{
    public interface IAuthSchemeClient
    {
        object Token { get; set; }
        string UserId { get; }
    }

    public interface IAuthenticationScheme
    {
        void SetAuthenticateParameters(
            IAuthSchemeClient secretHolder,
            ParameterDictionary requestParameters, Dictionary<byte, object> authParameter = null);

        void HandleAuthenticateResponse(IAuthSchemeClient secretHolder, Dictionary<byte, object> response);
    }
}