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
                HttpClient httpClient = PlatformTools.PlatformTools.Authorize(apiDomain, httpBasicAuthString);

                bool successfullyAuthorized = null != httpClient;
                if (successfullyAuthorized)
                {
                    try
                    {                        
                        const string orchestrationServiceType = "avid.orchestration.ctc";
                        
                        var registryServiceVersion = "0";
                        string defaultProcessQueryUriTemplate = $"https://{apiDomain}/apis/{orchestrationServiceType};version=0;realm={realm}/process-queries/{{id}}{{?offset,limit,sort}}";
                        string processQueryUriTemplate = PlatformTools.PlatformTools.FindInRegistry(httpClient, apiDomain, orchestrationServiceType, registryServiceVersion, "orchestration:process-query", defaultProcessQueryUriTemplate, realm);

                        /// Doing the process query and write the results to stdout:
                        UriTemplate processQueryUrlTemplate = new UriTemplate(processQueryUriTemplate);
                        Uri processQueryURL = new Uri(processQueryUrlTemplate.Resolve());

                        httpClient.DefaultRequestHeaders.Remove("Accept");
                        httpClient.DefaultRequestHeaders.Add("Accept", "application/hal+json");
                        
                        /// Get the orchestration resource to query process instances:
                        string queryExpression = $"<query version='1.0'><search><quick>{rawSearchExpression}</quick></search></query>";
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
                                    sb.AppendLine($"Page#: {++pageNo}, search expression: '{rawSearchExpression}'");
                                    foreach (dynamic processInstance in foundProcessInstances)
                                    {
                                        string id = processInstance["base"].id.ToString();
                                        string name = null != processInstance["common"].name ? processInstance["common"].name.ToString() : string.Empty;

                                        sb.AppendLine($"ProcessItem#: {++assetNo}, id: {id}, name: '{name}'");
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
                            sb.AppendFormat($"Querying processes failed with '{response.ReasonPhrase}'.");
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
