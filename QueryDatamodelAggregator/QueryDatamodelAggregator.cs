using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using Tavis.UriTemplates;

namespace QueryDatamodelAggregator
{
    /// <summary>
    /// This example enumerates the aggregated datamodel information from the CTMS Datamodel Aggregator and writes the results to stdout.
    /// </summary>
    public class QueryDatamodelAggregator
    {
        public static void Main(string[] args)
        {
            if (4 != args.Length)
            {
                Console.WriteLine($"Usage: {System.Reflection.Assembly.GetEntryAssembly().ManifestModule.Name} <apidomain> <serviceversion> <username> <password>");
            }
            else
            {
                string apiDomain = args[0];
                string serviceVersion = args[1];
                string username = args[2];
                string password = args[3];

                HttpClient httpClient = PlatformTools.PlatformTools.Authorize(apiDomain, username, password);

                bool successfullyAuthorized = null != httpClient;
                if (successfullyAuthorized)
                {
                    try
                    {
                        const string dataModelAggregatorServiceType = "avid.ctms.datamodel.aggregator";

                        // Specify an IETF BCP 47 language tag, such as "en-US":
                        const string lang = "en"; // "" represents the default language, which is "en"

                        var registryServiceVersion = "0";
                        string defaultSimpleSearchUriTemplate = String.Format("https://{0}/apis/{1};version={2}/aggregateddatamodel{{?lang}}", apiDomain, dataModelAggregatorServiceType, 0);
                        List<String> aggregatedDataModelUriTemplates = PlatformTools.PlatformTools.FindInRegistry(httpClient, apiDomain, new List<string> { dataModelAggregatorServiceType }, registryServiceVersion, "search:simple-search", defaultSimpleSearchUriTemplate);

                        UriTemplate aggregatedDataModeUriTemplate = new UriTemplate(aggregatedDataModelUriTemplates[0]);
                        aggregatedDataModeUriTemplate.SetParameter("lang", lang);
                        Uri aggregatedDataModelResourceURL = new Uri(aggregatedDataModeUriTemplate.Resolve());


                        httpClient.DefaultRequestHeaders.Remove("Accept");
                        httpClient.DefaultRequestHeaders.Add("Accept", "application/hal+json");
                        
                        /// Check, whether the CTMS Datamodel Aggregator is available:
                       
                        HttpResponseMessage aggregatedDataModelResponse = httpClient.GetAsync(aggregatedDataModelResourceURL).Result;
                        HttpStatusCode aggregatedDataModelStatus = aggregatedDataModelResponse.StatusCode;
                        if (HttpStatusCode.OK == aggregatedDataModelStatus)
                        {
                            /// Doing the datamodel lookup and write the results to stdout:
                            string rawAggregatedDataModelResult = aggregatedDataModelResponse.Content.ReadAsStringAsync().Result;
                            JObject aggregatedDataModelResult = JObject.Parse(rawAggregatedDataModelResult);

                            var attributesToken = aggregatedDataModelResult.SelectToken("attributes");
                            if (null != attributesToken)
                            {
                                StringBuilder sb = new StringBuilder();
                               
                                var customAttributes = attributesToken.SelectTokens("custom").Children();
                                sb.AppendLine("custom attributes:");
                                int nCustomAttributes = 0;
                                foreach (var item in customAttributes)
                                {
                                    sb.AppendLine($"{++nCustomAttributes}").AppendLine($"{item}");
                                }

                                var commonAttributes = attributesToken.SelectTokens("common").Children();
                                sb.AppendLine("common attributes:");
                                int nCommonAttributes = 0;
                                foreach (var item in commonAttributes)
                                {
                                    sb.AppendLine($"{++nCommonAttributes}").AppendLine($"{item}");
                                }                                   
                                
                                Console.WriteLine(sb);
                            }
                            else
                            {
                                Console.WriteLine("No datamodel found.");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Problem accessing <{aggregatedDataModelResourceURL}>: {aggregatedDataModelStatus:D} {aggregatedDataModelResponse.ReasonPhrase}");
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
