//
// Copyright 2016 by Avid Technology, Inc.
//

using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tavis.UriTemplates;

namespace AdvancedSearch
{
    /// <summary>
    /// This example issues an advanced search for assets, shows pagewise request of search results and prints the results to stdout.
    /// </summary>
    public class AdvancedSearch
    {
        public static void Main(string[] args)
        {
            if (5 != args.Length)
            {
                Console.WriteLine($"Usage: {System.Reflection.Assembly.GetEntryAssembly().ManifestModule.Name} <apidomain> <httpbasicauthstring> <servicetype> <realm> <advancedsearchdescriptionfilename>");
            }
            else
            {
                string apiDomain = args[0];
                string httpBasicAuthString = args[1];
                string serviceType = args[2];
                string realm = args[3];               
                string advancedSearchDescriptionFileName = args[4];

                if (File.Exists(advancedSearchDescriptionFileName))
                {
                    HttpClient httpClient = PlatformTools.PlatformTools.Authorize(apiDomain, httpBasicAuthString);

                    bool successfullyAuthorized = null != httpClient;
                    if (successfullyAuthorized)
                    {
                        try
                        {
                            string registryServiceVersion = "0";
                            string defaultAdvancedSearchUriTemplate = $"https://{apiDomain}/apis/{serviceType};version=0;realm={realm}/searches";
                            string advancedSearchUriTemplate = PlatformTools.PlatformTools.FindInRegistry(httpClient, apiDomain, serviceType, registryServiceVersion, "search:searches", defaultAdvancedSearchUriTemplate, realm);

                            UriTemplate advancedSearchUrlTemplate = new UriTemplate(advancedSearchUriTemplate);
                            Uri advancedSearchResultPageUrl = new Uri(advancedSearchUrlTemplate.Resolve());


                            httpClient.DefaultRequestHeaders.Remove("Accept");
                            httpClient.DefaultRequestHeaders.Add("Accept", "application/hal+json");

                            /// Check, whether advanced search is supported:
                            HttpResponseMessage searchesResponse = httpClient.GetAsync(advancedSearchResultPageUrl).Result;
                            HttpStatusCode searchesStatus = searchesResponse.StatusCode;
                            if (HttpStatusCode.OK == searchesStatus)
                            {
                                string rawSearchesResult = searchesResponse.Content.ReadAsStringAsync().Result;
                                JObject searchesResult = JObject.Parse(rawSearchesResult);
                                JToken advancedSearchLinkObject = searchesResult.SelectToken("_links.search:advanced-search");
                                // Is advanced search supported?
                                if (null != advancedSearchLinkObject)
                                {
                                    /// Doing the search and write the results to stdout:
                                    // Here, no URL-template library is used in favor to string surgery:
                                    string urlAdvancedSearch = advancedSearchLinkObject["href"].ToString();
                                    string urlUnTemplatedAdvancedSearch = urlAdvancedSearch.Substring(0, urlAdvancedSearch.LastIndexOf('{'));
                                    // Create and send the advanced searches description:
                                    string advancedSearchDescription = File.ReadAllText(advancedSearchDescriptionFileName);
                                    HttpContent content = new StringContent(advancedSearchDescription);
                                    content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, urlUnTemplatedAdvancedSearch) { Content = content };
                                    request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/hal+json"));
                                    HttpResponseMessage response = httpClient.SendAsync(request).Result;
                                    string rawAdvancedSearchPageResult = response.Content.ReadAsStringAsync().Result;
                                    dynamic advancedSearchResult = JObject.Parse(rawAdvancedSearchPageResult);

                                    int assetNo = 0;
                                    int pageNo = 0;
                                    // Page through the result:
                                    StringBuilder sb = new StringBuilder();
                                    sb.AppendLine(DateTime.Now.ToString());
                                    do
                                    {
                                        if (null != advancedSearchResult._embedded)
                                        {
                                            IEnumerable<dynamic> foundAssets = ((IEnumerable<dynamic>)advancedSearchResult._embedded["aa:asset"]);

                                            sb.AppendLine($"Page#: {++pageNo}, search description from file '{advancedSearchDescriptionFileName}'");
                                            foreach (dynamic asset in foundAssets)
                                            {
                                                string id = asset["base"].id.ToString();
                                                string name = asset["common"].name?.ToString() ?? string.Empty;

                                                sb.AppendLine($"Asset#: {++assetNo}, id: {id}, name: '{name}'");
                                            }

                                            // If we have more results, follow the next link and get the next page:
                                            dynamic linkToNextPage = advancedSearchResult.SelectToken("_links.next");
                                            if (null != linkToNextPage)
                                            {
                                                rawAdvancedSearchPageResult = httpClient.GetStringAsync(linkToNextPage.href.ToString()).Result;
                                                advancedSearchResult = JObject.Parse(rawAdvancedSearchPageResult);
                                            }
                                            else
                                            {
                                                advancedSearchResult = null;
                                            }
                                        }
                                    }
                                    while (null != advancedSearchResult);
                                    Console.WriteLine(sb);
                                }
                                else
                                {
                                    Console.WriteLine("Advanced search not supported.");
                                }
                            }
                            else
                            {
                                Console.WriteLine($"Resource {advancedSearchResultPageUrl} not found - {searchesResponse.ReasonPhrase}");
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
                else 
                {
                    Console.WriteLine($"File '{advancedSearchDescriptionFileName}' not found.");
                }

                Console.WriteLine("End");
            }
        }
    }
}