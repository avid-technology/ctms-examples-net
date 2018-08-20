//
// Copyright 2016 by Avid Technology, Inc.
//

using Avid.Platform.SDK;
using Avid.Platform.SDK.Authorization;
using Avid.Platform.SDK.Hal;
using Avid.Platform.SDK.Model.AssetAccess;
using Avid.Platform.SDK.Model.Common;
using Avid.Platform.SDK.Model.Searching;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using WebApi.Hal;

namespace AdvancedSearchSDK
{
    public class AdvancedSearchSDK
    {
        public static void Main(string[] args)
        {
            if (7 != args.Length)
            {
                Console.WriteLine($"Usage: {System.Reflection.Assembly.GetEntryAssembly().ManifestModule.Name} <apidomain> <servicetype> <realm> <oauth2token> <username> <password> <advancedsearchdescriptionfilename>");
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
                    Uri upstreamServerUrl = new Uri($"https://{apiDomain}");

                    using (CtmsRegistryClient registryClient = new CtmsRegistryClient(new OAuth2AuthorizationConnection(upstreamServerUrl, oauth2token, username, password)))
                    {
                        const string registeredLinkRelSearches = "search:searches";
                        Searches searchesResource = PlatformTools.PlatformToolsSDK.FindInRegistry<Searches>(registryClient, serviceType, realm, registeredLinkRelSearches);

                        if (null != searchesResource)
                        {
                            const string registeredLinkRelAdvancedSearch = "search:advanced-search";
                            Link advancedSearchLink = searchesResource.DiscoverLink(registeredLinkRelAdvancedSearch);
                            /// Check, whether simple search is supported:
                            if (null != advancedSearchLink)
                            {
                                UriTemplate advancedSearchUriTemplate = new UriTemplate(advancedSearchLink.Href);
                                advancedSearchUriTemplate.SetParameter("offset", 0);
                                advancedSearchUriTemplate.SetParameter("limit", 50);

                                string advancedSearchUri = advancedSearchUriTemplate.Resolve();

                                string advancedSearchDescription = File.ReadAllText(advancedSearchDescriptionFileName);

                                /// Doing the search and write the results to stdout:
                                // The search description must be passed as content type "application/json":
                                using (HttpContent content = new StringContent(advancedSearchDescription, Encoding.UTF8, "application/json"))
                                {
                                    using (HttpResponseMessage response = registryClient.HttpClient.PostAsync(advancedSearchUri, content).Result)
                                    {
                                        AdvancedSearch searchResult = response.Content.ReadAsAsyncHal<AdvancedSearch>().Result;

                                        int assetNo = 0;
                                        int pageNo = 0;
                                        // Page through the result:
                                        StringBuilder sb = new StringBuilder();
                                        do
                                        {
                                            if (searchResult.AssetList.Any())
                                            {
                                                sb.AppendLine($"Page#: {++pageNo}, search description from file '{advancedSearchDescriptionFileName}'");
                                                foreach (Asset asset in searchResult.AssetList)
                                                {
                                                    BaseInfo baseInfo = asset.Base;
                                                    CommonAttributes commonAttributes = asset.Common;

                                                    sb.AppendLine($"Asset#: {++assetNo}, id: {asset.Base.Id}, name: '{asset.Common.Name}'");
                                                }
                                            }

                                            // If we have more results, follow the next link and get the next page:
                                            searchResult = registryClient.GetHalResource<SimpleSearch>(searchResult.GetUri("next", Enumerable.Empty<EmbedResource>()));
                                        }
                                        while (searchResult != null);
                                        Console.WriteLine(sb);
                                    }
                                }
                            }
                            else
                            {
                                Console.WriteLine("Advanced search not supported.");
                            }
                        }
                        else
                        {
                            Console.WriteLine("Advanced search not supported.");
                        }

                        Console.WriteLine("End");
                    }
                }
            }
        }
    }
}
