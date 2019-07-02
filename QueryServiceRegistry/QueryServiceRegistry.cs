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
using System.Threading;
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
            try
            {
                if (2 != args.Length)
                {
                    Console.WriteLine($"Usage: {System.Reflection.Assembly.GetEntryAssembly().ManifestModule.Name} <apidomain> <httpbasicauthstring>");
                }
                else
                {
                    string apiDomain = args[0];
                    string httpBasicAuthString = args[1];

                    HttpClient httpClient = PlatformTools.PlatformTools.Authorize(apiDomain, httpBasicAuthString);

                    bool successfullyAuthorized = null != httpClient;
                    if (successfullyAuthorized)
                    {
                        try
                        {
                            try
                            {
                                const string registryServiceType = "avid.ctms.registry";

                                httpClient.DefaultRequestHeaders.Remove("Accept");
                                httpClient.DefaultRequestHeaders.Add("Accept", "application/hal+json");

                                /// Check, whether the service registry is available:
                                Uri serviceRootsResourceUrl = new Uri($"https://{apiDomain}/apis/{registryServiceType};version=0/serviceroots");
                                //Uri serviceRootsResourceUrl = new Uri($"https://{apiDomain}/apis/{registryServiceType};version=0/serviceroots?site-local={onlyRemotes}");
                                    
                                HttpResponseMessage serviceRootsResponse = httpClient.GetAsync(serviceRootsResourceUrl).Result;
                                HttpStatusCode serviceRootsStatus = serviceRootsResponse.StatusCode;
                                if (HttpStatusCode.OK == serviceRootsStatus)
                                {
                                    /// Doing the registry lookup and write the results to stdout:
                                    string rawServiceRootsResult = serviceRootsResponse.Content.ReadAsStringAsync().Result;
                                    JObject serviceRootsResult = JObject.Parse(rawServiceRootsResult);

                                    Console.WriteLine();
                                    Console.WriteLine($"next: {serviceRootsResourceUrl}");
                                    var systems = serviceRootsResult
                                                    .SelectToken("systems")
                                                    .Select((dynamic it) =>
                                                    {
                                                        return $"systemID = {it.systemID} \"{it.name}\"{(it.ContainsKey("host") ? $", host = {it.host}" : string.Empty)}{(it.ContainsKey("origin") ? $", origin = {it.origin}" : string.Empty)}";
                                                    })
                                                    .ToArray();
                                    Console.WriteLine(string.Join("\n", systems));

                                    var resourcesToken = serviceRootsResult.SelectToken("resources");
                                    if (null != resourcesToken)
                                    {
                                    StringBuilder sb = new StringBuilder();
                                    var resources = resourcesToken.Cast<JProperty>();
                                    foreach (var resource in resources)
                                    {
                                        sb.AppendLine($"Resource name: '{resource.Name}'");
                                        IEnumerable<string> serviceHrefs
                                            = (resource.Value is JArray)
                                                ? resource.Value.Select(it => (string)((dynamic)it).href.ToString())
                                                : new string[] { ((dynamic)resource.Value).href.ToString() };
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
                                    Console.WriteLine($"Problem accessing <{serviceRootsResourceUrl}> - {serviceRootsStatus:D} {serviceRootsResponse.ReasonPhrase}");
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Failure - {ex.Message}");
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
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}
