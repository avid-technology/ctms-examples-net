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
using System.Threading;
using System.Web;
using Tavis.UriTemplates;

namespace OrchestrationProcessQuery
{
    ///
    /// This example queries process instances, shows pagewise request of query results and prints the results to stdout.
    ///
    public class OrchestrationProcessQuery
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

                HttpClient httpClient = PlatformTools.PlatformTools.Authorize(apiDomain, username, password);

                bool successfullyAuthorized = null != httpClient;
                if (successfullyAuthorized)
                {
                    try
                    {
                        
                        const string orchestrationServiceType = "avid.orchestration.ctc";
                        
                        var registryServiceVersion = "0";
                        string defaultProcessQueryUriTemplate = String.Format("https://{0}/apis/{1};version={2};realm={3}/process-queries/{{id}}{{?offset,limit,sort}}", apiDomain, orchestrationServiceType, 0, realm);
                        List<String> processQueryUriTemplates = PlatformTools.PlatformTools.FindInRegistry(httpClient, apiDomain, new List<string> { orchestrationServiceType }, registryServiceVersion, "orchestration:process-query", defaultProcessQueryUriTemplate);

                        /// Doing the process query and write the results to stdout:
                        UriTemplate processQueryUriTemplate = new UriTemplate(processQueryUriTemplates[0]);
                        Uri processQueryURL = new Uri(processQueryUriTemplate.Resolve());

                        httpClient.DefaultRequestHeaders.Remove("Accept");
                        httpClient.DefaultRequestHeaders.Add("Accept", "application/hal+json");
                        
                        /// Get the orchestration resource to query process instances:
                        string queryExpression = "<query version='1.0'><search><quick>"+rawSearchExpression+"</quick></search></query>";
                        JObject query = new JObject(new JProperty("query", queryExpression));
                    
                        /// Check presence of the orchestration resource and continue with HATEOAS:
                        HttpResponseMessage response = httpClient.PostAsJsonAsync(processQueryURL, query).Result;
                        StringBuilder sb = new StringBuilder();
                        if (response.IsSuccessStatusCode)
                        {
                            string rawProcessQueryPageResult = response.Content.ReadAsStringAsync().Result;
                            dynamic processQueryResult = JObject.Parse(rawProcessQueryPageResult);

                            int assetNo = 0;
                            int pageNo = 0;
                            // Page through the result:
                            do
                            {
                                IEnumerable<dynamic> foundProcessInstances = ((IEnumerable<dynamic>)processQueryResult._embedded["orchestration:process"]);
                                if (foundProcessInstances.Any())
                                {
                                    sb.AppendLine(string.Format("Page#: {0}, search expression: '{1}'", ++pageNo, rawSearchExpression));
                                    foreach (dynamic processInstance in foundProcessInstances)
                                    {
                                        string id = processInstance["base"].id.ToString();
                                        string name = null != processInstance["common"].name ? processInstance["common"].name.ToString() : string.Empty;

                                        sb.AppendLine(string.Format("ProcessItem#: {0}, id: {1}, name: '{2}'", ++assetNo, id, name));
                                    }
                                }

                                // If we have more results, follow the next link and get the next page:
                                dynamic linkToNextPage = processQueryResult._links.next;
                                if (null != linkToNextPage)
                                {
                                    rawProcessQueryPageResult = httpClient.GetStringAsync(linkToNextPage.href.ToString()).Result;
                                    processQueryResult = JObject.Parse(rawProcessQueryPageResult);
                                }
                                else
                                {
                                    processQueryResult = null;
                                }
                            } while (null != processQueryResult); 
                        }
                        else
                        {
                            sb.AppendFormat("Querying processes failed with '{0}'.", response.ReasonPhrase);
                        }
                        Console.WriteLine(sb);
                     
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
