//
// Copyright 2016 by Avid Technology, Inc.
//

using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace QueryServiceRegistry
{
    /// <summary>
    /// This example enumerates the entries from the CTMS Registry and writes the results to stdout.
    /// </summary>
    public class QueryServiceRegistry
    {
        public static void Main(string[] args)
        {
            if (4 != args.Length)
            {
                Console.WriteLine("Usage: {0} <apidomain> <oauth2token> <username> <password>", System.Reflection.Assembly.GetEntryAssembly().ManifestModule.Name);
            }
            else
            {
                string apiDomain = args[0];
                string oAuth2Token = args[1];
                string username = args[2];
                string password = args[3];

                HttpClient httpClient = PlatformTools.PlatformTools.Authorize(apiDomain, oAuth2Token, username, password);

                bool successfullyAuthorized = null != httpClient;
                if (successfullyAuthorized)
                {
                    try
                    {
                        const string registryServiceType = "avid.ctms.registry";

                        httpClient.DefaultRequestHeaders.Remove("Accept");
                        httpClient.DefaultRequestHeaders.Add("Accept", "application/hal+json");

                        /// Check, whether the service registry is available:
                        Uri serviceRootsResourceUrl = new Uri(string.Format("https://{0}/apis/{1};version=0/serviceroots", apiDomain, registryServiceType));

                        HttpResponseMessage serviceRootsResponse = httpClient.GetAsync(serviceRootsResourceUrl).Result;
                        HttpStatusCode serviceRootsStatus = serviceRootsResponse.StatusCode;
                        if (HttpStatusCode.OK == serviceRootsStatus)
                        {
                            /// Doing the registry lookup and write the results to stdout:
                            string rawServiceRootsResult = serviceRootsResponse.Content.ReadAsStringAsync().Result;
                            JObject serviceRootsResult = JObject.Parse(rawServiceRootsResult);

                            var resourcesToken = serviceRootsResult.SelectToken("resources");
                            if (null != resourcesToken) {
                                StringBuilder sb = new StringBuilder();
                                var resources = resourcesToken.Cast<JProperty>();
                                foreach (var resource in resources)
                                {
                                    sb.AppendLine($"Resource name: '{resource.Name}'");
                                    IEnumerable<string> serviceHrefs 
                                        = (resource.Value is JArray)
                                            ? resource.Value.Select(it => (string) ((dynamic)it).href.ToString())
                                            : new string[]{((dynamic)resource.Value).href.ToString()};
                                    int index = 0;
                                    foreach (string serviceHref in serviceHrefs)
                                    {
                                        ++index;
                                        sb.AppendLine($"\t {index}. <{serviceHref}>");
                                    }                                
                                }
                                Console.WriteLine(sb);
                            }
                            else
                            {
                                Console.WriteLine("No services registered.");
                            }                            
                        }
                        else
                        {
                            Console.WriteLine("Problem accessing <{0}>: {1:D} {2}", serviceRootsResourceUrl, serviceRootsStatus, serviceRootsResponse.ReasonPhrase);
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
