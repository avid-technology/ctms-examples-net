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
            if (7 != args.Length)
            {
                Console.WriteLine("Usage: {0} <apidomain> <servicetype> <realm> <oauth2token> <username> <password> <advancedsearchdescriptionfilename>", System.Reflection.Assembly.GetEntryAssembly().ManifestModule.Name);
            }
            else
            {
                string apiDomain = args[0];
                string serviceType = args[1];
                string realm = args[2];
                string oauth2token = args[3];
                string username = args[4];
                string password = args[5];
                string advancedSearchDescriptionFileName = args[6];

                if (File.Exists(advancedSearchDescriptionFileName))
                {
                    HttpClient httpClient = PlatformTools.PlatformTools.Authorize(apiDomain, oauth2token, username, password);

                    bool successfullyAuthorized = null != httpClient;
                    if (successfullyAuthorized)
                    {
                        try
                        {
                            var registryServiceVersion = "0";
                            string defaultAdvancedSearchUriTemplate = string.Format("https://{0}/apis/{1};version={2};realm={3}/searches", apiDomain, serviceType, 0, realm);
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
                                    do
                                    {
                                        if (null != advancedSearchResult._embedded)
                                        {
                                            IEnumerable<dynamic> foundAssets = ((IEnumerable<dynamic>)advancedSearchResult._embedded["aa:asset"]);

                                            sb.AppendLine(string.Format("Page#: {0}, search description from file '{1}'", ++pageNo, advancedSearchDescriptionFileName));
                                            foreach (dynamic asset in foundAssets)
                                            {
                                                string id = asset["base"].id.ToString();
                                                string name = null != asset["common"].name ? asset["common"].name.ToString() : string.Empty;

                                                sb.AppendLine(string.Format("Asset#: {0}, id: {1}, name: '{2}'", ++assetNo, id, name));
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
                                Console.WriteLine("Failure accessing service.");
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
                    Console.WriteLine("File '{0}' not found.", advancedSearchDescriptionFileName);
                }

                Console.WriteLine("End");
            }
        }
    }
}