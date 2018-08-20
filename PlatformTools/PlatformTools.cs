//
// Copyright 2016 by Avid Technology, Inc.
//

using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
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
    public static class PlatformTools
    {
        private static CancellationTokenSource tokenSource;

        public static TimeSpan DefaultRequestTimeout
        {
            get
            {
                return TimeSpan.FromMilliseconds(60000);
            }
        }

        private static WebRequestHandler DefaultWebRequestHandler(bool autoRedirect)
        {
            WebRequestHandler wrh = new WebRequestHandler
            {
                AllowAutoRedirect = autoRedirect,
                CookieContainer = new CookieContainer(),
                // Establish tolerant certificate check:
                ServerCertificateValidationCallback = delegate { return true; },
                UnsafeAuthenticatedConnectionSharing = true,
                ReadWriteTimeout = 500000,
                UseCookies = false // Allows to set cookies after HttpClient creation.
            };
            return wrh;
        }

        /// <summary>
        /// OAuth2-identity-provider based authorization via via credentials and an OAuth2Token (= HTTP basic auth string).
        /// The used server-certificate validation is tolerant. Creates an authorized HttpClient instance,
        /// following the OAuth2-identity-provider authorization strategy.
        /// Just use the returned HttpClient instance for future calls against the platform.
        /// </summary>
        /// <param name="apiDomain">address to get "auth"</param>
        /// <param name="OAuth2Token">OAuth2 token</param>
        /// <param name="username">Platform login</param>
        /// <param name="password">Platform password</param>
        /// <returns>An authorized HttpClient instance, or null if something went wrong, esp. authorization went wrong.</returns>
        public static HttpClient Authorize(string apiDomain, string OAuth2Token, string username, string password)
        {
            WebRequestHandler webRequesthandler = DefaultWebRequestHandler(false);

            HttpClient httpClient = new HttpClient(webRequesthandler);
            httpClient.Timeout = DefaultRequestTimeout;
            httpClient.BaseAddress = new UriBuilder("https", apiDomain).Uri;

            /// Authorization procedure:
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            // Get identity providers:
            string rawAuthResult = httpClient.GetStringAsync($"https://{apiDomain}/auth").Result;
            dynamic authResult = JObject.Parse(rawAuthResult);
            string urlIdentityProviders = authResult.SelectToken("$._links.auth:identity-providers[0].href");

            // Select OAuth2 identity provider and retrieve login URL:
            string rawIdentityProvidersResult = httpClient.GetStringAsync(urlIdentityProviders).Result;
            dynamic identityProvidersResult = JObject.Parse(rawIdentityProvidersResult);
            dynamic oAuthLoginHrefObject = identityProvidersResult.SelectToken("$._embedded.auth:identity-provider[?(@.kind == 'oauth')]._links.auth:ropc-default[0].href");

            if (null == oAuthLoginHrefObject)
            {
                // OAuth2 authentication not supported
                return null;
            }

            string OAuth2EndPoint = oAuthLoginHrefObject.ToString();

            // Do the login:
            IDictionary<string, string> loginArguments = new Dictionary<string, string>(3)
            {
                { "username", username },
                { "password", password },
                { "grant_type", "password" },
            };

            using (HttpContent content = new FormUrlEncodedContent(loginArguments))
            {
                using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, OAuth2EndPoint) { Content = content })
                {
                    request.Headers.Add("Accept", "application/json");
                    request.Headers.Add("Authorization", "Basic " + OAuth2Token);

                    using (HttpResponseMessage result = httpClient.SendAsync(request).Result)
                    {
                        if (HttpStatusCode.RedirectMethod == result.StatusCode || result.IsSuccessStatusCode)
                        {
                            string rawAuthorizationData = result.Content.ReadAsStringAsync().Result;
                            dynamic authorizationData = JObject.Parse(rawAuthorizationData);                      
                            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", "avidAccessToken=" + authorizationData.access_token);

                            HttpClient httpKeepAliveClient = new HttpClient(DefaultWebRequestHandler(false));
                            httpKeepAliveClient.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", "avidAccessToken=" + authorizationData.access_token);
                            httpKeepAliveClient.BaseAddress = new Uri($"http://{apiDomain}");
                            httpKeepAliveClient.DefaultRequestHeaders.Add("Accept", "application/json");
                            tokenSource = new CancellationTokenSource();
                            Task sessionRefresher
                                = Task.Factory.StartNew(
                                    async ignored =>
                                    {
                                        while (!tokenSource.Token.IsCancellationRequested)
                                        {
                                            await Task.Delay(120000, tokenSource.Token).ConfigureAwait(false);
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
                            return null;
                        }
                    }
                }
            }

            return httpClient;
        }

        /// <summary>
        /// Signals the platform, that our session is still in use.
        /// </summary>
        /// <param name="httpClient">The HttpClient against the platform.</param>
        public static void SessionKeepAlive(HttpClient httpClient)
        {
            string urlExtend = $"https://{httpClient.BaseAddress.Host}/auth/tokens/current/extension";
            try
            {
                using (HttpContent refreshRequestContent = new StringContent(string.Empty))
                {
                    using (HttpResponseMessage httpResponseMessage = httpClient.PostAsync(urlExtend, refreshRequestContent).Result)
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
        public static void Logout(HttpClient httpClient)
        {
            /// Logout from platform:
            string authUri = $"https://{httpClient.BaseAddress.Host}/auth";
            string rawAuthResult = httpClient.GetStringAsync(authUri).Result;
            dynamic authResult = JObject.Parse(rawAuthResult);
            string urlCurrentToken = authResult.SelectToken("$._links.auth:token[?(@.name == 'current')].href").ToString();

            string rawCurrentTokenResult = httpClient.GetStringAsync(urlCurrentToken).Result;
            dynamic currentTokenResult = JObject.Parse(rawCurrentTokenResult);
            string urlCurrentTokenRemoval = currentTokenResult.SelectToken("$._links.auth-token:removal[0].href");
            // Should result in 204:
            using (HttpResponseMessage httpResponseMessage = httpClient.DeleteAsync(urlCurrentTokenRemoval).Result)
            {
            }

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

        public static string FindInRegistry(HttpClient httpClient, string apiDomain, string serviceType, string registryServiceVersion, string resourceName, string orDefaultUriTemplate, string realm)
        {
            httpClient.DefaultRequestHeaders.Remove("Accept");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/hal+json");

            /// Check, whether simple search is supported:          
            Uri serviceRootsResourceURL = new Uri($"https://{apiDomain}/apis/avid.ctms.registry;version={registryServiceVersion}/serviceroots");
            using (HttpResponseMessage registryResultsResponse = httpClient.GetAsync(serviceRootsResourceURL).Result)
            {
                HttpStatusCode registryResultStatus = registryResultsResponse.StatusCode;
                if (HttpStatusCode.OK == registryResultStatus)
                {
                    string rawSearchesResult = registryResultsResponse.Content.ReadAsStringAsync().Result;
                    JObject serviceRootsResult = JObject.Parse(rawSearchesResult);
                    JObject resources = (JObject)serviceRootsResult.GetValue("resources");
                    var resourceToken = resources.GetValue(resourceName);

                    if (resourceToken != null)
                    {
                        var candidateSystemIds
                                    = resourceToken.SelectTokens("..systems..systemID")                                        
                                        .Select(it => it.ToString())
                                        .ToArray();

                        string effectiveRealm = null;
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
