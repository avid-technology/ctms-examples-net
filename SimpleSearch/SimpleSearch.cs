//
// Copyright 2016 by Avid Technology, Inc.
//

using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Tavis.UriTemplates;

namespace SimpleSearch
{
    /// <summary>
    /// This example issues a simple search for assets, shows pagewise request of search results and prints the results to stdout.
    /// </summary>
    public class SimpleSearch
    {
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
                string rawSearchExpression = args[5].Trim('\'');

                HttpClient httpClient = PlatformTools.PlatformTools.Authorize(apiDomain, username, password);

                bool successfullyAuthorized = null != httpClient;
                if (successfullyAuthorized)
                {
                    try
                    {
                        var registryServiceVersion = "0";
                        string defaultSimpleSearchUriTemplate = String.Format("https://{0}/apis/{1};version={2};realm={3}/searches/simple?search={{search}}{{&offset,limit,sort}}", apiDomain, serviceType, 0, realm);
                        List<String> simpleSearchUriTemplates = PlatformTools.PlatformTools.FindInRegistry(httpClient, apiDomain, new List<string> { serviceType }, registryServiceVersion, "search:simple-search", defaultSimpleSearchUriTemplate);

                        UriTemplate simpleSearchUrlTemplate = new UriTemplate(simpleSearchUriTemplates[0]);
                        simpleSearchUrlTemplate.SetParameter("search", rawSearchExpression);
                        Uri simpleSearchResultPageUrl = new Uri(simpleSearchUrlTemplate.Resolve());

                        httpClient.DefaultRequestHeaders.Remove("Accept");
                        httpClient.DefaultRequestHeaders.Add("Accept", "application/hal+json");

                        /// Check, whether simple search is supported:
                        HttpResponseMessage searchesResponse = httpClient.GetAsync(simpleSearchResultPageUrl).Result;
                        HttpStatusCode searchesStatus = searchesResponse.StatusCode;
                        if (HttpStatusCode.OK == searchesStatus)
                        {
                            string rawSearchesResult = searchesResponse.Content.ReadAsStringAsync().Result;
                            JObject searchesResult = JObject.Parse(rawSearchesResult);
                            
                            int assetNo = 0;
                            int pageNo = 0;
                            // Page through the result:
                            StringBuilder sb = new StringBuilder();
                            do
                            {
                                HttpResponseMessage simpleSearchResultPage = httpClient.GetAsync(simpleSearchResultPageUrl).Result;

                                HttpStatusCode simpleSearchStatus = simpleSearchResultPage.StatusCode;
                                if (HttpStatusCode.OK == simpleSearchStatus)
                                {
                                    string rawSearchResult = simpleSearchResultPage.Content.ReadAsStringAsync().Result;
                                    JObject searchResult = JObject.Parse(rawSearchResult);

                                    var embeddedResults = searchResult.Properties().FirstOrDefault(it => "_embedded".Equals(it.Name));

                                    if (null != embeddedResults)
                                    {
                                        var results
                                            = embeddedResults
                                                .SelectToken("..aa:asset")
                                                .AsEnumerable<dynamic>();
                                        if (results.Any())
                                        {
                                            sb.AppendLine(string.Format("Page#: {0}, search expression: '{1}'", ++pageNo, rawSearchExpression));
                                            foreach (var item in results)
                                            {
                                                string id = item["base"].id.ToString();
                                                string name = item["common"].name?.ToString();

                                                sb.AppendLine(string.Format("Asset#: {0}, id: {1}, name: '{2}'", ++assetNo, id, name));
                                            }
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine("No results found for search expression '{0}'.", rawSearchExpression);
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
                                    simpleSearchResultPageUrl = null;
                                    Console.WriteLine("Search failed for search expression '{0}'.", rawSearchExpression);
                                }
                            }
                            while (null != simpleSearchResultPageUrl);
                            Console.WriteLine(sb);
                        }
                        else
                        {
                            Console.WriteLine("Simple search not supported.");
                        }
                        
                    }
                    finally
                    {
                        PlatformTools.PlatformTools.Logout(httpClient);
                    }
                }
                else
                {
                    Console.WriteLine("Authorization failed.");
                }

                Console.WriteLine("End");
            }
        }
    }
}
