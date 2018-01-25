//
// Copyright 2016 by Avid Technology, Inc.
//

using Avid.Platform.SDK;
using Avid.Platform.SDK.Authorization;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
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

        /// <summary>
        /// MCUX-identity-provider based authorization via credentials and cookies.
        /// The used server-certificate validation is tolerant. Creates an authorized HttpClient instance,
        /// following the MCUX-identity-provider authorization strategy.
        /// The resulting HttpClient instance refers a set of cookies, which are required for the communication
        /// with the platform. - Just use this HttpClient instance for future calls against the platform.
        /// </summary>
        /// <param name="apiDomain">address to get "auth"</param>
        /// <param name="username">MCUX login</param>
        /// <param name="password">MCUX password</param>
        /// <returns>An authorized HttpClient instance, or null if something went wrong, esp. authorization went wrong.</returns>
        public static HttpClient Authorize(string apiDomain, string username, string password)
        {
            WebRequestHandler webRequesthandler = new WebRequestHandler
            {
                CookieContainer = new CookieContainer(),
                // Establish tolerant certificate check:
                ServerCertificateValidationCallback = delegate { return true; },
                UnsafeAuthenticatedConnectionSharing = true,
                AllowAutoRedirect = false
            };

            HttpClient httpClient = new HttpClient(webRequesthandler);
            httpClient.Timeout = DefaultRequestTimeout;
            httpClient.BaseAddress = new UriBuilder("https", apiDomain).Uri;

            /// Authorization procedure:
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            // Get identity providers:
            string rawAuthResult = httpClient.GetStringAsync(string.Format("https://{0}/auth", apiDomain)).Result;
            dynamic authResult = JObject.Parse(rawAuthResult);
            string urlIdentityProviders = authResult.SelectToken("$._links.auth:identity-providers[0].href");

            // Select MC|UX identity provider and retrieve login URL:
            string rawIdentityProvidersResult = httpClient.GetStringAsync(urlIdentityProviders).Result;
            dynamic identityProvidersResult = JObject.Parse(rawIdentityProvidersResult);
            dynamic mcuxLoginHrefObject = identityProvidersResult.SelectToken("$._embedded.auth:identity-provider[?(@.kind == 'mcux')]._links.auth-mcux:login[0].href");

            if (null == mcuxLoginHrefObject)
            {
                // MCUX authentication not supported
                return null;
            }

            string urlLogin = mcuxLoginHrefObject.ToString();

            // Do the login:
            JObject loginContent
                = new JObject(
                    new JProperty("username", username),
                    new JProperty("password", password)
                );
            using (HttpContent content = new ObjectContent<JObject>(loginContent, new System.Net.Http.Formatting.JsonMediaTypeFormatter()))
            {
                using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, urlLogin) { Content = content })
                {
                    using (HttpResponseMessage result = httpClient.SendAsync(request).Result)
                    {
                        if (HttpStatusCode.RedirectMethod == result.StatusCode || result.IsSuccessStatusCode)
                        {
                            HttpClient httpKeepAliveClient = new HttpClient(webRequesthandler);
                            httpKeepAliveClient.Timeout = DefaultRequestTimeout;
                            httpKeepAliveClient.BaseAddress = new UriBuilder("https", apiDomain).Uri;
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
            // TODO: this is a workaround, see {CORE-7359}. In future the access token prolongation API should be used.
            string urlPing = string.Format("https://{0}/api/middleware/service/ping", httpClient.BaseAddress.Host);
            HttpResponseMessage httpResponseMessage = httpClient.GetAsync(urlPing).Result;
        }

        /// <summary>
        /// Performs a logout against the platform.
        /// </summary>
        /// <param name="httpClient">The HttpClient against the platform.</param>
        public static void Logout(HttpClient httpClient)
        {
            httpClient.DefaultRequestHeaders.Remove("Accept");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            /// Logout from platform:
            string rawAuthResult = httpClient.GetStringAsync(string.Format("https://{0}/auth", httpClient.BaseAddress.Host)).Result;
            dynamic authResult = JObject.Parse(rawAuthResult);
            string urlCurrentToken = authResult.SelectToken("$._links.auth:token[?(@.name == 'current')].href").ToString();

            string rawCurrentTokenResult = httpClient.GetStringAsync(urlCurrentToken).Result;
            dynamic currentTokenResult = JObject.Parse(rawCurrentTokenResult);
            string urlCurrentTokenRemoval = currentTokenResult.SelectToken("$._links.auth-token:removal[0].href");
            // Should result in 204:
            HttpResponseMessage httpResponseMessage = httpClient.DeleteAsync(urlCurrentTokenRemoval).Result;


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

        //public static List<String> findInRegistry(String apiDomain, List<String> serviceTypes, String registryServiceVersion, String resourceName, String orDefaultUriTemplate) throws IOException
        public static List<String> FindInRegistry(HttpClient httpClient, String apiDomain, List<String> serviceTypes, String registryServiceVersion, String resourceName, String orDefaultUriTemplate)
        {
            List<string> foundUriTemplates = new List<string>();   
            httpClient.DefaultRequestHeaders.Remove("Accept");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/hal+json");

            /// Check, whether simple search is supported:
            Uri serviceRootsResourceURL = new Uri(string.Format("https://{0}/apis/avid.ctms.registry;version={1}/serviceroots", apiDomain, registryServiceVersion));
            HttpResponseMessage searchesResponse = httpClient.GetAsync(serviceRootsResourceURL).Result;
            HttpStatusCode searchesStatus = searchesResponse.StatusCode;
            if (HttpStatusCode.OK == searchesStatus)
            {
                    string rawSearchesResult = searchesResponse.Content.ReadAsStringAsync().Result;
                    JObject serviceRootsResult = JObject.Parse(rawSearchesResult);
                    JObject resources = (JObject)serviceRootsResult.GetValue("resources");
                    var resourceToken = resources.GetValue(resourceName);

                if (resourceToken != null)
                {
                    if (resourceToken is JArray)
                    {
                        var asArray = (JArray)resourceToken;
                        foreach (var item in asArray)
                        {
                            string href = item["href"].ToString();
                            if (serviceTypes.Any(it => href.Contains(it)))
                            {
                                foundUriTemplates.Add(href);
                            }
                        }
                        if (!foundUriTemplates.Any())
                        {
                            foundUriTemplates.Add(orDefaultUriTemplate);
                        }

                    }
                    else
                    {
                        string href = resourceToken["href"].ToString();
                        if (serviceTypes.Any(it => href.Contains(it)))
                        {
                            foundUriTemplates.Add(href);
                        }
                        else
                        {
                            foundUriTemplates.Add(orDefaultUriTemplate);
                        }

                    }

                }
                else {
                    if (!foundUriTemplates.Any())
                    {
                        foundUriTemplates.Add(orDefaultUriTemplate);
                    }
                }

            }
            return foundUriTemplates;
               
        }

    }
}
