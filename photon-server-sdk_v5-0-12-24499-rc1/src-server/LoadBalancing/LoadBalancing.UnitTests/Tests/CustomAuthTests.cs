using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using ExitGames.Concurrency.Fibers;
using ExitGames.Logging;
using Newtonsoft.Json;
using NUnit.Framework;
using Photon.Common.Annotations;
using Photon.Common.Authentication;
using Photon.Common.Authentication.CustomAuthentication;
using Photon.Common.Authentication.Data;
using Photon.Common.Authentication.Diagnostic;
using Photon.LoadBalancing.Operations;
using Photon.Realtime;
using Photon.SocketServer;
using Photon.SocketServer.Net;
using ErrorCode = Photon.Common.ErrorCode;
using System.Linq;
using Photon.Common.Configuration;

#pragma warning disable 1570

namespace Photon.LoadBalancing.UnitTests.Tests
{

    [TestFixture]
    public class CustomAuthTests
    {
        private const string UserId = "UserId";
        private const string DashBoardParams = "p1=v1&p2=v2&p3=v3";
        private const string TestUrl = "http://test.url/";
        private const string ClientQueryString = "cp1=v1&cp2=v2";
        private const string ClientQueryStringWithDashboardIntersection = ClientQueryString + "&p1=v2";
        private const string PostDataString = "string";
        private readonly byte[] PostDataArray = new byte[] { 0, 1, 2, 3 };

        private const string PostDataTypeString = "string";
        private const string PostDataTypeArray = "array";
        private const string PostDataTypeDict = "dictionary";
        private const string PostDataTypeDictWithIntersect = "dictionary_intersect";
        private const string PostDataTypeOther = "other";
        private const string PostDataTypeNull = "Null";


        private readonly Dictionary<string, object> PostDataDictionary = new Dictionary<string, object>
        {
            {"dp1", "dv1"},
            {"dp2", "dv2"},
            {"dp3", "dv2"},
        };

        private readonly Dictionary<string, object> PostDataDictionaryWithDashBoardIngtersection = new Dictionary<string, object>
        {
            {"dp1", "dv1"},
            {"dp2", "dv2"},
            {"dp3", "dv2"},
            {"p1", "dv1"},
            {"p2", "dv2"},
            {"p3", "dv2"},
        };


        /// <summary>
        /// test covering cases 1) and 3) from https://confluence.exitgames.com/display/PTN/Custom+Authentication+Behaviour+Cheatsheet
        /// </summary>
        /// <param name="dashboardParams"></param>
        /// <param name="anonymous"></param>
        [Test]
        public void SuccessNoClientParamsNoPostData([Values(null, DashBoardParams)] string dashboardParams, [Values(false, true)] bool anonymous)
        {
            var handler = new TestCustomAuthHandler(anonymous);

            var queue = handler.AddQueue(TestUrl, dashboardParams);

            var authRequest = new AuthenticateRequest
            {
                UserId = UserId,
                ClientAuthenticationParams = null
            };

            var peer = new TestCustomAuthPeer();

            handler.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

            Assert.That(queue.ResutlPostData, Is.Null);
            Assert.That(queue.ResultContentType, Is.Null.Or.Empty);
            Assert.That(queue.ExecuteRequestCalled, Is.EqualTo(!anonymous));
            Assert.That(peer.OnCustomAuthenticationResultCalled, Is.EqualTo(anonymous));
            Assert.That(peer.OnCustomAuthenticationErrorCalled, Is.False);

            if (anonymous)
            {
                Assert.That(queue.ResutlClientQueryStringParameters, Is.Null.Or.Empty);
                Assert.That(peer.ResultCustomAuthResult.ResultCode, Is.EqualTo(CustomAuthenticationResultCode.Ok));
            }
            else
            {
                var testString = dashboardParams != null ? TestUrl + "?" + DashBoardParams : TestUrl;
                Assert.That(queue.ResutlClientQueryStringParameters, Is.EqualTo(testString));
            }
        }

        /// <summary>
        /// test covering cases 2) and 4) from https://confluence.exitgames.com/display/PTN/Custom+Authentication+Behaviour+Cheatsheet
        /// </summary>
        [Test]
        public void SuccessClientParamsNoPostData([Values(ClientQueryString, ClientQueryStringWithDashboardIntersection)] string clientQueryString,
            [Values(null, DashBoardParams)] string dashboardParams, [Values(false, true)] bool anonymous)
        {
            var handler = new TestCustomAuthHandler(anonymous);

            var queue = handler.AddQueue(TestUrl, dashboardParams);

            var authRequest = new AuthenticateRequest
            {
                UserId = UserId,
                ClientAuthenticationParams = clientQueryString
            };

            var peer = new TestCustomAuthPeer();

            handler.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

            Assert.That(queue.ResutlPostData, Is.Null);
            Assert.That(queue.ResultContentType, Is.Null.Or.Empty);
            Assert.That(queue.ExecuteRequestCalled, Is.True);
            Assert.That(peer.OnCustomAuthenticationResultCalled, Is.False);
            Assert.That(peer.OnCustomAuthenticationErrorCalled, Is.False);

            string testString;
            if (dashboardParams != null)
            {
                testString = TestUrl + "?" + dashboardParams + "&" + ClientQueryString;
            }
            else
            {
                testString = TestUrl + "?" + clientQueryString;
            }
            Assert.That(queue.ResutlClientQueryStringParameters, Is.EqualTo(testString));
        }

        /// <summary>
        /// test covering cases 5) and 7) from https://confluence.exitgames.com/display/PTN/Custom+Authentication+Behaviour+Cheatsheet
        /// </summary>
        [Test]
        public void SuccessNoClientParamsPostData([Values(PostDataTypeString, PostDataTypeArray)] object postData,
            [Values(null, DashBoardParams)] string dashboardParams, [Values(false, true)] bool anonymous)
        {
            postData = GetPostData((string)postData);

            var handler = new TestCustomAuthHandler(anonymous);

            var queue = handler.AddQueue(TestUrl, dashboardParams);

            var authRequest = new AuthenticateRequest
            {
                UserId = UserId,
                ClientAuthenticationParams = null,
                ClientAuthenticationData = postData,
            };

            var peer = new TestCustomAuthPeer();

            handler.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

            Assert.That(queue.ResutlPostData, Is.Not.Null);
            Assert.That(queue.ResultContentType, Is.Null.Or.Empty);
            Assert.That(queue.ExecuteRequestCalled, Is.True);
            Assert.That(peer.OnCustomAuthenticationResultCalled, Is.False);
            Assert.That(peer.OnCustomAuthenticationErrorCalled, Is.False);

            var testString = dashboardParams != null ? TestUrl + "?" + DashBoardParams : TestUrl;
            Assert.That(queue.ResutlClientQueryStringParameters, Is.EqualTo(testString));
        }

        /// <summary>
        /// test covering cases 6) and 8) from https://confluence.exitgames.com/display/PTN/Custom+Authentication+Behaviour+Cheatsheet
        /// </summary>
        [Test]
        public void SuccessClientParamsPostData([Values(ClientQueryString, ClientQueryStringWithDashboardIntersection)] string clientQueryString,
            [Values(PostDataTypeString, PostDataTypeArray)] object postData,
            [Values(null, DashBoardParams)] string dashboardParams, [Values(false, true)] bool anonymous)
        {
            postData = GetPostData((string)postData);

            var handler = new TestCustomAuthHandler(anonymous);

            var queue = handler.AddQueue(TestUrl, dashboardParams);

            var authRequest = new AuthenticateRequest
            {
                UserId = UserId,
                ClientAuthenticationParams = clientQueryString,
                ClientAuthenticationData = postData,
            };

            var peer = new TestCustomAuthPeer();

            handler.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

            Assert.That(queue.ResutlPostData, Is.Not.Null);
            Assert.That(queue.ResultContentType, Is.Null.Or.Empty);
            Assert.That(queue.ExecuteRequestCalled, Is.True);
            Assert.That(peer.OnCustomAuthenticationResultCalled, Is.False);
            Assert.That(peer.OnCustomAuthenticationErrorCalled, Is.False);

            string testString;
            if (dashboardParams != null)
            {
                testString = TestUrl + "?" + dashboardParams + "&" + ClientQueryString;
            }
            else
            {
                testString = TestUrl + "?" + clientQueryString;
            }
            Assert.That(queue.ResutlClientQueryStringParameters, Is.EqualTo(testString));

        }


        /// <summary>
        /// test covering cases 9) and 11) from https://confluence.exitgames.com/display/PTN/Custom+Authentication+Behaviour+Cheatsheet
        /// </summary>
        [Test]
        public void SuccessNoClientParamsPostDataDictionary([Values(PostDataTypeDict, PostDataTypeDictWithIntersect)] string postData,
            [Values(null, DashBoardParams)] string dashboardParams, [Values(false, true)] bool anonymous)
        {
            var handler = new TestCustomAuthHandler(anonymous);

            var queue = handler.AddQueue(TestUrl, dashboardParams);

            var authRequest = new AuthenticateRequest
            {
                UserId = UserId,
                ClientAuthenticationParams = null,
                ClientAuthenticationData = GetPostData(postData),
            };

            var peer = new TestCustomAuthPeer();

            handler.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

            Assert.That(queue.ResutlPostData, Is.Not.Null);
            Assert.That(queue.ResultContentType, Is.EqualTo("application/json"));
            Assert.That(queue.ExecuteRequestCalled, Is.True);
            Assert.That(peer.OnCustomAuthenticationResultCalled, Is.False);
            Assert.That(peer.OnCustomAuthenticationErrorCalled, Is.False);

            var testString = dashboardParams != null ? TestUrl + "?" + DashBoardParams : TestUrl;
            Assert.That(queue.ResutlClientQueryStringParameters, Is.EqualTo(testString));

            var json = Encoding.UTF8.GetString(queue.ResutlPostData);
            string jsonTestString;
            if (dashboardParams != null)
            {
                jsonTestString = Newtonsoft.Json.JsonConvert.SerializeObject(GetPostData(PostDataTypeDict));// duplicates will be removed
            }
            else
            {
                jsonTestString = Newtonsoft.Json.JsonConvert.SerializeObject(GetPostData(postData));
            }
            Assert.That(json, Is.EqualTo(jsonTestString));
        }


        /// <summary>
        /// test covering cases 10) and 12) from https://confluence.exitgames.com/display/PTN/Custom+Authentication+Behaviour+Cheatsheet
        /// </summary>
        [Test]
        public void SuccessClientParamsPostDataDictionary(
            [Values(ClientQueryString, ClientQueryStringWithDashboardIntersection)] string clientQueryString,
            [Values(PostDataTypeDict, PostDataTypeDictWithIntersect)] string postData,
            [Values(null, DashBoardParams)] string dashboardParams, [Values(false, true)] bool anonymous)
        {
            var handler = new TestCustomAuthHandler(anonymous);

            var queue = handler.AddQueue(TestUrl, dashboardParams);

            var authRequest = new AuthenticateRequest
            {
                UserId = UserId,
                ClientAuthenticationParams = clientQueryString,
                ClientAuthenticationData = GetPostData(postData),
            };

            var peer = new TestCustomAuthPeer();

            handler.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

            Assert.That(queue.ExecuteRequestCalled, Is.True);
            Assert.That(queue.ResutlPostData, Is.Not.Null);
            Assert.That(queue.ResultContentType, Is.EqualTo("application/json"));
            Assert.That(peer.OnCustomAuthenticationResultCalled, Is.False);
            Assert.That(peer.OnCustomAuthenticationErrorCalled, Is.False);

            string testString;
            if (dashboardParams != null)// all duplicates will be removed from ClientQueryString
            {
                testString = TestUrl + "?" + dashboardParams + "&" + ClientQueryString;
            }
            else if (postData == PostDataTypeDictWithIntersect)// all duplicates will be removed from ClientQueryString
            {
                testString = TestUrl + "?" + ClientQueryString;
            }
            else
            {
                testString = TestUrl + "?" + clientQueryString;
            }
            Assert.That(queue.ResutlClientQueryStringParameters, Is.EqualTo(testString));

            var json = Encoding.UTF8.GetString(queue.ResutlPostData);
            string jsonTestString;
            if (dashboardParams != null)
            {
                jsonTestString = Newtonsoft.Json.JsonConvert.SerializeObject(GetPostData(PostDataTypeDict));// duplicates will be removed
            }
            else
            {
                jsonTestString = Newtonsoft.Json.JsonConvert.SerializeObject(GetPostData(postData));
            }
            Assert.That(json, Is.EqualTo(jsonTestString));

        }

        /// <summary>
        /// test covering cases 13) and 15) from https://confluence.exitgames.com/display/PTN/Custom+Authentication+Behaviour+Cheatsheet
        /// </summary>
        /// <param name="dashboardParams"></param>
        /// <param name="anonymous"></param>
        [Test]
        public void JSON_SuccessNoClientParamsNoPostData([Values(null, DashBoardParams)] string dashboardParams, [Values(false, true)] bool anonymous)
        {
            var handler = new TestCustomAuthHandler(anonymous);

            var queue = handler.AddQueue(TestUrl, dashboardParams, true);

            var authRequest = new AuthenticateRequest
            {
                UserId = UserId,
                ClientAuthenticationParams = null
            };

            var peer = new TestCustomAuthPeer();

            handler.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

            Assert.That(queue.ExecuteRequestCalled, Is.EqualTo(!anonymous));
            Assert.That(peer.OnCustomAuthenticationResultCalled, Is.EqualTo(anonymous));
            Assert.That(peer.OnCustomAuthenticationErrorCalled, Is.False);

            if (anonymous)
            {
                Assert.That(queue.ResultContentType, Is.Null.Or.Empty);
                Assert.That(queue.ResutlPostData, Is.Null);
                Assert.That(queue.ResutlClientQueryStringParameters, Is.Null.Or.Empty);
                Assert.That(peer.ResultCustomAuthResult.ResultCode, Is.EqualTo(CustomAuthenticationResultCode.Ok));
            }
            else
            {
                Assert.That(queue.ResutlPostData, Is.Not.Null);
                Assert.That(queue.ResultContentType, Is.EqualTo("application/json"));

                Assert.That(queue.ResutlClientQueryStringParameters, Is.EqualTo(TestUrl));
                Assert.That(Encoding.UTF8.GetString(queue.ResutlPostData), Is.EqualTo(GetJSONFromQueryStrings(dashboardParams, null, null)));
            }
        }


        /// <summary>
        /// test covering cases 14) and 16) from https://confluence.exitgames.com/display/PTN/Custom+Authentication+Behaviour+Cheatsheet
        /// </summary>
        [Test]
        public void JSON_SuccessClientParamsNoPostData([Values(ClientQueryString, ClientQueryStringWithDashboardIntersection)] string clientQueryString,
            [Values(null, DashBoardParams)] string dashboardParams, [Values(false, true)] bool anonymous)
        {
            var handler = new TestCustomAuthHandler(anonymous);

            var queue = handler.AddQueue(TestUrl, dashboardParams, true);

            var authRequest = new AuthenticateRequest
            {
                UserId = UserId,
                ClientAuthenticationParams = clientQueryString
            };

            var peer = new TestCustomAuthPeer();

            handler.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

            Assert.That(queue.ResutlPostData, Is.Not.Null);
            Assert.That(queue.ResultContentType, Is.EqualTo("application/json"));
            Assert.That(queue.ExecuteRequestCalled, Is.True);
            Assert.That(peer.OnCustomAuthenticationResultCalled, Is.False);
            Assert.That(peer.OnCustomAuthenticationErrorCalled, Is.False);

            Assert.That(queue.ResutlClientQueryStringParameters, Is.EqualTo(TestUrl));

            string testString;
            if (dashboardParams != null)
            {
                testString = GetJSONFromQueryStrings(dashboardParams, ClientQueryString, GetPostData(PostDataTypeNull));
            }
            else
            {
                testString = GetJSONFromQueryStrings(null, clientQueryString, GetPostData(PostDataTypeNull));
            }

            Assert.That(Encoding.UTF8.GetString(queue.ResutlPostData), Is.EqualTo(testString));
        }

  
        /// <summary>
        /// test covering cases 21) and 24) from https://confluence.exitgames.com/display/PTN/Custom+Authentication+Behaviour+Cheatsheet
        /// </summary>
        [Test]
        public void JSON_SuccessClientParamsPostDataDictionary(
            [Values(null, ClientQueryString, ClientQueryStringWithDashboardIntersection)] string clientQueryString,
            [Values(PostDataTypeDict, PostDataTypeDictWithIntersect)] string postData,// no null here. we have such test already
            [Values(null, DashBoardParams)] string dashboardParams, [Values(false, true)] bool anonymous)
        {
            var handler = new TestCustomAuthHandler(anonymous);

            var queue = handler.AddQueue(TestUrl, dashboardParams, true);

            var authRequest = new AuthenticateRequest
            {
                UserId = UserId,
                ClientAuthenticationParams = clientQueryString,
                ClientAuthenticationData = GetPostData(postData),
            };

            var peer = new TestCustomAuthPeer();

            handler.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

            Assert.That(queue.ResutlPostData, Is.Not.Null);
            Assert.That(queue.ResultContentType, Is.EqualTo("application/json"));
            Assert.That(queue.ExecuteRequestCalled, Is.True);
            Assert.That(peer.OnCustomAuthenticationResultCalled, Is.False);
            Assert.That(peer.OnCustomAuthenticationErrorCalled, Is.False);

            Assert.That(queue.ResutlClientQueryStringParameters, Is.EqualTo(TestUrl));

            var testString = GetJSONFromQueryStrings(dashboardParams, clientQueryString, GetPostData(postData));

            Assert.That(Encoding.UTF8.GetString(queue.ResutlPostData), Is.EqualTo(testString));
        }

        /// <summary>
        /// test covering cases 17)-20), 25) from https://confluence.exitgames.com/display/PTN/Custom+Authentication+Behaviour+Cheatsheet
        /// </summary>
        [Test]
        public void JSON_Fail_PostDataOfUnsupportedType(
            [Values(null, ClientQueryString, ClientQueryStringWithDashboardIntersection)] string clientQueryString,
            [Values(PostDataTypeString, PostDataTypeArray)] object postData,
            [Values(null, DashBoardParams)] string dashboardParams, 
            [Values(false, true)] bool anonymous)
        {
            postData = GetPostData((string)postData);

            var handler = new TestCustomAuthHandler(anonymous);
            var queue = handler.AddQueue(TestUrl, dashboardParams, true);

            var authRequest = new AuthenticateRequest
            {
                UserId = UserId,
                ClientAuthenticationParams = clientQueryString,
                ClientAuthenticationData = postData,
            };

            var peer = new TestCustomAuthPeer();

            handler.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

            Assert.That(queue.ResutlPostData, Is.Null);
            Assert.That(queue.ResultContentType, Is.Null.Or.Empty);
            Assert.That(queue.ExecuteRequestCalled, Is.False);
            Assert.That(peer.OnCustomAuthenticationResultCalled, Is.False);
            Assert.That(peer.OnCustomAuthenticationErrorCalled, Is.True);
            Assert.That(handler.ErrorsCount, Is.EqualTo(1));
            Assert.That(queue.ResutlClientQueryStringParameters, Is.Null);
        }

        /// <summary>
        /// case 25) from https://confluence.exitgames.com/display/PTN/Custom+Authentication+Behaviour+Cheatsheet
        /// </summary>
        /// <param name="clientQueryString"></param>
        /// <param name="dashboardParams"></param>
        /// <param name="anonymous"></param>
        /// <param name="validProvider"></param>
        [Test]
        public void FailAllCombinationsWithWrongPostDataType(
            [Values(ClientQueryString, ClientQueryStringWithDashboardIntersection)] string clientQueryString,
            [Values(null, DashBoardParams)] string dashboardParams, 
            [Values(false, true)] bool anonymous,
            [Values(false, true)] bool validProvider
            )
        {
            var handler = new TestCustomAuthHandler(anonymous);

            var queue = handler.AddQueue(TestUrl, dashboardParams);

            var authRequest = new AuthenticateRequest
            {
                UserId = UserId,
                ClientAuthenticationParams = clientQueryString,
                ClientAuthenticationData = GetPostData(PostDataTypeOther),
                ClientAuthenticationType = validProvider ? (byte)0 :  (byte)ClientAuthenticationType.Facebook,
            };

            var peer = new TestCustomAuthPeer();

            handler.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

            Assert.That(queue.ExecuteRequestCalled, Is.False);
            Assert.That(queue.ResutlPostData, Is.Null);
            Assert.That(queue.ResultContentType, Is.Null);
            Assert.That(peer.OnCustomAuthenticationResultCalled, Is.False);
            Assert.That(peer.OnCustomAuthenticationErrorCalled, Is.True);
            Assert.That(peer.ResultErrorCode, Is.EqualTo(ErrorCode.CustomAuthenticationFailed));
        }

        /// <summary>
        /// we check that if account has multiple auth providers then they also work fine
        /// </summary>
        /// <param name="clientQueryString"></param>
        /// <param name="postData"></param>
        /// <param name="dashboardParams"></param>
        /// <param name="anonymous"></param>
        [Test]
        public void Success_MultiAuthTypesTest(
            [Values(null, ClientQueryString, ClientQueryStringWithDashboardIntersection)] string clientQueryString,
            [Values(PostDataTypeString, PostDataTypeArray, PostDataTypeDict, PostDataTypeDictWithIntersect)] object postData,
            [Values(null, DashBoardParams)] string dashboardParams,
            [Values(false, true)] bool anonymous)
        {
            postData = GetPostData((string)postData);

            var handler = new TestCustomAuthHandler(anonymous);

            var queue = handler.AddQueue(TestUrl, dashboardParams);
            var psQueue = handler.AddQueue(TestUrl, ClientQueryString, authType: ClientAuthenticationType.PlayStation);

            var authRequest = new AuthenticateRequest
            {
                UserId = UserId,
                ClientAuthenticationParams = clientQueryString,
                ClientAuthenticationData = postData,
            };

            var peer = new TestCustomAuthPeer();

            handler.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

            Assert.That(queue.ExecuteRequestCallsCount, Is.EqualTo(1));
            Assert.That(psQueue.ExecuteRequestCallsCount, Is.EqualTo(0));

            authRequest = new AuthenticateRequest
            {
                UserId = UserId,
                ClientAuthenticationParams = clientQueryString,
                ClientAuthenticationData = postData,
                ClientAuthenticationType = (byte)ClientAuthenticationType.PlayStation,
            };

            peer = new TestCustomAuthPeer();

            handler.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

            Assert.That(queue.ExecuteRequestCallsCount, Is.EqualTo(1));
            Assert.That(psQueue.ExecuteRequestCallsCount, Is.EqualTo(1));
        }

        /// <summary>
        /// we check that if account has multiple auth providers with json enabled then they also work fine
        /// </summary>
        /// <param name="clientQueryString"></param>
        /// <param name="postData"></param>
        /// <param name="dashboardParams"></param>
        /// <param name="anonymous"></param>
        [Test]
        public void JSON_Success_MultiAuthTypesTest(
            [Values(null, ClientQueryString, ClientQueryStringWithDashboardIntersection)] string clientQueryString,
            [Values(PostDataTypeDict, PostDataTypeDictWithIntersect)] object postData,
            [Values(null, DashBoardParams)] string dashboardParams,
            [Values(false, true)] bool anonymous)
        {
            postData = GetPostData((string)postData);

            var handler = new TestCustomAuthHandler(anonymous);

            var queue = handler.AddQueue(TestUrl, dashboardParams, forwardAsJSON: true);
            var psQueue = handler.AddQueue(TestUrl, ClientQueryString, forwardAsJSON: true, authType: ClientAuthenticationType.PlayStation);

            var authRequest = new AuthenticateRequest
            {
                UserId = UserId,
                ClientAuthenticationParams = clientQueryString,
                ClientAuthenticationData = postData,
            };

            var peer = new TestCustomAuthPeer();

            handler.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

            Assert.That(queue.ExecuteRequestCallsCount, Is.EqualTo(1));
            Assert.That(psQueue.ExecuteRequestCallsCount, Is.EqualTo(0));

            authRequest = new AuthenticateRequest
            {
                UserId = UserId,
                ClientAuthenticationParams = clientQueryString,
                ClientAuthenticationData = postData,
                ClientAuthenticationType = (byte)ClientAuthenticationType.PlayStation,
            };

            peer = new TestCustomAuthPeer();

            handler.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

            Assert.That(queue.ExecuteRequestCallsCount, Is.EqualTo(1));
            Assert.That(psQueue.ExecuteRequestCallsCount, Is.EqualTo(1));
        }

        [Test]
        public void Fail_SuccessForCustomProvider_FailForFacebookTest(
            [Values(null, ClientQueryString, ClientQueryStringWithDashboardIntersection)] string clientQueryString,
            [Values(PostDataTypeString, PostDataTypeArray)] object postData,
            [Values(null, DashBoardParams)] string dashboardParams,
            [Values(false, true)] bool anonymous)
        {
            postData = GetPostData((string)postData);

            var handler = new TestCustomAuthHandler(anonymous);

            var queue = handler.AddQueue(TestUrl, dashboardParams, forwardAsJSON: false);
            var facebookQueue = handler.AddQueue(TestUrl, ClientQueryString, forwardAsJSON: true, authType: ClientAuthenticationType.Facebook);

            var authRequest = new AuthenticateRequest
            {
                UserId = UserId,
                ClientAuthenticationParams = clientQueryString,
                ClientAuthenticationData = postData,
            };

            var peer = new TestCustomAuthPeer();

            handler.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

            Assert.That(queue.ExecuteRequestCallsCount, Is.EqualTo(1));
            Assert.That(facebookQueue.ExecuteRequestCallsCount, Is.EqualTo(0));

            authRequest = new AuthenticateRequest
            {
                UserId = UserId,
                ClientAuthenticationParams = clientQueryString,
                ClientAuthenticationData = postData,
                ClientAuthenticationType = (byte)ClientAuthenticationType.Facebook,
            };

            peer = new TestCustomAuthPeer();

            handler.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

            Assert.That(queue.ExecuteRequestCallsCount, Is.EqualTo(1));
            Assert.That(facebookQueue.ExecuteRequestCallsCount, Is.EqualTo(0));
            Assert.That(peer.OnCustomAuthenticationErrorCalled, Is.True);
            Assert.That(handler.ErrorsCount, Is.EqualTo(1));
        }

        /// <summary>
        /// tests are covering case 26
        /// if we do not send client params and client post data other settings does not matter
        /// from https://confluence.exitgames.com/display/PTN/Custom+Authentication+Behaviour+Cheatsheet
        /// </summary>
        [Test]
        public void SuccessfulAnonym_Case_26(
            [Values(null, DashBoardParams)] string dashboardParams,
            [Values(false, true)] bool json)
        {
            var handler = new TestCustomAuthHandler(true);

            var queue = handler.AddQueue(TestUrl, dashboardParams, json);

            var authRequest = new AuthenticateRequest
            {
                UserId = UserId,
                ClientAuthenticationType = (byte)ClientAuthenticationType.Facebook,
            };

            var peer = new TestCustomAuthPeer();

            handler.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

            Assert.That(queue.ExecuteRequestCalled, Is.False);
            Assert.That(queue.ResutlPostData, Is.Null);
            Assert.That(queue.ResultContentType, Is.Null);
            Assert.That(peer.OnCustomAuthenticationResultCalled, Is.True);
            Assert.That(peer.ResultErrorCode, Is.EqualTo(ErrorCode.Ok));
        }

        /// <summary>
        /// tests are covering case 26 if anonym == true. 30 - anonym == false 
        /// from https://confluence.exitgames.com/display/PTN/Custom+Authentication+Behaviour+Cheatsheet
        /// just one more test
        /// </summary>
        /// <param name="anonymous"></param>
        [Test]
        public void NonExistingAuthTypeNoQueryNoData([Values(false, true)] bool anonymous)
        {
            var handler = new TestCustomAuthHandler(anonymous);

            var facebookQueue = handler.AddQueue(TestUrl, DashBoardParams, true, ClientAuthenticationType.Facebook);

            var authRequest = new AuthenticateRequest
            {
                UserId = UserId,
            };

            var peer = new TestCustomAuthPeer();

            handler.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

            Assert.That(facebookQueue.ExecuteRequestCallsCount, Is.EqualTo(0));
            if (anonymous)
            {
                Assert.That(peer.OnCustomAuthenticationResultCalled, Is.True);
                Assert.That(peer.ResultErrorCode, Is.EqualTo(ErrorCode.Ok));
            }
            else
            {
                Assert.That(peer.OnCustomAuthenticationErrorCalled, Is.True);
                Assert.That(peer.ResultErrorCode, Is.EqualTo(ErrorCode.CustomAuthenticationFailed));
            }
        }

        /// <summary>
        /// tests are covering 27-30 https://confluence.exitgames.com/display/PTN/Custom+Authentication+Behaviour+Cheatsheet
        /// </summary>
        /// <param name="authType"></param>
        /// <param name="clientQueryString"></param>
        /// <param name="postData"></param>
        [Test]
        public void WrongProvidersOrWronUrlTest(
            [Values(CustomAuthenticationType.Custom, CustomAuthenticationType.Facebook, CustomAuthenticationType.PlayStation)]
            CustomAuthenticationType authType,
            [Values(null, ClientQueryString)] string clientQueryString,
            [Values(PostDataTypeNull, PostDataString, PostDataTypeArray, PostDataTypeDict, PostDataTypeOther)] string postData,
            [Values(false, true)] bool wrongUrl,
            [Values(false, true)] bool json
            )
        {
            var handler = new TestCustomAuthHandler(true);
            if (wrongUrl)
            {
                handler.AddQueue("wrong.url", DashBoardParams, json, (ClientAuthenticationType) authType);
            }

            var authRequest = new AuthenticateRequest
            {
                UserId = UserId,
                ClientAuthenticationType = (byte)authType,
                ClientAuthenticationParams = clientQueryString,
                ClientAuthenticationData = GetPostData(postData)
            };

            var peer = new TestCustomAuthPeer();
            handler.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

            if (postData == PostDataTypeNull && clientQueryString == null)
            {
                //anonymous was ignored, test fails (wrongUrl = true required because only then handler is added, which is checked in CustomAuthHandler.OnAuthenticateClient)
                if (authType == CustomAuthenticationType.PlayStation && wrongUrl)
                {
                    Assert.That(peer.OnCustomAuthenticationErrorCalled, Is.True);
                    Assert.That(peer.ResultErrorCode, Is.EqualTo(ErrorCode.CustomAuthenticationFailed));
                }
                else
                {
                    Assert.That(peer.OnCustomAuthenticationResultCalled, Is.True);
                    Assert.That(peer.ResultErrorCode, Is.EqualTo(ErrorCode.Ok));
                }
            }
            else
            {
                Assert.That(peer.OnCustomAuthenticationErrorCalled, Is.True);
                Assert.That(peer.ResultErrorCode, Is.EqualTo(ErrorCode.CustomAuthenticationFailed));
            }
        }

        /// <summary>
        /// we test here how production code handles wrong url.
        /// in some other tests special queue for tests was used
        /// </summary>
        [Test]
        public void WrongUrlForRealQueue()
        {
            var handler = new CustomAuthHandler(null, new HttpQueueSettings{LimitHttpResponseMaxSize = 100000});

            handler.AddNewAuthProvider("wrong.url", "xx=uyy", true, ClientAuthenticationType.Custom, false, "test");

            var authRequest = new AuthenticateRequest
            {
                UserId = UserId,
                ClientAuthenticationType = (byte)ClientAuthenticationType.Custom,
                ClientAuthenticationParams = "",
                ClientAuthenticationData = new byte[1]
            };

            var peer = new TestCustomAuthPeer();
            handler.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

            Thread.Sleep(500);
            Assert.That(peer.OnCustomAuthenticationErrorCalled, Is.True);
        }

        [Test]
        public void NewtonsoftJson_6_to_11plus_regressionTest(
            [Values(ClientQueryString)] string clientQueryString,
            [Values(PostDataTypeNull)] string postData,
            [Values(false)] bool wrongUrl,
            [Values(true)] bool json
            )
        {
            var jsonResponse = "{\"Message\":\"Success\",\"ResultCode\":1.0,\"UserId\":\"5c891ecec1524a05168e9491\",\"Nickname\":\"flibble\"}";


            var handler = new TestCustomAuthHandler(true);
            handler.AddQueue("doesnt.matter", DashBoardParams, json, (ClientAuthenticationType)CustomAuthenticationType.Custom, jsonResponse);

            var authRequest = new AuthenticateRequest
            {
                UserId = UserId,
                ClientAuthenticationType = (byte)CustomAuthenticationType.Custom,
                ClientAuthenticationParams = clientQueryString,
                ClientAuthenticationData = GetPostData(postData)
            };

            var peer = new TestCustomAuthPeer();
            handler.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);
            Assert.That(peer.OnCustomAuthenticationResultCalled, Is.True);
            Assert.That(peer.ResultErrorCode, Is.EqualTo(ErrorCode.Ok));
        }

        [Test]
        public void FacebookTests(
            [Values(null, "foo=bar", "token=7890")] string clientQueryString,
            [Values(null, "foo=bar", "appid=123&secret=456")] string dashboardParams,
            [Values(false, true)] bool anonymous)
        {
            var handler = new TestCustomAuthHandler(anonymous);

            var queue = handler.AddQueue(TestUrl, dashboardParams, authType: ClientAuthenticationType.Facebook);

            var authRequest = new AuthenticateRequest
            {
                UserId = UserId,
                ClientAuthenticationParams = clientQueryString,
                ClientAuthenticationType = (byte)ClientAuthenticationType.Facebook,
            };

            var peer = new TestCustomAuthPeer();

            handler.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

            //anonymous
            if (anonymous && clientQueryString == null)
            {
                Assert.That(peer.OnCustomAuthenticationResultCalled, Is.True);
                Assert.That(handler.ErrorsCount, Is.EqualTo(0));
            }
            //required parameters supplied (values don't matter for test)
            else if (clientQueryString != null &&
                     dashboardParams != null &&
                     clientQueryString.Contains("token=") &&
                     dashboardParams.Contains("appid=") &&
                     dashboardParams.Contains("secret="))
            {
                Assert.That(queue.ExecuteRequestCallsCount, Is.EqualTo(1));
            }
            else
            {
                Assert.That(queue.ExecuteRequestCallsCount, Is.EqualTo(0));
                Assert.That(peer.OnCustomAuthenticationErrorCalled, Is.True);
                Assert.That(handler.ErrorsCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void SteamTests(
            [Values(null, "foo=bar", "ticket=7890")] string clientQueryString,
            [Values(null, "foo=bar", "appid=123&apiKeySecret=456")] string dashboardParams,
            [Values(false, true)] bool anonymous)
        {
            var handler = new TestCustomAuthHandler(anonymous);

            var queue = handler.AddQueue(TestUrl, dashboardParams, authType: ClientAuthenticationType.Steam);

            var authRequest = new AuthenticateRequest
            {
                UserId = UserId,
                ClientAuthenticationParams = clientQueryString,
                ClientAuthenticationType = (byte)ClientAuthenticationType.Steam,
            };

            var peer = new TestCustomAuthPeer();

            handler.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

            //required parameters supplied (values don't matter for test)
            if (clientQueryString != null &&
                     dashboardParams != null &&
                     clientQueryString.Contains("ticket=") &&
                     dashboardParams.Contains("appid=") &&
                     dashboardParams.Contains("apiKeySecret="))
            {
                Assert.That(queue.ExecuteRequestCallsCount, Is.EqualTo(1));
            }
            else
            {
                Assert.That(queue.ExecuteRequestCallsCount, Is.EqualTo(0));
                Assert.That(peer.OnCustomAuthenticationErrorCalled, Is.True);
                Assert.That(handler.ErrorsCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void OculusTests(
            [Values(null, "foo=bar", "userid=7890&nonce=123456")] string clientQueryString,
            [Values(null, "foo=bar", "appid=123&appsecret=456")] string dashboardParams,
            [Values(false, true)] bool anonymous)
        {
            var handler = new TestCustomAuthHandler(anonymous);

            var queue = handler.AddQueue(TestUrl, dashboardParams, authType: ClientAuthenticationType.Oculus);

            var authRequest = new AuthenticateRequest
            {
                UserId = UserId,
                ClientAuthenticationParams = clientQueryString,
                ClientAuthenticationType = (byte)ClientAuthenticationType.Oculus,
            };

            var peer = new TestCustomAuthPeer();

            handler.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

            //required parameters supplied (values don't matter for test)
            if (clientQueryString != null &&
                     dashboardParams != null &&
                     clientQueryString.Contains("userid=") &&
                     clientQueryString.Contains("nonce=") &&
                     dashboardParams.Contains("appid=") &&
                     dashboardParams.Contains("appsecret="))
            {
                Assert.That(queue.ExecuteRequestCallsCount, Is.EqualTo(1));
            }
            else
            {
                Assert.That(queue.ExecuteRequestCallsCount, Is.EqualTo(0));
                Assert.That(peer.OnCustomAuthenticationErrorCalled, Is.True);
                Assert.That(handler.ErrorsCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void ViveportTests(
            [Values(null, "foo=bar", "usertoken=7890")] string clientQueryString,
            [Values(null, "foo=bar", "appid=123&appsecret=456")] string dashboardParams,
            [Values(false, true)] bool anonymous)
        {
            var handler = new TestCustomAuthHandler(anonymous);

            var queue = handler.AddQueue(TestUrl, dashboardParams, authType: ClientAuthenticationType.Viveport);

            var authRequest = new AuthenticateRequest
            {
                UserId = UserId,
                ClientAuthenticationParams = clientQueryString,
                ClientAuthenticationType = (byte)ClientAuthenticationType.Viveport,
            };

            var peer = new TestCustomAuthPeer();

            handler.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

            //required parameters supplied (values don't matter for test)
            if (clientQueryString != null &&
                     dashboardParams != null &&
                     clientQueryString.Contains("usertoken=") &&
                     dashboardParams.Contains("appid=") &&
                     dashboardParams.Contains("appsecret="))
            {
                Assert.That(queue.ExecuteRequestCallsCount, Is.EqualTo(1));
            }
            else
            {
                Assert.That(queue.ExecuteRequestCallsCount, Is.EqualTo(0));
                Assert.That(peer.OnCustomAuthenticationErrorCalled, Is.True);
                Assert.That(handler.ErrorsCount, Is.EqualTo(1));
            }
        }

        [TestCase(HttpStatusCode.InternalServerError)]
        [TestCase(HttpStatusCode.NotFound)]
        //here we test errors that should NOT be treated as service unavailable
        public void CustomAuthHttpResponseReject(HttpStatusCode statusCode)
        {
            var uri = $"http://localhost:{55555}";
            var httpListener = new HttpTestListener(new Uri(uri));
            try
            {
                httpListener.Start();

                var customAuthHandlerReject = new CustomAuthHandler(null, new HttpQueueSettings { LimitHttpResponseMaxSize = 100 });

                customAuthHandlerReject.AddNewAuthProvider(uri, "", true, ClientAuthenticationType.Custom, false, "test");
                var peer = new TestCustomAuthPeer();

                var authRequest = new AuthenticateRequest
                {
                    UserId = UserId,
                    ClientAuthenticationParams = httpListener.GetStatusRequest(statusCode),
                    ClientAuthenticationType = (byte)ClientAuthenticationType.Custom,
                };

                customAuthHandlerReject.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

                Assert.That(peer.Signal.Wait(1000000), Is.True);
                Assert.That(peer.OnCustomAuthenticationResultCalled, Is.True);
                Assert.That(peer.ResultCustomAuthResult.ResultCode, Is.EqualTo(CustomAuthenticationResultCode.Failed));

                var customAuthHandlerNonReject = new CustomAuthHandler(null, new HttpQueueSettings { LimitHttpResponseMaxSize = 100 });

                customAuthHandlerNonReject.AddNewAuthProvider(uri, "", false, ClientAuthenticationType.Custom, false, "test");
                peer = new TestCustomAuthPeer();

                authRequest = new AuthenticateRequest
                {
                    UserId = UserId,
                    ClientAuthenticationParams = httpListener.GetStatusRequest(statusCode),
                    ClientAuthenticationType = (byte)ClientAuthenticationType.Custom,
                };

                customAuthHandlerNonReject.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

                Assert.That(peer.Signal.Wait(1000), Is.True);
                Assert.That(peer.OnCustomAuthenticationResultCalled, Is.True);
                Assert.That(peer.ResultCustomAuthResult.ResultCode, Is.EqualTo(CustomAuthenticationResultCode.Failed));
            }
            finally
            {
                httpListener.Dispose();
            }
        }

        [TestCase(HttpStatusCode.ServiceUnavailable)]
        //here we test errors that means that service is unavailable
        public void CustomAuthHttpResponseAllow(HttpStatusCode statusCode)
        {
            var uri = $"http://localhost:{55555}";
            var httpListener = new HttpTestListener(new Uri(uri));
            try
            {
                httpListener.Start();

                var customAuthHandlerReject = new CustomAuthHandler(null, new HttpQueueSettings { LimitHttpResponseMaxSize = 10000 });

                customAuthHandlerReject.AddNewAuthProvider(uri, "", true, ClientAuthenticationType.Custom, false, "test");
                var peer = new TestCustomAuthPeer();

                var authRequest = new AuthenticateRequest
                {
                    UserId = UserId,
                    ClientAuthenticationParams = httpListener.GetStatusRequest(statusCode),
                    ClientAuthenticationType = (byte)ClientAuthenticationType.Custom,
                };

                customAuthHandlerReject.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

                Assert.That(peer.Signal.Wait(1000), Is.True);
                Assert.That(peer.OnCustomAuthenticationResultCalled, Is.True);
                Assert.That(peer.ResultCustomAuthResult.ResultCode, Is.EqualTo(CustomAuthenticationResultCode.Failed));

                var customAuthHandlerNonReject = new CustomAuthHandler(null, new HttpQueueSettings { LimitHttpResponseMaxSize = 10000 });

                customAuthHandlerNonReject.AddNewAuthProvider(uri, "", false, ClientAuthenticationType.Custom, false, "test");
                peer = new TestCustomAuthPeer();

                authRequest = new AuthenticateRequest
                {
                    UserId = UserId,
                    ClientAuthenticationParams = httpListener.GetStatusRequest(statusCode),
                    ClientAuthenticationType = (byte)ClientAuthenticationType.Custom,
                };

                customAuthHandlerNonReject.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

                Assert.That(peer.Signal.Wait(1000), Is.True);
                Assert.That(peer.OnCustomAuthenticationResultCalled, Is.True);
                Assert.That(peer.ResultCustomAuthResult.ResultCode, Is.EqualTo(CustomAuthenticationResultCode.Ok));
            }
            finally
            {
                httpListener.Dispose();
            }
        }


        [Test()]
        public void CustomAuthHttpResponseTooBig()
        {
            var uri = $"http://localhost:{55555}";
            var httpListener = new HttpTestListener(new Uri(uri));
            try
            {
                httpListener.Start();

                var customAuthHandlerReject = new CustomAuthHandler(null, new HttpQueueSettings { LimitHttpResponseMaxSize = 100 });

                customAuthHandlerReject.AddNewAuthProvider(uri, "", true, ClientAuthenticationType.Custom, false, "test");
                var peer = new TestCustomAuthPeer();

                var authRequest = new AuthenticateRequest
                {
                    UserId = UserId,
                    ClientAuthenticationParams = httpListener.GetTooBigResponse(),
                    ClientAuthenticationType = (byte)ClientAuthenticationType.Custom,
                };

                customAuthHandlerReject.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

                Assert.That(peer.Signal.Wait(1000), Is.True);
                Assert.That(peer.OnCustomAuthenticationResultCalled, Is.True);
                Assert.That(peer.ResultCustomAuthResult.ResultCode, Is.EqualTo(CustomAuthenticationResultCode.Failed));

                var customAuthHandlerNonReject = new CustomAuthHandler(null, new HttpQueueSettings { LimitHttpResponseMaxSize = 100 });

                customAuthHandlerNonReject.AddNewAuthProvider(uri, "", false, ClientAuthenticationType.Custom, false, "test");
                peer = new TestCustomAuthPeer();

                authRequest = new AuthenticateRequest
                {
                    UserId = UserId,
                    ClientAuthenticationParams = httpListener.GetTooBigResponse(),
                    ClientAuthenticationType = (byte)ClientAuthenticationType.Custom,
                };

                customAuthHandlerNonReject.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

                Assert.That(peer.Signal.Wait(1000), Is.True);
                Assert.That(peer.OnCustomAuthenticationResultCalled, Is.True);
                Assert.That(peer.ResultCustomAuthResult.ResultCode, Is.EqualTo(CustomAuthenticationResultCode.Failed));
            }
            finally
            {
                httpListener.Dispose();
            }
        }

        #region Helpers

        private object GetPostData(string requested)
        {
            switch (requested)
            {
                case PostDataTypeArray:
                    return this.PostDataArray;
                case PostDataTypeString:
                    return PostDataString;
                case PostDataTypeDict:
                    return new Dictionary<string, object>(this.PostDataDictionary);
                case PostDataTypeDictWithIntersect:
                    return new Dictionary<string, object>(this.PostDataDictionaryWithDashBoardIngtersection);
                case PostDataTypeOther:
                    return 1;
                case PostDataTypeNull:
                    return null;
                default:
                    Assert.Fail("Unknown type of post data - {0}", requested);
                    break;
            }

            return null;
        }

        private static string GetJSONFromQueryStrings(string dashboardParams, string clientQueryString, object getPostData)
        {
            var dictionary = new Dictionary<string, object>();

            if (getPostData != null)
            {
                dictionary = new Dictionary<string, object>((IDictionary<string, object>)getPostData);
            }
            var collection = HttpUtility.ParseQueryString(string.Empty + clientQueryString);

            for (int i = 0; i < collection.Count; i++)
            {
                if (!dictionary.ContainsKey(collection.GetKey(i)))
                {
                    dictionary.Add(collection.GetKey(i), collection.Get(i));
                }
            }

            collection = HttpUtility.ParseQueryString(string.Empty + dashboardParams);

            for (int i = 0; i < collection.Count; i++)
            {
                dictionary[collection.GetKey(i)] = collection.Get(i);
            }
            return JsonConvert.SerializeObject(dictionary);
        }

        #endregion
    }

    public class HttpTestListener : IDisposable
    {
        private readonly HttpListener listener;

        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private static readonly byte[] defaultResponseData = System.Text.Encoding.UTF8.GetBytes("Hello");

        public bool IsRunning { get; private set; }

        public bool IsDisposed { get; private set; }

        public Uri Url { get; }

        public HttpTestListener(bool start = true)
        {
            var uri = $"http://localhost:{55555}";
            this.Url = new Uri(uri);

            this.listener = new HttpListener();
            listener.Prefixes.Add(this.Url.ToString());

            if (start)
            {
                this.Start();
            }
        }

        public HttpTestListener(Uri uriPrefix)
        {
            this.Url = uriPrefix;
            this.listener = new HttpListener();
            listener.Prefixes.Add(uriPrefix.ToString());
        }

        public void Start()
        {
            lock (this.listener)
            {
                if (this.IsDisposed)
                {
                    throw new ObjectDisposedException(nameof(HttpTestListener));
                }

                if (this.IsRunning)
                {
                    return;
                }

                this.listener.Start();
                this.IsRunning = true;
                this.listener.GetContextAsync().ContinueWith(this.HandleRequest);
            }
        }

        public void Stop()
        {
            lock (this.listener)
            {
                if (IsDisposed)
                {
                    throw new ObjectDisposedException(nameof(HttpTestListener));
                }
                this.listener.Stop();
                this.IsRunning = false;
            }
        }

        public void Dispose()
        {
            lock (this.listener)
            {
                if (this.IsDisposed)
                {
                    return;
                }

                this.Stop();
                this.IsDisposed = true;

                this.listener.Abort();
            }
        }

        public string GetStatusRequest(HttpStatusCode statusCode)
        {
            return $"statusCode={statusCode}";
        }

        public string GetTimeoutRequest()
        {
            return "timeout=true";
        }

        public string GetTooBigResponse()
        {
            return @"toobig=1000";
        }

        public string GetTooBigResponseChunked()
        {
            return "chunksending=1000";
        }

        private async void HandleRequest(Task<HttpListenerContext> contextTask)
        {
            if (contextTask.IsCanceled)
            {
                if (log.IsDebugEnabled)
                {
                    log.Debug("Task canceled");
                }
                return;
            }

            if (contextTask.IsFaulted && contextTask.Exception?.InnerException is ObjectDisposedException)
            {
                log.Debug("Task faulted with ObjectDisposedException");
                return;
            }

            try
            {
                _ = this.listener.GetContextAsync().ContinueWith(this.HandleRequest, TaskContinuationOptions.OnlyOnRanToCompletion);
            }
            catch (ObjectDisposedException) { }

            if (!contextTask.IsFaulted)
            {
                await HandleRequest(contextTask.Result);
            }
        }

        private static async Task HandleRequest(HttpListenerContext context)
        {
            try
            {
                if (log.IsDebugEnabled)
                {
                    log.Debug("Handling of the request");
                }

                var request = context.Request;

                if (((IList) request.QueryString.AllKeys).Contains("timeout"))
                {
                    return;
                }

                var delayParam = request.QueryString["delay"];
                if (delayParam != null && int.TryParse(delayParam, out int delay))
                {
                    if (log.IsDebugEnabled)
                    {
                        log.Debug("delay before answer");
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(delay));
                }

                context.Response.StatusCode = (int)GetStatusCode(request);

                var responseData = GetResponseData(request);
                if (request.QueryString.AllKeys.Contains(@"longsending"))
                {
                    if (log.IsDebugEnabled)
                    {
                        log.Debug("long sending");
                    }

                    context.Response.ContentLength64 = responseData.Length;
                    context.Response.OutputStream.Write(responseData, 0, responseData.Length / 2);
                    context.Response.OutputStream.Flush();

                    await Task.Delay(3500);

                    context.Response.OutputStream.Write(responseData, responseData.Length / 2, responseData.Length - responseData.Length / 2);
                    context.Response.OutputStream.Flush();
                }
                else if (request.QueryString.AllKeys.Contains(@"chunksending"))
                {
                    if (log.IsDebugEnabled)
                    {
                        log.Debug("chunk sending");
                    }

                    responseData = new byte[2048];

                    context.Response.SendChunked = true;
                    context.Response.OutputStream.Write(responseData, 0, responseData.Length / 2);
                    context.Response.OutputStream.Flush();

                    await Task.Delay(500);

                    context.Response.OutputStream.Write(responseData, responseData.Length / 2, responseData.Length - responseData.Length / 2);
                    context.Response.OutputStream.Flush();
                }
                else if (request.QueryString.AllKeys.Contains(@"toobig"))
                {
                    responseData = new byte[1000];
                    context.Response.ContentLength64 = responseData.Length;
                    context.Response.OutputStream.Write(responseData, 0, responseData.Length);
                    context.Response.OutputStream.Flush();
                }
                else
                {
                    if (log.IsDebugEnabled)
                    {
                        log.Debug("sending...");
                    }
                    context.Response.OutputStream.Write(responseData, 0, responseData.Length);
                }

                context.Response.Close();
            }
            catch (Exception e)
            {
                log.Error(e);

                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                context.Response.StatusDescription = e.Message;
                context.Response.Close();
            }
        }

        private static HttpStatusCode GetStatusCode(HttpListenerRequest request)
        {
            string codeString = request.QueryString["statusCode"];
            if (string.IsNullOrEmpty(codeString))
                return HttpStatusCode.OK;

            if (int.TryParse(codeString, out var code))
                return (HttpStatusCode)code;

            if (Enum.TryParse<HttpStatusCode>(codeString, true, out var statusCode))
                return statusCode;

            return HttpStatusCode.OK;
        }

        private static byte[] GetResponseData(HttpListenerRequest request)
        {
            if (!request.HasEntityBody)
            {
                return defaultResponseData;
            }

            var result = new byte[request.ContentLength64];
            request.InputStream.Read(result, 0, result.Length);
            return result;
        }
    }

    class TestCustomAuthPeer : ICustomAuthPeer
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();
        public TestCustomAuthPeer()
        {
            this.ConnectionId = 0;
        }

        public int ConnectionId { get; private set; }
        public string UserId { get; set; }
        public bool OnCustomAuthenticationResultCalled { get; internal set; }
        public bool OnCustomAuthenticationErrorCalled { get; internal set; }
        public CustomAuthenticationResult ResultCustomAuthResult { get; set; }
        public ErrorCode ResultErrorCode { get; set; }

        public ManualResetEventSlim Signal { get; } = new ManualResetEventSlim(false);

        public void OnCustomAuthenticationError(ErrorCode errorCode, string debugMessage, IAuthenticateRequest authenticateRequest, SendParameters sendParameters)
        {
            log.Warn($"error during custom auth request execution, errorCode={errorCode}, errorMsg={debugMessage}");
            this.OnCustomAuthenticationErrorCalled = true;
            this.ResultErrorCode = errorCode;
            this.Signal.Set();
        }

        public void OnCustomAuthenticationResult(CustomAuthenticationResult customAuthResult, IAuthenticateRequest authenticateRequest,
            SendParameters sendParameters, object state)
        {
            this.OnCustomAuthenticationResultCalled = true;
            this.ResultCustomAuthResult = customAuthResult;
            this.Signal.Set();
        }
    }

    class TestClientAuthQueue : IClientAuthenticationQueue
    {
        private readonly string JsonTestResponse;

        public TestClientAuthQueue(string uri, string queryStringParameters, bool forwardAsJSON)
        {
            this.Uri = uri;
            this.QueryStringParameters = queryStringParameters;
            if (!string.IsNullOrEmpty(queryStringParameters))
            {
                this.QueryStringParametersCollection = HttpUtility.ParseQueryString(queryStringParameters);
            }
            this.ForwardAsJSON = forwardAsJSON;
            this.ClientAuthenticationType = ClientAuthenticationType.Custom;
        }

        public TestClientAuthQueue(string uri, string queryStringParameters, bool forwardAsJSON, string jsonTestResponse) : this (uri, queryStringParameters, forwardAsJSON)
        {
            this.JsonTestResponse = jsonTestResponse;
        }

        public NameValueCollection QueryStringParametersCollection { get; private set; }
        public string Uri { get; private set; }
        public string QueryStringParameters { get; private set; }
        public bool RejectIfUnavailable { get; private set; }
        public bool ForwardAsJSON { get; private set; }
        public ClientAuthenticationType ClientAuthenticationType { get; private set; }

        public string ResutlClientQueryStringParameters { get; private set; }
        public byte[] ResutlPostData { get; private set; }
        public string ResultContentType { get; private set; }
        public bool ExecuteRequestCalled { get; private set; }
        public int ExecuteRequestCallsCount { get; private set; }

        public object CustomData { get; set; }
        public void EnqueueRequest(string clientQueryStringParameters, byte[] postData, string contentType, Action<AsyncHttpResponse, IClientAuthenticationQueue> callback, object state, bool checkUrl = true)
        {
            this.ExecuteRequestCalled = true;
            ++this.ExecuteRequestCallsCount;
            this.ResutlClientQueryStringParameters = clientQueryStringParameters;
            this.ResutlPostData = postData;
            this.ResultContentType = contentType;
            this.RejectIfUnavailable = true;

            if (this.Uri.Contains("wrong"))
            {
                callback(new AsyncHttpResponse(HttpRequestQueueResultCode.Error, this.RejectIfUnavailable, state), this);
            }

            if(!string.IsNullOrEmpty(this.JsonTestResponse))
            {
                callback(new TestAsyncHttpResponse(HttpRequestQueueResultCode.Success, this.RejectIfUnavailable, state, Encoding.UTF8.GetBytes(this.JsonTestResponse)), this);
            }
        }

        public void EnqueueRequestWithExpectedStatusCodes(HttpWebRequest webRequest, byte[] postData, Action<AsyncHttpResponse, IClientAuthenticationQueue> callback, object state, List<HttpStatusCode> expectedStatusCodes)
        {
            throw new NotImplementedException();
        }
    }

    class TestCustomAuthHandler : CustomAuthHandler
    {
        class Fiber : IFiber
        {
            public void Enqueue(Action action)
            {
                action();
            }

            public void Enqueue(IFiberAction action)
            {
                action.Execute();
            }

            public void Start()
            {

            }

            public void RegisterSubscription(IDisposable toAdd)
            {

            }

            public bool DeregisterSubscription(IDisposable toRemove)
            {
                throw new NotImplementedException();
            }

            public IDisposable Schedule(Action action, int firstInMs)
            {
                throw new NotImplementedException();
            }

            public IDisposable ScheduleOnInterval(Action action, int firstInMs, int regularInMs)
            {
                throw new NotImplementedException();
            }

            public IDisposable Schedule(IFiberAction action, int firstInMs)
            {
                throw new NotImplementedException();
            }

            public IDisposable ScheduleOnInterval(IFiberAction action, int firstInMs, int regularInMs)
            {
                throw new NotImplementedException();
            }

            public void Dispose()
            {
            }
        }

        public int ErrorsCount;

        public TestCustomAuthHandler(bool anonymous = false) : base(null, new Fiber(), new HttpQueueSettings { LimitHttpResponseMaxSize = 100000 })
        {
            this.IsAnonymousAccessAllowed = anonymous;
        }

        public TestClientAuthQueue AddQueue(string url, string clientQueryString, bool forwardAsJSON = false, ClientAuthenticationType authType = ClientAuthenticationType.Custom, string jsonResponse = null)
        {
            return (TestClientAuthQueue)this.AddNewAuthProvider(url, clientQueryString, true, authType, forwardAsJSON, jsonResponse);
        }

        protected override IClientAuthenticationQueue CreateClientAuthenticationQueue(string url, string nameValuePairAsQueryString,
            bool rejectIfUnavailable, ClientAuthenticationType authenticationType, bool forwardAsJson, string instanceName)
        {
            TestClientAuthQueue queue;
            if (string.IsNullOrEmpty(instanceName))
            {
                queue = new TestClientAuthQueue(url, nameValuePairAsQueryString, forwardAsJson);
            }
            else
            {
                queue = new TestClientAuthQueue(url, nameValuePairAsQueryString, forwardAsJson, instanceName);
            }

            return queue;
        }

        protected override void IncrementErrors(ClientAuthenticationType authenticationType, CustomAuthResultCounters instance)
        {
            ++this.ErrorsCount;
        }
    }

    public class TestAsyncHttpResponse : AsyncHttpResponse
    {
        public TestAsyncHttpResponse(HttpRequestQueueResultCode status, bool rejectIfUnavailable, object state, byte[] responseData) : base(status, rejectIfUnavailable, state)
        {
            this.ResponseData = responseData;
        }
    }

}
