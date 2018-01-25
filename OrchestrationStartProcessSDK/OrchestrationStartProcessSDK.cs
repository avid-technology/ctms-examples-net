//
// Copyright 2016 by Avid Technology, Inc.
//

using Avid.Platform.SDK;
using Avid.Platform.SDK.Authorization;
using Avid.Platform.SDK.Model.Orchestration;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OrchestrationStartProcessSDK
{
    /// <summary>
    /// This example starts a process and monitors its progress.
    /// </summary>
    public class Program
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

                Uri upstreamServerUrl = new Uri(string.Format("https://{0}", apiDomain));
                using (OrchestrationClient orchestrationClient = new OrchestrationClient(new MCUXAuthorizationConnection(upstreamServerUrl, username, password), realm))
                {
                    /// Create and start an export process with attachments:
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
                    Process process = orchestrationClient.StartProcess(processDescription);
                    
                    Console.WriteLine("Process: '{0}' - start initiated", newProcessName);
                    Console.WriteLine("Lifecycle: {0}", process.LifeCycle);
                    /// Monitor the running process:                                
                    while("running".Equals(process.LifeCycle) || "pending".Equals(process.LifeCycle))
                    {
                        Thread.Sleep(500);
                        // Directly get the process instance via its id:
                        process = orchestrationClient.GetProcess(newProcessId);
                        Console.WriteLine("Lifecycle: {0}", process.LifeCycle);
                    }

                }
                Console.WriteLine("End");
            }
        }
    }
}
