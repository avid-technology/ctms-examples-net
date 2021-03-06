﻿//
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
            if (3 != args.Length)
            {
                Console.WriteLine($"Usage: {System.Reflection.Assembly.GetEntryAssembly().ManifestModule.Name} <apidomain> <httpbasicauthstring> <realm>");
            }
            else
            {
                string apiDomain = args[0];
                string httpBasicAuthString = args[1];
                string realm = args[2];

                HttpClient httpClient = PlatformTools.PlatformTools.Authorize(apiDomain, httpBasicAuthString);

                bool successfullyAuthorized = null != httpClient;
                if (successfullyAuthorized)
                {
                    try
                    {
                        const string orchestrationServiceType = "avid.orchestration.ctc";

                        var registryServiceVersion = "0";
                        string defaultProcessUriTemplate = $"https://{apiDomain}/apis/{orchestrationServiceType};version=0;realm={realm}/processes/{{id}}";
                        string processUriTemplate = PlatformTools.PlatformTools.FindInRegistry(httpClient, apiDomain, orchestrationServiceType, registryServiceVersion, "orchestration:process", defaultProcessUriTemplate, realm);

                        UriTemplate startProcessTemplate = new UriTemplate(processUriTemplate);
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
                            string newProcessName = $"New process as to {DateTime.Now}".Replace(" ", "_").Replace(":", "_").Replace("-", "_");
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

                            Console.WriteLine($"Process: '{newProcessName}' - start initiated");
                            Console.WriteLine($"Lifecycle: {lifecycle}");
                            if ("pending".Equals(lifecycle) || "running".Equals(lifecycle))
                            {
                                do
                                {
                                    Thread.Sleep(500);
                                    rawStartedProcessResult = httpClient.GetStringAsync(urlStartedProcess).Result;
                                    startProcessResult = JObject.Parse(rawStartedProcessResult);
                                    lifecycle = startProcessResult.lifecycle.ToString();
                                    Console.WriteLine($"Lifecycle: {lifecycle}");
                                }
                                while ("running".Equals(lifecycle) || "pending".Equals(lifecycle));
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Resource <{startProcessURL}> not found");
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
