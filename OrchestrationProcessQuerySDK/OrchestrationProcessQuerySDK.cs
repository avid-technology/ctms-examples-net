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

namespace OrchestrationProcessQuerySDK
{
    ///
    /// This example queries process instances, shows pagewise request of query results and prints the results to stdout.
    ///
    public class OrchestrationProcessQuerySDK
    {        
        public static void Main(string[] args)
        {
            if (5 != args.Length || "'".Equals(args[4]) || !args[4].StartsWith("'") || !args[4].EndsWith("'"))
            {
                Console.WriteLine("Usage: {0} <apidomain> <realm> <username> <password> '<simplesearchexpression>'", System.Reflection.Assembly.GetEntryAssembly().ManifestModule.Name);
            }
            else
            {
                string apiDomain = args[0];
                string realm = args[1];
                string username = args[2];
                string password = args[3];
                string rawSearchExpression = args[4].Trim('\'');

                Uri upstreamServerUrl = new Uri(string.Format("https://{0}", apiDomain));

                using (OrchestrationClient orchestrationClient = new OrchestrationClient(new MCUXAuthorizationConnection(upstreamServerUrl, username, password), realm))
                {
                    /// Create the process query:
                    string queryExpression = "<query version='1.0'><search><quick>"+rawSearchExpression+"</quick></search></query>";
                    JObject query = new JObject(new JProperty("query", queryExpression));
                    ProcessQuery result = orchestrationClient.CreateProcessQuery(query);

                    int assetNo = 0;
                    int pageNo = 0;
                    /// Page through the result:
                    StringBuilder sb = new StringBuilder();
                    do
                    {
                        sb.AppendLine(string.Format("Page#: {0}, search expression: '{1}'", ++pageNo, rawSearchExpression));
                        foreach (Process processInstance in result.ResourceList)
                        {
                            string id = processInstance.Base.Id;
                            string name = processInstance.Common.Name;

                            sb.AppendLine(string.Format("ProcessItem#: {0}, id: {1}, name: '{2}'", ++assetNo, id, name));
                        }

                        // If we have more results, follow the next link and get the next page:
                        result = orchestrationClient.GetHalResource<ProcessQuery>(result.GetUri("next", Enumerable.Empty<EmbedResource>()));
                    }
                    while (result != null);
                    Console.WriteLine(sb);
                }                                
                Console.WriteLine("End");
            }
        }
    }
}
