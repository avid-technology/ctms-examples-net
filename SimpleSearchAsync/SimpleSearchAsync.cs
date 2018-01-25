//
// Copyright 2016 by Avid Technology, Inc.
//

using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Web;
using System.Collections.Generic;

namespace SimpleSearchAsync
{
    /// <summary>
    /// This example issues an asynchronous 
    /// simple search for assets, shows pagewise request of search results and prints the results to stdout.
    /// </summary>
    public class SimpleSearchAsync
    {

        private static async Task<string> Stringify(IList<JToken> pages, string rawSearchExpression)
        {
            int assetNo = 0;
            int pageNo = 0;
            // Page through the result:
            StringBuilder sb = new StringBuilder();
            foreach (JToken page in pages)
            {
                var results
                    = page
                        .SelectToken("..aa:asset")
                        .AsEnumerable<dynamic>();
                if (results.Any())
                {
                    sb.AppendLine(string.Format("Page#: {0}, search expression: '{1}'", ++pageNo, rawSearchExpression));
                    foreach (var item in results)
                    {
                        string id = item["base"].id.ToString();
                        string name = item["common"].name.ToString();

                        sb.AppendLine(string.Format("Asset#: {0}, id: {1}, name: '{2}'", ++assetNo, id, name));
                    }
                }
            }

            return sb.ToString();
        }

        private static async Task<IList<JToken>> SimpleSearchAsyncCore(HttpClient httpClient, string apiDomain, string serviceType, string realm, string rawSearchExpression)
        {
            IList<JToken> pages = new List<JToken>();
            
            httpClient.DefaultRequestHeaders.Remove("Accept");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/hal+json");

            /// Check, whether simple search is supported:
            Uri searchesResourceUrl = new Uri(string.Format("https://{0}/apis/{1};version=0;realm={2}/searches", apiDomain, serviceType, realm));
            HttpResponseMessage searchesResponse = await httpClient.GetAsync(searchesResourceUrl);
            HttpStatusCode searchesStatus = searchesResponse.StatusCode;
            if (HttpStatusCode.OK == searchesStatus)
            {
                string rawSearchesResult = await searchesResponse.Content.ReadAsStringAsync();
                JObject searchesResult = JObject.Parse(rawSearchesResult);
                JToken simpleSearchLinkObject = searchesResult.SelectToken("_links.search:simple-search");
                // Is simple search supported?
                if (null != simpleSearchLinkObject)
                {
                    /// Doing the simple search and write the results to stdout:
                    // Here, no URL-template library is used in favor to string surgery:
                    string urlUnTemplatedSearch = simpleSearchLinkObject["href"].ToString();
                    urlUnTemplatedSearch = urlUnTemplatedSearch.Substring(0, urlUnTemplatedSearch.LastIndexOf('=') + 1);
                    string searchExpression = HttpUtility.UrlEncode(rawSearchExpression);
                    Uri simpleSearchResultPageUrl = new Uri(urlUnTemplatedSearch + searchExpression);

                    // Page through the result:
                    do
                    {
                        HttpResponseMessage simpleSearchResultPage = await httpClient.GetAsync(simpleSearchResultPageUrl);

                        HttpStatusCode simpleSearchStatus = simpleSearchResultPage.StatusCode;
                        if (HttpStatusCode.OK == simpleSearchStatus)
                        {
                            string rawSearchResult = await simpleSearchResultPage.Content.ReadAsStringAsync();
                            JObject searchResult = JObject.Parse(rawSearchResult);
                            var embeddedResults = searchResult.Properties().FirstOrDefault(it => "_embedded".Equals(it.Name));

                            if (null != embeddedResults)
                            {
                                pages.Add(embeddedResults);
                            }
                            else
                            {
                                Console.WriteLine("No results found for search expression '{0}'.", rawSearchExpression);
                                return pages;
                            }

                            // If we have more results, follow the next link and get the next page:
                            dynamic linkToNextPage = searchResult.SelectToken("_links.next");
                            simpleSearchResultPageUrl
                                = null != linkToNextPage
                                    ? new Uri(linkToNextPage.href.ToString())
                                    : null;
                        }
                        else
                        {
                            Console.WriteLine("Search failed for search expression '{0}'.", rawSearchExpression);
                            return pages;
                        }
                    }
                    while (null != simpleSearchResultPageUrl);
                }
                else
                {
                    Console.WriteLine("Simple search not supported.");
                }
            }
            else
            {
                Console.WriteLine("Request failed with code {0}.", searchesStatus);
            }

            return pages;
        }

        public static async Task SimpleSearchAsyncImpl(string apiDomain, string serviceType, string realm, string username, string password, string searchExpression)
        {
            HttpClient httpClient;
            try
            {
                httpClient = await PlatformTools.PlatformToolsAsync.Prepare(apiDomain);
            }
            catch (Exception)
            {
                Console.WriteLine("Preparation failed");
                throw;
            }

            JObject authEndpointResponse;
            try
            {
                authEndpointResponse = await PlatformTools.PlatformToolsAsync.GetAuthEndpoint(httpClient, apiDomain);
            }
            catch (Exception)
            {
                Console.WriteLine("Requesting authorization endpoint failed");
                throw;
            }

            JObject identityProvidersResponse;
            try
            {
                identityProvidersResponse = await PlatformTools.PlatformToolsAsync.GetIdentityProviders(httpClient, authEndpointResponse);
            }
            catch (Exception)
            {
                Console.WriteLine("Requesting identity providers failed");
                throw;
            }

            try
            {
                httpClient = await PlatformTools.PlatformToolsAsync.Authorize(httpClient, identityProvidersResponse, apiDomain, username, password);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine("Authorization failed");
                throw;
            }

            IList<JToken> pages;
            try
            {
                pages = await SimpleSearchAsyncCore(httpClient, apiDomain, serviceType, realm, searchExpression);
            }
            catch (Exception)
            {
                Console.WriteLine("Simple search failed");
                throw;
            }
            string result = await Stringify(pages, searchExpression);
            Console.WriteLine(result);
            try
            {
                authEndpointResponse = await PlatformTools.PlatformToolsAsync.GetAuthEndpoint(httpClient, apiDomain);
            }
            catch (Exception)
            {
                Console.WriteLine("Requesting logout endpoint failed");
                throw;
            }

            JToken currentToken;
            try
            {
                currentToken = await PlatformTools.PlatformToolsAsync.GetCurrentToken(httpClient, authEndpointResponse, apiDomain);
            }
            catch (Exception)
            {
                Console.WriteLine("Requesting the current session token for logout failed");
                throw;
            }

            try
            {
                await PlatformTools.PlatformToolsAsync.RemoveToken(httpClient, currentToken);
            }
            catch (Exception)
            {
                Console.WriteLine("Deletion of the current session token for logout failed");
                throw;
            }
        }

        public static void Main(string[] args)
        {
            if (6 != args.Length || "'".Equals(args[5]) || !args[5].StartsWith("'") || !args[5].EndsWith("'"))
            {
                Console.WriteLine("Usage: {0} <apidomain> <servicetype> <realm> <username> <password> '<simplesearchexpression>'", System.Reflection.Assembly.GetEntryAssembly().ManifestModule.Name);
            }
            else
            {
                string apiDomain = args[0];
                string serviceType = args[1];
                string realm = args[2];
                string username = args[3];
                string password = args[4];
                string searchExpression = args[5].Trim('\'');

                try
                {
                    SimpleSearchAsyncImpl(apiDomain, serviceType, realm, username, password, searchExpression)
                        .GetAwaiter()
                        .OnCompleted(() =>
                        {
                            Console.Write("End");
                        });
                    Console.ReadLine();
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception);
                    Console.WriteLine("End");
                }
            }
        }
    }
}
