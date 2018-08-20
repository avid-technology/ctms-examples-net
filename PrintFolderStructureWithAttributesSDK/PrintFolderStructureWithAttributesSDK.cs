using Avid.Platform.SDK;
using Avid.Platform.SDK.Authorization;
using Avid.Platform.SDK.Hal;
using Avid.Platform.SDK.Model.Location;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

using HierarchicalItem = System.Tuple<Avid.Platform.SDK.Model.Location.Item, int>;

namespace PrintFolderStructureWithAttributesSDK
{
    public class PrintFolderStructureWithAttributesSDK
    {
        /// <summary>
        /// Traverses the structure of the folder tree (location structure) and collects the results in the passed list.
        /// </summary>
        /// <param name="platformClient">The PlatformClient against the platform.</param>
        /// <param name="rootItem">The Item to start traversal from.</param>
        /// <param name="results">!!Will be modified!! The list, in which the results of traversal will be collected.</param>
        /// <param name="depth">The depth of the traversal.</param>
        private static void Traverse(PlatformClient platformClient, Item rootItem, IList<HierarchicalItem> results, int depth)
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
                    collection = platformClient.GetHalResource<Collection>(collection.GetUri("next", Enumerable.Empty<EmbedResource>()));
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
                if ("folder".Equals(child.Base.Type))
                {
                    Traverse(platformClient, platformClient.GetHalResource<Item>(new Uri(child.Href)), results, depth + 1);
                }
            }

            foreach (Item child in materializedChildren)
            {
                if (!"folder".Equals(child.Base.Type))
                {
                    results.Add(Tuple.Create(child, depth + 1));
                }
            }
        }


        public static void Main(string[] args)
        {
            if (6 != args.Length)
            {
                Console.WriteLine("Usage: {0} <apidomain> <servicetype> <realm> <oauth2token> <username> <password>", System.Reflection.Assembly.GetEntryAssembly().ManifestModule.Name);
            }
            else
            {
                string apiDomain = args[0];
                string serviceType = args[1];
                string realm = args[2];
                string oauth2token = args[3];
                string username = args[4];
                string password = args[5];

                const string extraAttributes = "comment,RIGHTS_INDICATOR,piffpaff,escape\\,me";

                Uri upstreamServerUrl = new Uri($"https://{apiDomain}");

                using (CtmsRegistryClient registryClient = new CtmsRegistryClient(new OAuth2AuthorizationConnection(upstreamServerUrl, oauth2token, username, password)))
                {            
                    IList<HierarchicalItem> results = new List<HierarchicalItem>();
                    Stopwatch watch = new Stopwatch();
                    watch.Start();
                    /// Get the root folder:
                    // !!
                    // The MAM Connectivity Toolkit Connector does always embed all items of a folder.
                    // !!
                    Item rootItem = PlatformTools.PlatformToolsSDK.FindInRegistry<Item>(registryClient, serviceType, realm, "loc:root-item");

                    Uri newUri
                        = new Uri(rootItem.Href)
                            .ExtendQuery(new Dictionary<string, string> { { "attributes", extraAttributes } });

                    Item effectiveRootItem = new Item { Href = newUri.ToString() }; 
                    
                    try
                    {
                        /// Traverse the folder tree and collect the results in the passed list:
                        Traverse(registryClient, effectiveRootItem, results, 0);

                        StringBuilder sb = new StringBuilder();
                        foreach (HierarchicalItem item in results)
                        {
                            string text = string.Format("{0}{1} depth: {2} {3}"
                                            , new string('\t', item.Item2)
                                            , "folder".Equals(item.Item1.Base.Type) ? "- (folder) " : string.Empty
                                            , item.Item2
                                            , item.Item1.Common.Name);

                            text += string.Format(", Comment: \"{0}\", RightsIndicator: \"{1}\", piffpaff: \"{2}\", escape\\, me: \"{3}\""
                                                    , item.Item1.GetAttribute("comment")
                                                    , item.Item1.GetAttribute("RIGHTS_INDICATOR")
                                                    , item.Item1.GetAttribute("piffpaff")
                                                    , item.Item1.GetAttribute("escape\\,me"));

                            sb.AppendLine(text);
                        }
                        Console.WriteLine(sb);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                    finally
                    {
                        watch.Stop();
                        Console.WriteLine("elapsed: {0}", watch.ElapsedMilliseconds);
                    }
                }                 
            }
            Console.WriteLine("End");
        }
    }
}

