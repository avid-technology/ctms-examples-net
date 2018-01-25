﻿//
// Copyright 2016 by Avid Technology, Inc.
//

using Avid.Platform.SDK;
using Avid.Platform.SDK.Hal;
using Avid.Platform.SDK.Model.AssetAccess;
using Avid.Platform.SDK.Model.Common;
using Avid.Platform.SDK.Model.Searching;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdvancedSearchSDK
{
    public class AdvancedSearchSDK
    {
        public static void Main(string[] args)
        {
            if (6 != args.Length)
            {
                Console.WriteLine("Usage: {0} <apidomain> <servicetype> <realm> <username> <password> <advancedsearchdescriptionfilename>", System.Reflection.Assembly.GetEntryAssembly().ManifestModule.Name);
            }
            else
            {                
                string apiDomain = args[0];
                string serviceType = args[1];
                string realm = args[2];
                string username = args[3];
                string password = args[4];
                string advancedSearchDescriptionFileName = args[5];

                if (File.Exists(advancedSearchDescriptionFileName))
                {
                    Uri upstreamServerUrl = new Uri(string.Format("https://{0}", apiDomain));
                    AssetAccessClient assetAccessClient
                        = PlatformTools.PlatformToolsSDK.CreateAssetAccessClient(upstreamServerUrl, serviceType
                            , realm
                            , username
                            , password);

                    if (null != assetAccessClient)
                    {
                        using (assetAccessClient)
                        {
                            /// Check, whether simple search is supported:
                            if (assetAccessClient.SupportsAdvancedSearch())
                            {
                                string advancedSearchDescription = File.ReadAllText(advancedSearchDescriptionFileName);

                                /// Doing the search and write the results to stdout:
                                AdvancedSearch searchResult = assetAccessClient.AdvancedSearch(advancedSearchDescription);

                                int assetNo = 0;
                                int pageNo = 0;
                                // Page through the result:
                                StringBuilder sb = new StringBuilder();
                                do
                                {
                                    if (searchResult.AssetList.Any())
                                    {
                                        sb.AppendLine(string.Format("Page#: {0}, search description from file '{1}'", ++pageNo, advancedSearchDescriptionFileName));
                                        foreach (Asset asset in searchResult.AssetList)
                                        {
                                            BaseInfo baseInfo = asset.Base;
                                            CommonAttributes commonAttributes = asset.Common;

                                            sb.AppendLine(string.Format("Asset#: {0}, id: {1}, name: '{2}'", ++assetNo, asset.Base.Id, asset.Common.Name));
                                        }
                                    }

                                    // If we have more results, follow the next link and get the next page:
                                    searchResult = assetAccessClient.GetHalResource<SimpleSearch>(searchResult.GetUri("next", Enumerable.Empty<EmbedResource>()));
                                }
                                while (searchResult != null);
                                Console.WriteLine(sb);
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("Couldn't create AssetAccessClient for serviceType: '{0}', realm: '{1}', username: '{2}', upstreamServerUrl: <{3}>"
                            , serviceType, realm, username, upstreamServerUrl);
                    }
                    Console.WriteLine("End");
                }
            }
        }
    }
}
