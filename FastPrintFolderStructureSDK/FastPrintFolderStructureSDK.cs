//
// Copyright 2016 by Avid Technology, Inc.
//

using Avid.Platform.SDK;
using Avid.Platform.SDK.Model.Location;
using Avid.Platform.SDK.Hal;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

using HierarchicalItem = System.Tuple<Avid.Platform.SDK.Model.Location.Item, int>;

namespace FastPrintFolderStructureSDK
{
    /// <summary>
    /// This example traverses the structure of the folder tree (location structure) with embedded resources and prints the
    /// results to stdout.
    /// </summary>
    public class FastPrintFolderStructureSDK
    {
        /// <summary>
        /// Traverses the structure of the folder tree (location structure) and collects the results in the passed list.
        /// </summary>
        /// <param name="assetAccessClient">The AssetAccessClient against the platform.</param>
        /// <param name="rootItem">The Item to start traversal from.</param>
        /// <param name="results">!!Will be modified!! The list, in which the results of traversal will be collected.</param>
        /// <param name="depth">The depth of the traversal.</param>
        private static void Traverse(AssetAccessClient assetAccessClient, Item rootItem, IList<HierarchicalItem> results, int depth)
        {
            results.Add(Tuple.Create(rootItem, depth));

            var children = Enumerable.Empty<Item>();
           
            Collection collection = rootItem.Collection != null && rootItem.Collection.Links != null ? rootItem.Collection : null;
            // The item is a folder:            
            if (null != collection)
            {
                // Get the items of the folder pagewise:
                do
                {
                    children = children.Concat(collection.Items);
                    collection = assetAccessClient.GetHalResource<Collection>(collection.GetUri("next", Enumerable.Empty<EmbedResource>()));
                }
                while (null != collection);
            }
            else
            {
                // The item to traverse is no folder.
            }
            

            Item[] materializedChildren = children.ToArray();
            foreach (Item child in materializedChildren)
            {
                if (child.DiscoverLinks("loc:collection").Any())
                { 
                    Traverse(assetAccessClient, assetAccessClient.GetItem(new Uri(child.Href)), results, depth + 1); 
                }
            }

            foreach (Item child in materializedChildren)
            {
                if (!child.DiscoverLinks("loc:collection").Any())
                {
                    results.Add(Tuple.Create(child, depth + 1));
                }
            }
        }


        public static void Main(string[] args)
        {
            int serviceVersion;
            if (6 != args.Length || !int.TryParse(args[2], out serviceVersion))
            {
                Console.WriteLine("Usage: {0} <apidomain> <servicetype> <serviceversion> <realm> <username> <password>", System.Reflection.Assembly.GetEntryAssembly().ManifestModule.Name);
            }
            else
            {
                string apiDomain = args[0];
                string serviceType = args[1];
                string realm = args[3];
                string username = args[4];
                string password = args[5];

                Uri upstreamServerUrl = new Uri(string.Format("https://{0}", apiDomain));
                AssetAccessClient assetAccessClient
                    = PlatformTools.PlatformToolsSDK.CreateAssetAccessClient(upstreamServerUrl
                        , serviceType
                        , serviceVersion
                        , realm
                        , username
                        , password
);

                if (null != assetAccessClient)
                {
                    using (assetAccessClient)
                    {
                        IList<HierarchicalItem> results = new List<HierarchicalItem>();
                        Stopwatch watch = new Stopwatch();
                        watch.Start();
                        /// Get the root folder:
                        // !!
                        // The MAM Connectivity Toolkit Connector does always embed all items of a folder.
                        // !!
                        Item rootItem = assetAccessClient.GetRootItem();

                        /// Traverse the folder tree and collect the results in the passed list:
                        Traverse(assetAccessClient, rootItem, results, 0);
                        StringBuilder sb = new StringBuilder();
                        foreach (HierarchicalItem item in results)
                        {
                            string text = string.Format("{0}{1}depth: {2} {3}"
                                           , new string('\t', item.Item2)
                                           , item.Item1.DiscoverLinks("loc:collection").Any() ? "- (collection) " : string.Empty
                                           , item.Item2
                                           , item.Item1.Common.Name);
                            sb.AppendLine(text);
                        }
                        Console.WriteLine(sb);
                        watch.Stop();
                        Console.WriteLine("elapsed: {0}", watch.ElapsedMilliseconds);
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
