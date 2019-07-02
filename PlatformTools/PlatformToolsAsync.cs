//
// Copyright 2016 by Avid Technology, Inc.
//

using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// A set of tooling methods.
/// </summary>
namespace PlatformTools
{
    /// <summary>
    /// A set of tooling methods.
    /// </summary>
    public static class PlatformToolsAsync
    {        
        private static CancellationTokenSource tokenSource;

        /// <summary>
        /// Retrieves the the default request timeout.
        /// </summary>
        public static TimeSpan DefaultRequestTimeout
        {
            get
            {
                return TimeSpan.FromMilliseconds(60000);
            }
        }

        public static async Task<HttpClient> Prepare(string apiDomain)
        {
            WebRequestHandler webRequesthandler = new WebRequestHandler
            {
                CookieContainer = new CookieContainer(),
                // Establish tolerant certificate check:
                ServerCertificateValidationCallback = delegate { return true; },
                UnsafeAuthenticatedConnectionSharing = true,
                AllowAutoRedirect = false,
                UseCookies = false
            };

            HttpClient httpClient = new HttpClient(webRequesthandler);
            httpClient.Timeout = DefaultRequestTimeout;
            httpClient.BaseAddress = new UriBuilder("https", apiDomain).Uri;

            /// Authorization procedure:
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            return httpClient;
        }

        public static async Task<JObject> GetAuthEndpoint(HttpClient httpClient, string apiDomain)
        {
            string rawAuthResult = await httpClient.GetStringAsync($"https://{apiDomain}/auth");

            return JObject.Parse(rawAuthResult);
        }

        public static async Task<JObject> GetIdentityProviders(HttpClient httpClient, JObject response)
        {
            dynamic dynamicResponseHandle = response;
            string urlIdentityProviders = dynamicResponseHandle.SelectToken("$._links.auth:identity-providers[0].href");
            string rawIdentityProvidersResult = await httpClient.GetStringAsync(urlIdentityProviders);

            return JObject.Parse(rawIdentityProvidersResult);
        }

        public static async Task<HttpClient> Authorize(HttpClient httpClient, JObject response, string apiDomain, string httpBasicAuthString)
        {
            // Select OAuth2 identity provider and retrieve login URL:
            dynamic dynamicResponseHandle = response;
            dynamic oAuthLoginHrefObject = dynamicResponseHandle.SelectToken("$._embedded.auth:identity-provider[?(@.kind == 'oauth')]._links.auth:ropc-default[0].href");

            if (null == oAuthLoginHrefObject)
            {
                throw new Exception("OAuth2 authentication not supported");
            }

            string OAuth2EndPoint = oAuthLoginHrefObject.ToString();
            // Do the login:
            IDictionary<string, string> loginArguments = new Dictionary<string, string>(3)
            {
                { "grant_type", "client_credentials" },
                { "scope", "openid" }
            };

            using (HttpContent content = new FormUrlEncodedContent(loginArguments))
            {
                using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, OAuth2EndPoint) { Content = content })
                {
                    request.Headers.Add("Authorization", $"Basic {httpBasicAuthString}");

                    using (HttpResponseMessage result = await httpClient.SendAsync(request))
                    {
                        if (HttpStatusCode.RedirectMethod == result.StatusCode || result.IsSuccessStatusCode)
                        {
                            WebRequestHandler webRequesthandler = new WebRequestHandler
                            {
                                CookieContainer = new CookieContainer(),
                                // Establish tolerant certificate check:
                                ServerCertificateValidationCallback = delegate { return true; },
                                UnsafeAuthenticatedConnectionSharing = true,
                                AllowAutoRedirect = false,
                                UseCookies = false
                            };

                            string rawAuthorizationData = result.Content.ReadAsStringAsync().Result;
                            dynamic authorizationData = JObject.Parse(rawAuthorizationData);
                            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", $"avidAccessToken={authorizationData.access_token}");

                            HttpClient httpKeepAliveClient = new HttpClient(webRequesthandler);
                            httpKeepAliveClient.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", $"avidAccessToken={authorizationData.access_token}");
                            httpKeepAliveClient.Timeout = DefaultRequestTimeout;
                            httpKeepAliveClient.BaseAddress = new Uri($"http://{apiDomain}");
                            httpKeepAliveClient.DefaultRequestHeaders.Add("Accept", "application/json");
                            tokenSource = new CancellationTokenSource();
                            Task sessionRefresher
                                = Task.Factory.StartNew(
                                    async ignored =>
                                    {
                                        while (!tokenSource.Token.IsCancellationRequested)
                                        {
                                            await Task.Delay(120000, tokenSource.Token);
                                            tokenSource.Token.ThrowIfCancellationRequested();

                                            SessionKeepAlive(httpKeepAliveClient);
                                        }
                                    }
                                    , null
                                    , tokenSource.Token
                                    , TaskCreationOptions.DenyChildAttach | TaskCreationOptions.LongRunning
                                    , TaskScheduler.Default);
                        }
                        else
                        {
                            throw new Exception(result.ReasonPhrase);
                        }
                    }
                }
            }

            return httpClient;
        }

        public static async Task<JObject> GetCurrentToken(HttpClient httpClient, JObject response, string apiDomain)
        {
            dynamic dynamicResponseHandle = response;
            string urlCurrentToken = dynamicResponseHandle.SelectToken("$._links.auth:token[?(@.name == 'current')].href").ToString();


            string rawCurrentTokenResult = await httpClient.GetStringAsync(urlCurrentToken);
            return JObject.Parse(rawCurrentTokenResult);
        }

        public static async Task RemoveToken(HttpClient httpClient, JToken response)
        {
            dynamic dynamicResponseHandle = response;
            string urlTokenRemoval = dynamicResponseHandle.SelectToken("$._links.auth-token:removal[0].href");
            // Should result in 204:
            HttpResponseMessage httpResponseMessage = await httpClient.DeleteAsync(urlTokenRemoval);

            /// Unregister the keep alive task:
            if (null != tokenSource)
            {
                try
                {
                    tokenSource.Cancel();
                }
                catch (OperationCanceledException ignored)
                {
                    // Yes, this is expected.
                }
            }
        }

        /// <summary>
        /// Signals the platform, that our session is still in use.
        /// </summary>
        /// <param name="httpClient">The HttpClient against the platform.</param>
        public static async void SessionKeepAlive(HttpClient httpClient) 
        {
            string rawAuthResult = await httpClient.GetStringAsync($"https://{httpClient.BaseAddress.Host}/auth");
            dynamic authResult = JObject.Parse(rawAuthResult);
            string urlCurrentToken = authResult._links["auth:token"][0].href;

            string rawCurrentTokenResult = await httpClient.GetStringAsync(urlCurrentToken);
            dynamic currentTokenResult = JObject.Parse(rawCurrentTokenResult);

            string urlExtend = currentTokenResult._links["auth-token:extend"][0].href;

            try
            {
                using (HttpContent refreshRequestContent = new StringContent(string.Empty))
                {
                    using (HttpResponseMessage httpResponseMessage = await httpClient.PostAsync(urlExtend, refreshRequestContent))
                    {
                    }
                }
            }
            catch
            {
                // Nothing. Usually those exceptions can be ignored.
            }
        }

        /// <summary>
        /// Performs a logout against the platform.
        /// </summary>
        /// <param name="httpClient">The HttpClient against the platform.</param>
        public static async Task<HttpResponseMessage> Logout(HttpClient httpClient) 
        {
            httpClient.DefaultRequestHeaders.Remove("Accept");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            /// Logout from platform:
            string authUri = $"https://{httpClient.BaseAddress.Host}/auth";
            string rawAuthResult = await httpClient.GetStringAsync(authUri);
            dynamic authResult = JObject.Parse(rawAuthResult);
            string urlCurrentToken = authResult.SelectToken("$._links.auth:token[?(@.name == 'current')].href").ToString();

            string rawCurrentTokenResult = await httpClient.GetStringAsync(urlCurrentToken);
            dynamic currentTokenResult = JObject.Parse(rawCurrentTokenResult);
            string urlCurrentTokenRemoval = currentTokenResult.SelectToken("$._links.auth-token:removal[0].href");
            // Should result in 204:
            HttpResponseMessage httpResponseMessage = await httpClient.DeleteAsync(urlCurrentTokenRemoval);

            /// Unregister the keep alive task:
            if (null != tokenSource) 
            {
                try
                {
                    tokenSource.Cancel();
                }
                catch (OperationCanceledException ignored)
                {
                    // Yes, this is expected.
                }
            }

            return httpResponseMessage;
        }

        public static async Task<string> FindInRegistry(HttpClient httpClient, string apiDomain, string serviceType, string registryServiceVersion, string resourceName, string orDefaultUriTemplate, string realm)
        {
            httpClient.DefaultRequestHeaders.Remove("Accept");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/hal+json");

            /// Check, whether simple search is supported:          
            Uri serviceRootsResourceURL = new Uri($"https://{apiDomain}/apis/avid.ctms.registry;version={registryServiceVersion}/serviceroots");
            using (HttpResponseMessage registryResultsResponse = await httpClient.GetAsync(serviceRootsResourceURL))
            {
                HttpStatusCode registryResultStatus = registryResultsResponse.StatusCode;
                if (HttpStatusCode.OK == registryResultStatus)
                {
                    string rawSearchesResult = await registryResultsResponse.Content.ReadAsStringAsync();
                    JObject serviceRootsResult = JObject.Parse(rawSearchesResult);
                    JObject resources = (JObject)serviceRootsResult.GetValue("resources");
                    var resourceToken = resources.GetValue(resourceName);

                    if (resourceToken != null)
                    {
                        var candidateSystemIds
                                    = resourceToken.SelectTokens("..systems..systemID")
                                        .Select(it => it.ToString())
                                        .ToArray();

                        string effectiveRealm = realm;
                        if (!candidateSystemIds.Contains(realm))
                        {
                            effectiveRealm = candidateSystemIds.FirstOrDefault();
                            Console.WriteLine($"'{resourceName}' was not available on realm {realm}. Falling back to {effectiveRealm}.");
                        }

                        string href = resourceToken.SelectToken($"..systems[?(@.systemID == '{effectiveRealm}')]").Parent.Parent.Parent["href"].ToString();
                        return href;
                    }
                    else
                    {
                        return orDefaultUriTemplate;
                    }
                }
            }

            return null;
        }
    }
}
