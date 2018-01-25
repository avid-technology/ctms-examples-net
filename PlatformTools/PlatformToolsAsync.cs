﻿//
// Copyright 2016 by Avid Technology, Inc.
//

using Newtonsoft.Json.Linq;
using System;
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
                AllowAutoRedirect = false
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
            string rawAuthResult = await httpClient.GetStringAsync(string.Format("https://{0}/auth", apiDomain));

            return JObject.Parse(rawAuthResult);
        }

        public static async Task<JObject> GetIdentityProviders(HttpClient httpClient, JObject response)
        {
            dynamic dynamicResponseHandle = response;
            string urlIdentityProviders = dynamicResponseHandle.SelectToken("$._links.auth:identity-providers[0].href");
            string rawIdentityProvidersResult = await httpClient.GetStringAsync(urlIdentityProviders);

            return JObject.Parse(rawIdentityProvidersResult);
        }

        public static async Task<HttpClient> Authorize(HttpClient httpClient, JObject response, string apiDomain, string username, string password)
        {
            // Select MC|UX identity provider and retrieve login URL:
            dynamic dynamicResponseHandle = response;
            dynamic mcuxLoginHrefObject = dynamicResponseHandle.SelectToken("$._embedded.auth:identity-provider[?(@.kind == 'mcux')]._links.auth-mcux:login[0].href");

            if (null == mcuxLoginHrefObject)
            {
                throw new Exception("MCUX authentication not supported");
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
                                AllowAutoRedirect = false
                            };
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
        public static async Task<HttpClient> AuthorizeAsync(string apiDomain, string username, string password)
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
            string rawAuthResult = await httpClient.GetStringAsync(string.Format("https://{0}/auth", apiDomain));

            dynamic authResult = JObject.Parse(rawAuthResult);
            string urlIdentityProviders = authResult.SelectToken("$._links.auth:identity-providers[0].href");

            // Select MC|UX identity provider and retrieve login URL:
            string rawIdentityProvidersResult = await httpClient.GetStringAsync(urlIdentityProviders);
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
                    using (HttpResponseMessage result = await httpClient.SendAsync(request))
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
        public static async void SessionKeepAlive(HttpClient httpClient) 
        {
            // TODO: this is a workaround, see {CORE-7359}. In future the access token prolongation API should be used.
            string urlPing = string.Format("https://{0}/api/middleware/service/ping", httpClient.BaseAddress.Host);
            HttpResponseMessage httpResponseMessage = await httpClient.GetAsync(urlPing);
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
            string rawAuthResult = await httpClient.GetStringAsync(string.Format("https://{0}/auth", httpClient.BaseAddress.Host));
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
    }
}
