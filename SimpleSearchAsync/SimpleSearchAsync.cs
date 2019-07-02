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
using Tavis.UriTemplates;

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
                    sb.AppendLine($"Page#: {++pageNo}, search expression: '{rawSearchExpression}'");
                    foreach (var item in results)
                    {
                        string id = item["base"].id.ToString();
                        string name = item["common"].name.ToString();

                        sb.AppendLine($"Asset#: {++assetNo}, id: {id}, name: '{name}'");
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
            var registryServiceVersion = "0";
            string defaultSimpleSearchUriTemplate = $"https://{apiDomain}/apis/{serviceType};version=0;realm={realm}/searches/simple?search={{search}}{{&offset,limit,sort}}";
            string simpleSearchUriTemplate = await PlatformTools.PlatformToolsAsync.FindInRegistry(httpClient, apiDomain, serviceType, registryServiceVersion, "search:simple-search", defaultSimpleSearchUriTemplate, realm);

            UriTemplate simpleSearchUrlTemplate = new UriTemplate(simpleSearchUriTemplate);
            simpleSearchUrlTemplate.SetParameter("search", rawSearchExpression);
            Uri simpleSearchResultPageUrl = new Uri(simpleSearchUrlTemplate.Resolve());

            httpClient.DefaultRequestHeaders.Remove("Accept");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/hal+json");

            // Page through the result:
            do
            {
                using (HttpResponseMessage simpleSearchResultPage = await httpClient.GetAsync(simpleSearchResultPageUrl))
                {
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
                            Console.WriteLine($"No results found for search expression '{rawSearchExpression}'.");
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
                        Console.WriteLine($"Search failed for search expression '{rawSearchExpression}'.");
                        return pages;
                    }
                }
            }
            while (null != simpleSearchResultPageUrl);

            return pages;
        }

        public static async Task SimpleSearchAsyncImpl(string apiDomain, string serviceType, string realm, string httpBasicAuthString, string searchExpression)
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
                httpClient = await PlatformTools.PlatformToolsAsync.Authorize(httpClient, identityProvidersResponse, apiDomain, httpBasicAuthString);
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
            if (5 != args.Length || "'".Equals(args[4]) || !args[4].StartsWith("'") || !args[4].EndsWith("'"))
            {
                Console.WriteLine($"Usage: {System.Reflection.Assembly.GetEntryAssembly().ManifestModule.Name} <apidomain> <httpbasicauthstring> <servicetype> <realm> '<simplesearchexpression>'");
            }
            else
            {
                string apiDomain = args[0];
                string httpBasicAuthString = args[1];
                string serviceType = args[2];
                string realm = args[3];
                string searchExpression = args[4].Trim('\'');

                try
                {
                    SimpleSearchAsyncImpl(apiDomain, serviceType, realm, httpBasicAuthString, searchExpression)
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
