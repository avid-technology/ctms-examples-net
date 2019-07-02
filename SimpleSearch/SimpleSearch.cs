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
                string rawSearchExpression = args[4].Trim('\'');

                using (HttpClient httpClient = PlatformTools.PlatformTools.Authorize(apiDomain, httpBasicAuthString))
                {
                    bool successfullyAuthorized = null != httpClient;
                    if (successfullyAuthorized)
                    {
                        try
                        {
                            var registryServiceVersion = "0";
                            string defaultSimpleSearchUriTemplate = $"https://{apiDomain}/apis/{serviceType};version=0;realm={realm}/searches/simple?search={{search}}{{&offset,limit,sort}}";
                            string simpleSearchUriTemplate = PlatformTools.PlatformTools.FindInRegistry(httpClient, apiDomain, serviceType, registryServiceVersion, "search:simple-search", defaultSimpleSearchUriTemplate, realm);

                            UriTemplate simpleSearchUrlTemplate = new UriTemplate(simpleSearchUriTemplate);
                            simpleSearchUrlTemplate.SetParameter("search", rawSearchExpression);
                            Uri simpleSearchResultPageUrl = new Uri(simpleSearchUrlTemplate.Resolve());

                            httpClient.DefaultRequestHeaders.Remove("Accept");
                            httpClient.DefaultRequestHeaders.Add("Accept", "application/hal+json");

                            using (HttpResponseMessage searchesResponse = httpClient.GetAsync(simpleSearchResultPageUrl).Result)
                            {
                                /// Check, whether simple search is supported:
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
                                        using (HttpResponseMessage simpleSearchResultPage = httpClient.GetAsync(simpleSearchResultPageUrl).Result)
                                        {
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
                                                        sb.AppendLine($"Page#: {++pageNo}, search expression: '{rawSearchExpression}'");
                                                        foreach (var item in results)
                                                        {
                                                            string id = item["base"].id.ToString();
                                                            string name = item["common"].name?.ToString();

                                                            sb.AppendLine($"Asset#: {++assetNo}, id: {id}, name: '{name}'");
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    Console.WriteLine($"No results found for search expression '{rawSearchExpression}'.");
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
                                                Console.WriteLine($"Search failed for search expression '{rawSearchExpression}'.");
                                            }
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
                }

                Console.WriteLine("End");
            }
        }
    }
}
