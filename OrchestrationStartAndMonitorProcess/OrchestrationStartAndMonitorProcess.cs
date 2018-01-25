//
// Copyright 2016 by Avid Technology, Inc.
//

using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using Tavis.UriTemplates;

namespace OrchestrationStartAndMonitorProcess
{
    /// <summary>
    /// This example starts a process and monitors its progress.
    /// </summary>
    public class OrchestrationStartAndMonitorProcess
    {
        public static void Main(string[] args)
        {
            if (4 != args.Length)
            {
                Console.WriteLine("Usage: {0} <apidomain> <realm> <username> <password>", System.Reflection.Assembly.GetEntryAssembly().ManifestModule.Name);
            }
            else
            {
                string apiDomain = args[0];
                string realm = args[1];
                string username = args[2];
                string password = args[3];

                HttpClient httpClient = PlatformTools.PlatformTools.Authorize(apiDomain, username, password);

                bool successfullyAuthorized = null != httpClient;
                if (successfullyAuthorized)
                {
                    try
                    {
                        const string orchestrationServiceType = "avid.orchestration.ctc";

                        var registryServiceVersion = "0";
                        string defaultProcessUriTemplate = String.Format("https://{0}/apis/{1};version={2};realm={3}/processes/{{id}}", apiDomain, orchestrationServiceType, 0, realm);
                        List<String> processUriTemplates = PlatformTools.PlatformTools.FindInRegistry(httpClient, apiDomain, new List<string> { orchestrationServiceType }, registryServiceVersion, "orchestration:process", defaultProcessUriTemplate);

                        UriTemplate startProcessTemplate = new UriTemplate(processUriTemplates[0]);
                        Uri startProcessURL = new Uri(startProcessTemplate.Resolve());


                        httpClient.DefaultRequestHeaders.Remove("Accept");
                        httpClient.DefaultRequestHeaders.Add("Accept", "application/hal+json");

                        /// Check presence of the orchestration resource and continue with HATEOAS:
                        //string urlOrchestration = string.Format("https://{0}/apis/{1};version=0;realm={2}", apiDomain, orchestrationServiceType, realm);
                        HttpResponseMessage result = httpClient.GetAsync(startProcessURL).Result;
                        if (result.IsSuccessStatusCode)
                        {
                            /// Get the orchestration resources to query process instances:
                            string rawOrchestrationResults = result.Content.ReadAsStringAsync().Result;
                            dynamic orchestrationResults = JObject.Parse(rawOrchestrationResults);


                            /// Create an export process:
                            string now = DateTime.Now.ToUniversalTime().ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffK");
                            const string itemToExport = "2016050410152760101291561460050569B02260000003692B00000D0D000005";
                            string newProcessName = string.Format("New process as to {0}", DateTime.Now).Replace(" ", "_").Replace(":", "_").Replace("-", "_");
                            string newProcessId = Guid.NewGuid().ToString();
                            JObject processDescription
                                = new JObject(
                                    new JProperty("base",
                                        new JObject(
                                            new JProperty("id", newProcessId)
                                            , new JProperty("type", "MAM_EXPORT_FILE")
                                            , new JProperty("systemType", "interplay-mam")
                                            , new JProperty("systemID", realm)
                                        )
                                    )
                                    , new JProperty("common",
                                        new JObject(
                                            new JProperty("name", newProcessName)
                                            , new JProperty("creator", ".NET_Example")
                                            , new JProperty("created", now)
                                            , new JProperty("modifier", "Service-WorkflowEngine")
                                            , new JProperty("modified", now)
                                        )
                                    )
                                    , new JProperty("attachments",
                                        new JArray(
                                            new JObject(
                                                new JProperty("base",
                                                    new JObject(
                                                        new JProperty("id", itemToExport)
                                                        , new JProperty("type", "Asset")
                                                        , new JProperty("systemType", "interplay-mam")
                                                        , new JProperty("systemID", realm)
                                                    )
                                                )
                                            )
                                        )
                                    )
                                );


                            /// Start the process:
                            // Here, no URL-template library is used in favor to string surgery:
                            string urlStartProcess = startProcessURL+""; //orchestrationResults._embedded["orchestration:orchestration"]._links["orchestration:start-process"].href.ToString();
                            string urlUntemplatedStartProcess = urlStartProcess; //urlStartProcess.Substring(0, urlStartProcess.LastIndexOf("{id}"));

                            HttpResponseMessage response = httpClient.PostAsJsonAsync(urlUntemplatedStartProcess, processDescription).Result;

                            // Begin monitoring the started process:
                            string rawStartedProcessResult = response.Content.ReadAsStringAsync().Result;
                            dynamic startProcessResult = JObject.Parse(rawStartedProcessResult);
                            string urlStartedProcess = startProcessResult._links.self.href.ToString();
                            string lifecycle = startProcessResult.lifecycle.ToString();

                            Console.WriteLine("Process: '{0}' - start initiated", newProcessName);
                            Console.WriteLine("Lifecycle: {0}", lifecycle);
                            if ("pending".Equals(lifecycle) || "running".Equals(lifecycle))
                            {
                                do
                                {
                                    Thread.Sleep(500);
                                    rawStartedProcessResult = httpClient.GetStringAsync(urlStartedProcess).Result;
                                    startProcessResult = JObject.Parse(rawStartedProcessResult);
                                    lifecycle = startProcessResult.lifecycle.ToString();
                                    Console.WriteLine("Lifecycle: {0}", lifecycle);
                                }
                                while ("running".Equals(lifecycle) || "pending".Equals(lifecycle));
                            }
                        }
                        else
                        {
                            Console.WriteLine("Resource <{0}> not found", startProcessURL);
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
