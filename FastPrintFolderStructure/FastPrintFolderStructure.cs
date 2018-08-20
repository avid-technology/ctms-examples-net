//
// Copyright 2016 by Avid Technology, Inc.
//

using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using PlatformTools;

using HierarchicalItem = System.Tuple<dynamic, int>;

namespace FastPrintFolderStructure
{
    /// <summary>
    /// This example traverses the structure of the folder tree (location structure) with embedded resources and prints the
    /// results to stdout.
    /// </summary>
    public class FastPrintFolderStructure
    {
        /// <summary>
        /// Traverses the structure of the folder tree (location structure) with embedded resources and collects the results
        /// in the passed list.
        /// </summary>
        /// <param name="httpClient">The HttpClient against the platform.</param>
        /// <param name="rootItemUrl">The URL to start traversal from.</param>
        /// <param name="results">!!Will be modified!! The list, in which the results of traversal will be collected.</param>
        /// <param name="depth">The depth of the traversal.</param>
        private static void Traverse(HttpClient httpClient, Uri rootItemUrl, IList<HierarchicalItem> results, int depth)
        {
            var children = Enumerable.Empty<dynamic>();
            Uri itemUrl = rootItemUrl;
            HttpResponseMessage itemResultPage = httpClient.GetAsync(itemUrl).Result;

            HttpStatusCode itemStatus = itemResultPage.StatusCode;
            if (HttpStatusCode.OK == itemStatus)
            {
                string rawItemPageResults = itemResultPage.Content.ReadAsStringAsync().Result;
                JObject itemResult = JObject.Parse(rawItemPageResults);
                results.Add(Tuple.Create((object)itemResult, depth));

                JToken collection = itemResult.SelectToken("_embedded.loc:collection");
                // The item is a folder:
                if (null != collection)
                {
                    // Get the items of the folder pagewise:
                    do
                    {
                        var items = collection.SelectToken("_embedded.loc:item");
                        if (null != items)
                        {
                            var itemPage
                                = (items is JArray)
                                    ? items.AsEnumerable<dynamic>()
                                    : new dynamic[] { items };

                            children = children.Concat(itemPage);
                        }

                        dynamic linkToNextPage = collection.SelectToken("_links.next");
                        if (null != linkToNextPage)
                        {
                            HttpResponseMessage itemResultNextPage = httpClient.GetAsync(linkToNextPage.href.ToString()).Result;

                            HttpStatusCode itemNextPageStatus = itemResultNextPage.StatusCode;
                            if (HttpStatusCode.OK == itemNextPageStatus)
                            {
                                string rawItemNextPageResults = itemResultNextPage.Content.ReadAsStringAsync().Result;
                                collection = JObject.Parse(rawItemNextPageResults);
                            }
                            else
                            {
                                collection = null;
                            }
                        }
                        else
                        {
                            collection = null;
                        }
                    } while (null != collection);
                }
                else
                {
                    // The item to traverse is no folder.
                }

                dynamic[] materializedChildren = children.ToArray();
                foreach (var child in materializedChildren)
                {
                    if (null != child._links["loc:collection"])
                    {
                        Traverse(httpClient, new Uri(child._links.self.href.Value), results, depth + 1);
                    }
                }

                foreach (var child in materializedChildren)
                {
                    if (null == child._links["loc:collection"])
                    {
                        results.Add(Tuple.Create((object)child, depth + 1));
                    }
                }
            }
            else
            {
                Console.WriteLine("Get item failed for item <{0}>.", itemUrl);
            }
        }

        public static void Main(string[] args)
        {
            int serviceVersion;
            if (7 != args.Length || !int.TryParse(args[2], out serviceVersion))
            {
                Console.WriteLine("Usage: {0} <apidomain> <servicetype> <serviceversion> <realm> <oauth2token> <username> <password>", System.Reflection.Assembly.GetEntryAssembly().ManifestModule.Name);
            }
            else
            {
                string apiDomain = args[0];
                string serviceType = args[1];                
                string realm = args[3];
                string oauth2token = args[4];
                string username = args[5];
                string password = args[6];
                HttpClient httpClient = PlatformTools.PlatformTools.Authorize(apiDomain, oauth2token, username, password);

                bool successfullyAuthorized = null != httpClient;
                if (successfullyAuthorized)
                {
                    try
                    {
                        var registryServiceVersion = "0";
                        string defaultLocationsUriTemplate = string.Format("https://{0}/apis/{1};version={2};realm={3}/locations", apiDomain, serviceType, 0, realm);
                        string locationsUriTemplate = PlatformTools.PlatformTools.FindInRegistry(httpClient, apiDomain, serviceType, registryServiceVersion, "loc:locations", defaultLocationsUriTemplate, realm);

                        Uri locationsURL = new Uri(locationsUriTemplate);

                        httpClient.DefaultRequestHeaders.Remove("Accept");
                        httpClient.DefaultRequestHeaders.Add("Accept", "application/hal+json");

                        /// Check presence of the locations resource and continue with HATEOAS:
                        HttpResponseMessage result = httpClient.GetAsync(locationsURL).Result;
                        if (result.IsSuccessStatusCode)
                        {
                            /// Get the root folder item:
                            string rawLocationsResults = result.Content.ReadAsStringAsync().Result;
                            dynamic locationsResults = JObject.Parse(rawLocationsResults);
                            string urlRootItem = locationsResults._links["loc:root-item"].href.ToString();


                            /// The root folder URL:
                            // !!
                            // The MAM Connectivity Toolkit Connector does always embed all direct items of a folder. For other
                            // service types, the query parameter embed=asset must be added if necessary.
                            // E.g. resulting in => https://$apiDomain/apis/$serviceType;version=0;realm=$realm/locations/folders?embed=asset
                            // !!
                            Uri rootItemUrl = new Uri(urlRootItem);

                            IList<HierarchicalItem> results = new List<HierarchicalItem>();
                            /// Traverse the folder tree and collect the results in the passed list:
                            Stopwatch watch = new Stopwatch();
                            watch.Start();
                            Traverse(httpClient, rootItemUrl, results, 0);
                            StringBuilder sb = new StringBuilder();
                            foreach (HierarchicalItem item in results)
                            {
                                string text = string.Format("{0}{1}depth: {2} {3}"
                                                , new string('\t', item.Item2)
                                                , null != item.Item1._links["loc:collection"] ? "- (collection) " : string.Empty
                                                , item.Item2
                                                , item.Item1["common"].name);
                                sb.AppendLine(text);
                            }
                            Console.WriteLine(sb);
                            watch.Stop();
                            Console.WriteLine("elapsed: {0}", watch.ElapsedMilliseconds);
                        }
                        else
                        {
                            Console.WriteLine("Resource {0} not found", locationsURL);
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
