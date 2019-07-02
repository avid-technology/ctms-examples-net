//
// Copyright 2016 by Avid Technology, Inc.
//

using Avid.Platform.SDK;
using Avid.Platform.SDK.Hal;
using Avid.Platform.SDK.Authorization;
using Avid.Platform.SDK.Model.Orchestration;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebApi.Hal;
using System.Net.Http;

namespace OrchestrationProcessQuerySDK
{
    ///
    /// This example queries process instances, shows pagewise request of query results and prints the results to stdout.
    ///
    public class OrchestrationProcessQuerySDK
    {        
        public static void Main(string[] args)
        {
            if (4 != args.Length || "'".Equals(args[3]) || !args[3].StartsWith("'") || !args[3].EndsWith("'"))
            {
                Console.WriteLine($"Usage: {System.Reflection.Assembly.GetEntryAssembly().ManifestModule.Name} <apidomain> <httpbasicauthstring> <realm> '<simplesearchexpression>'");
            }
            else
            {
                string apiDomain = args[0];
                string httpBasicAuthString = args[1];
                string realm = args[2];
                string rawSearchExpression = args[3].Trim('\'');

                Uri upstreamServerUrl = new Uri($"https://{apiDomain}");

                using (CtmsRegistryClient registryClient = new CtmsRegistryClient(new OAuth2AuthorizationConnection(upstreamServerUrl, httpBasicAuthString)))
                {
                    const string registeredLinkRelOrchestrationRoot = "orchestration:orchestration";
                    const string orchestrationServiceType = "avid.orchestration.ctc";
                    OrchestrationRoot orchestrationRootResource = PlatformTools.PlatformToolsSDK.FindInRegistry<OrchestrationRoot>(registryClient, orchestrationServiceType, realm, registeredLinkRelOrchestrationRoot);

                    if (null != orchestrationRootResource)
                    {
                        const string registeredLinkRelOrchestrationProcessQuery = "orchestration:process-query";
                        Link orchestrationProcessQueryLink = orchestrationRootResource.DiscoverLink(registeredLinkRelOrchestrationProcessQuery);
                        if (null != orchestrationProcessQueryLink)
                        {
                            UriTemplate orchestrationProcessQueryUriTemplate = new UriTemplate(orchestrationProcessQueryLink.Href);
                            orchestrationProcessQueryUriTemplate.SetParameter("offset", 0);
                            orchestrationProcessQueryUriTemplate.SetParameter("limit", 50);

                            string orchestrationProcessQueryUri = orchestrationProcessQueryUriTemplate.Resolve();

                            /// Create the process query:
                            string queryExpression = $"<query version='1.0'><search><quick>{rawSearchExpression}</quick></search></query>";
                            JObject query = new JObject(new JProperty("query", queryExpression));
                            ProcessQuery result = registryClient.SendHal<ProcessQuery>(HttpMethod.Post, new Uri(orchestrationProcessQueryUri), query);

                            int assetNo = 0;
                            int pageNo = 0;
                            /// Page through the result:
                            StringBuilder sb = new StringBuilder();
                            do
                            {
                                sb.AppendLine($"Page#: {++pageNo}, search expression: '{rawSearchExpression}'");
                                foreach (Process processInstance in result.ResourceList)
                                {
                                    string id = processInstance.Base.Id;
                                    string name = processInstance.Common.Name;

                                    sb.AppendLine($"ProcessItem#: {++assetNo}, id: {id}, name: '{name}'");
                                }

                                // If we have more results, follow the next link and get the next page:
                                result = registryClient.GetHalResource<ProcessQuery>(result.GetUri("next", Enumerable.Empty<EmbedResource>()));
                            }
                            while (result != null);
                            Console.WriteLine(sb);  
                        }
                        else
                        {
                            Console.WriteLine("ProcessQuery not supported.");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Orchestration not supported.");
                    }
                }                                
                Console.WriteLine("End");
            }
        }
    }
}
