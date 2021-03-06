﻿//
// Copyright 2016 by Avid Technology, Inc.
//

using Avid.Platform.SDK;
using Avid.Platform.SDK.Authorization;
using Avid.Platform.SDK.Hal;
using Avid.Platform.SDK.Model.AssetAccess;
using Avid.Platform.SDK.Model.Common;
using Avid.Platform.SDK.Model.Searching;
using System;
using System.Linq;
using System.Text;
using WebApi.Hal;

namespace SimpleSearchSDK
{
    /// <summary>
    /// This example issues a simple search for assets, shows pagewise request of search results using the SDK and prints the results to stdout.
    /// </summary>
    public class SimpleSearchSDK
    {
        public static void Main(string[] args)
        {
            if (5 != args.Length || "'".Equals(args[4]) || !args[4].StartsWith("'") || !args[4].EndsWith("'"))
            {
                Console.WriteLine($"Usage: {System.Reflection.Assembly.GetEntryAssembly().ManifestModule.Name} <apidomain> <httpBasicAuthString> <servicetype> <realm> '<simplesearchexpression>'");
            }
            else
            {             
                string apiDomain = args[0];
                string httpBasicAuthString = args[1];
                string serviceType = args[2];
                string realm = args[3];               
                string rawSearchExpression = args[4].Trim('\'');

                Uri upstreamServerUrl = new Uri($"https://{apiDomain}");

                using (CtmsRegistryClient registryClient = new CtmsRegistryClient(new OAuth2AuthorizationConnection(upstreamServerUrl, httpBasicAuthString)))
                {
                    const string registeredLinkRelSearches = "search:searches";
                    Searches searchesResource = PlatformTools.PlatformToolsSDK.FindInRegistry<Searches>(registryClient, serviceType, realm, registeredLinkRelSearches);

                    if (null != searchesResource)
                    {
                        const string registeredLinkRelSimpleSearch = "search:simple-search";
                        Link simpleSearchLink = searchesResource.DiscoverLink(registeredLinkRelSimpleSearch);
                        /// Check, whether simple search is supported:
                        if (null != simpleSearchLink)
                        {

                            UriTemplate simpleSearchUrlTemplate = new UriTemplate(simpleSearchLink.Href);
                            simpleSearchUrlTemplate.SetParameter("search", rawSearchExpression);
                            Uri simpleSearchResultPageUrl = new Uri(simpleSearchUrlTemplate.Resolve());

                            /// Doing the search and write the results to stdout:
                            SimpleSearch searchResult = registryClient.GetHalResource<SimpleSearch>(simpleSearchResultPageUrl);

                            int assetNo = 0;
                            int pageNo = 0;
                            // Page through the result:
                            StringBuilder sb = new StringBuilder();
                            do
                            {
                                if (searchResult.AssetList.Any())
                                {
                                    sb.AppendLine($"Page#: {++pageNo}, search expression: '{rawSearchExpression}'");
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

                Console.WriteLine("End");
            }            
        }
    }
}
